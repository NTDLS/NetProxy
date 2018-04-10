using NetProxy.Hub.Common;
using NetProxy.Library.Routing;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Caching;
using System.Security.Cryptography;
using System.Text;

namespace NetProxy.Service.Routing
{
    internal static class Packetizer
    {
        private class KeySet
        {
            public byte[] Bytes { get; set; }
            public byte[] IV { get; set; }
            private SymmetricAlgorithm algorithm;

            public ICryptoTransform GetEncryptor()
            {
                return algorithm.CreateEncryptor(Bytes, IV);
            }
            public ICryptoTransform CreateDecryptor()
            {
                return algorithm.CreateDecryptor(Bytes, IV);
            }

            public KeySet(string textKey, string salt)
            {
                using (Rfc2898DeriveBytes k2 = new Rfc2898DeriveBytes(textKey, Encoding.Unicode.GetBytes(salt)))
                {
                    this.Bytes = k2.GetBytes(32);
                    this.IV = k2.GetBytes(16);
                    this.algorithm = Aes.Create();
                }
            }
        }

        private static MemoryCache keyCache = new MemoryCache("NetProxy.Service.Routing.Packetizer.KeySet");

        private static KeySet GetKey(string textKey, string salt)
        {
            lock (keyCache)
            {
                string lookupKey = Library.Crypto.Hashing.Sha1(textKey + salt);

                if (keyCache.Contains(lookupKey))
                {
                    return (KeySet)keyCache[lookupKey];
                }

                var key = new KeySet(textKey, salt);

                keyCache.Add(lookupKey, key, new CacheItemPolicy()
                {
                    SlidingExpiration = new TimeSpan(0, 0, 10)
                });
                return key;
            }
        }

        public static byte[] Encrypt(string textKey, string salt, byte[] inputbuffer)
        {
            var key = GetKey(textKey, salt);
            using (var crypto = key.GetEncryptor())
            {
                return crypto.TransformFinalBlock(inputbuffer, 0, inputbuffer.Length);
            }
        }

        public static byte[] Decrypt(string textKey, string salt, byte[] inputbuffer)
        {
            var key = GetKey(textKey, salt);
            using (var crypto = key.CreateDecryptor())
            {
                string outbound = Encoding.UTF8.GetString(inputbuffer).Trim();


                return crypto.TransformFinalBlock(inputbuffer, 0, inputbuffer.Length);
            }
        }

        public static byte[] AssembleMessagePacket(byte[] payload, int payloadLength, bool compress, bool encrypt, string encryptPacketKey, string keySalt)
        {
            var envelope = new PacketEnvelope();

            envelope.Label = null;
            envelope.Payload = payload.Take(payloadLength).ToArray();

            return AssembleMessagePacket(envelope, compress, encrypt, encryptPacketKey, keySalt);
        }

        public static byte[] AssembleMessagePacket(PacketEnvelope envelope, bool compress, bool encrypt, string encryptPacketKey, string keySalt)
        {
            try
            {
                byte[] payloadBody = Serialization.ObjectToByteArray(envelope);

                if (compress)
                {
                    payloadBody = Zip(payloadBody);
                }

                if (encrypt)
                {
                    payloadBody = Encrypt(encryptPacketKey, keySalt, payloadBody);
                }

                int grossPacketSize = payloadBody.Length + Constants.PAYLOAD_HEADEER_SIZE;

                byte[] packetBytes = new byte[grossPacketSize];

                UInt16 payloadCrc = CRC16.ComputeChecksum(payloadBody);

                Buffer.BlockCopy(BitConverter.GetBytes(Constants.PAYLOAD_DELIMITER), 0, packetBytes, 0, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(grossPacketSize), 0, packetBytes, 4, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(payloadCrc), 0, packetBytes, 8, 2);
                Buffer.BlockCopy(payloadBody, 0, packetBytes, Constants.PAYLOAD_HEADEER_SIZE, payloadBody.Length);

                return packetBytes;
            }
            catch(Exception ex)
            {
                //TODO: allow this to be logged.
            }

            return null;
        }

        private static void SkipPacket(ref SocketState state)
        {
            try
            {
                Byte[] payloadDelimiterBytes = new Byte[4];

                for (int offset = 1; offset < state.PayloadBuilderLength - payloadDelimiterBytes.Length; offset++)
                {
                    Buffer.BlockCopy(state.PayloadBuilder, offset, payloadDelimiterBytes, 0, payloadDelimiterBytes.Length);

                    int value = BitConverter.ToInt32(payloadDelimiterBytes, 0);

                    if (value == Constants.PAYLOAD_DELIMITER)
                    {
                        Buffer.BlockCopy(state.PayloadBuilder, offset, state.PayloadBuilder, 0, state.PayloadBuilderLength - offset);
                        state.PayloadBuilderLength = state.PayloadBuilderLength - offset;
                        return;
                    }
                }

                Array.Clear(state.PayloadBuilder, 0, state.PayloadBuilder.Length);
                state.PayloadBuilderLength = 0;
            }
            catch
            {
                //TODO: allow this to be logged.
            }
        }

        public static List<PacketEnvelope> DissasemblePacketData(Router router, SocketState state, bool compress, bool encrypt, string encryptPacketKey, string keySalt)
        {
            List<PacketEnvelope> envelopes = new List<PacketEnvelope>();

            try
            {
                if (state.PayloadBuilderLength + state.BytesReceived >= state.PayloadBuilder.Length)
                {
                    Array.Resize(ref state.PayloadBuilder, state.PayloadBuilderLength + state.BytesReceived);
                }

                Buffer.BlockCopy(state.Buffer, 0, state.PayloadBuilder, state.PayloadBuilderLength, state.BytesReceived);

                state.PayloadBuilderLength = state.PayloadBuilderLength + state.BytesReceived;

                while (state.PayloadBuilderLength > Constants.PAYLOAD_HEADEER_SIZE) //[PayloadSize] and [CRC16]
                {
                    Byte[] payloadDelimiterBytes = new Byte[4];
                    Byte[] payloadSizeBytes = new Byte[4];
                    Byte[] expectedCRC16Bytes = new Byte[2];

                    Buffer.BlockCopy(state.PayloadBuilder, 0, payloadDelimiterBytes, 0, payloadDelimiterBytes.Length);
                    Buffer.BlockCopy(state.PayloadBuilder, 4, payloadSizeBytes, 0, payloadSizeBytes.Length);
                    Buffer.BlockCopy(state.PayloadBuilder, 8, expectedCRC16Bytes, 0, expectedCRC16Bytes.Length);

                    int payloadDelimiter = BitConverter.ToInt32(payloadDelimiterBytes, 0);
                    int grossPayloadSize = BitConverter.ToInt32(payloadSizeBytes, 0);
                    UInt16 expectedCRC16 = BitConverter.ToUInt16(expectedCRC16Bytes, 0);

                    if (payloadDelimiter != Constants.PAYLOAD_DELIMITER)
                    {
                        SkipPacket(ref state);
                        //throw new Exception("Malformed payload packet, invalid delimiter.");
                        router.Stats.PacketMalformedCount++;
                        continue;
                    }

                    if (grossPayloadSize < Constants.DEFAULT_MIN_MSG_SIZE || grossPayloadSize > Constants.DEFAULT_MAX_MSG_SIZE)
                    {
                        SkipPacket(ref state);
                        //throw new Exception("Malformed payload packet, invalid length."); 
                        router.Stats.PacketSizeExceededCount++;
                        continue;
                    }

                    if (state.PayloadBuilderLength < grossPayloadSize)
                    {
                        //We have data in the buffer, but it's not enough to make up
                        //  the entire message (fragmented packet) so we will break and wait on more data.
                        router.Stats.PacketFragmentCount++;
                        break;
                    }

                    UInt16 actualCRC16 = CRC16.ComputeChecksum(state.PayloadBuilder, Constants.PAYLOAD_HEADEER_SIZE, grossPayloadSize - Constants.PAYLOAD_HEADEER_SIZE);

                    if (actualCRC16 != expectedCRC16)
                    {
                        SkipPacket(ref state);
                        //throw new Exception("Malformed payload packet, invalid CRC.");
                        router.Stats.PacketCRCFailureCount++;
                        continue;
                    }

                    int netPayloadSize = grossPayloadSize - Constants.PAYLOAD_HEADEER_SIZE;
                    byte[] payloadBytes = new byte[netPayloadSize];

                    Buffer.BlockCopy(state.PayloadBuilder, Constants.PAYLOAD_HEADEER_SIZE, payloadBytes, 0, netPayloadSize);

                    if (encrypt)
                    {
                        payloadBytes = Decrypt(encryptPacketKey, keySalt, payloadBytes);
                    }

                    if (compress)
                    {
                        payloadBytes = Unzip(payloadBytes);
                    }

                    envelopes.Add((PacketEnvelope)Serialization.ByteArrayToObject(payloadBytes));

                    //Zero out the consumed portion of the payload buffer - more for fun than anything else.
                    Array.Clear(state.PayloadBuilder, 0, grossPayloadSize);

                    Buffer.BlockCopy(state.PayloadBuilder, grossPayloadSize, state.PayloadBuilder, 0, state.PayloadBuilderLength - grossPayloadSize);
                    state.PayloadBuilderLength -= grossPayloadSize;
                }
            }
            catch(Exception ex)
            {
                //TODO: allow this to be logged.
            }

            return envelopes;
        }

        public static byte[] Zip(byte[] bytes)
        {
            using (var msi = new MemoryStream(bytes, 0, bytes.Length))
            using (var mso = new MemoryStream())
            {
                using (var gs = new GZipStream(mso, CompressionLevel.Optimal))
                {
                    msi.CopyTo(gs);
                }

                return mso.ToArray();
            }
        }

        public static byte[] Unzip(byte[] bytes)
        {
            using (var msi = new MemoryStream(bytes))
            using (var mso = new MemoryStream())
            {
                using (var gs = new GZipStream(msi, CompressionMode.Decompress))
                {
                    gs.CopyTo(mso);
                }

                return mso.ToArray();
            }
        }
    }
}