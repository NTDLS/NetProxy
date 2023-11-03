using NetProxy.Hub.Common;
using NetProxy.Library.Routing;
using System.IO.Compression;

namespace NetProxy.Service.Routing
{
    internal static class Packetizer
    {

        public static byte[] AssembleMessagePacket(byte[] payload, int payloadLength, bool compress, bool encrypt, string encryptPacketKey, string keySalt)
        {
            var envelope = new PacketEnvelope
            {
                Label = null,
                Payload = payload.Take(payloadLength).ToArray()
            };

            return AssembleMessagePacket(envelope, compress, encrypt, encryptPacketKey, keySalt);
        }

        public static byte[] AssembleMessagePacket(PacketEnvelope envelope, bool compress, bool encrypt, string encryptPacketKey, string keySalt)
        {
            try
            {
                byte[] payloadBody = Serialization.SerializeToByteArray(envelope);

                if (compress)
                {
                    payloadBody = Zip(payloadBody);
                }

                if (encrypt)
                {
                    //payloadBody = Encrypt(encryptPacketKey, keySalt, payloadBody);
                }

                int grossPacketSize = payloadBody.Length + Constants.PayloadHeaderSize;

                byte[] packetBytes = new byte[grossPacketSize];

                UInt16 payloadCrc = Crc16.ComputeChecksum(payloadBody);

                Buffer.BlockCopy(BitConverter.GetBytes(Constants.PayloadDelimiter), 0, packetBytes, 0, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(grossPacketSize), 0, packetBytes, 4, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(payloadCrc), 0, packetBytes, 8, 2);
                Buffer.BlockCopy(payloadBody, 0, packetBytes, Constants.PayloadHeaderSize, payloadBody.Length);

                return packetBytes;
            }
            catch (Exception ex)
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

                    if (value == Constants.PayloadDelimiter)
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

                while (state.PayloadBuilderLength > Constants.PayloadHeaderSize) //[PayloadSize] and [CRC16]
                {
                    Byte[] payloadDelimiterBytes = new Byte[4];
                    Byte[] payloadSizeBytes = new Byte[4];
                    Byte[] expectedCrc16Bytes = new Byte[2];

                    Buffer.BlockCopy(state.PayloadBuilder, 0, payloadDelimiterBytes, 0, payloadDelimiterBytes.Length);
                    Buffer.BlockCopy(state.PayloadBuilder, 4, payloadSizeBytes, 0, payloadSizeBytes.Length);
                    Buffer.BlockCopy(state.PayloadBuilder, 8, expectedCrc16Bytes, 0, expectedCrc16Bytes.Length);

                    int payloadDelimiter = BitConverter.ToInt32(payloadDelimiterBytes, 0);
                    int grossPayloadSize = BitConverter.ToInt32(payloadSizeBytes, 0);
                    UInt16 expectedCrc16 = BitConverter.ToUInt16(expectedCrc16Bytes, 0);

                    if (payloadDelimiter != Constants.PayloadDelimiter)
                    {
                        SkipPacket(ref state);
                        //throw new Exception("Malformed payload packet, invalid delimiter.");
                        router.Stats.PacketMalformedCount++;
                        continue;
                    }

                    if (grossPayloadSize < Constants.DefaultMinMsgSize || grossPayloadSize > Constants.DefaultMaxMsgSize)
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

                    UInt16 actualCrc16 = Crc16.ComputeChecksum(state.PayloadBuilder, Constants.PayloadHeaderSize, grossPayloadSize - Constants.PayloadHeaderSize);

                    if (actualCrc16 != expectedCrc16)
                    {
                        SkipPacket(ref state);
                        //throw new Exception("Malformed payload packet, invalid CRC.");
                        router.Stats.PacketCrcFailureCount++;
                        continue;
                    }

                    int netPayloadSize = grossPayloadSize - Constants.PayloadHeaderSize;
                    byte[] payloadBytes = new byte[netPayloadSize];

                    Buffer.BlockCopy(state.PayloadBuilder, Constants.PayloadHeaderSize, payloadBytes, 0, netPayloadSize);

                    if (encrypt)
                    {
                        //payloadBytes = Decrypt(encryptPacketKey, keySalt, payloadBytes);
                    }

                    if (compress)
                    {
                        payloadBytes = Unzip(payloadBytes);
                    }

                    envelopes.Add(Serialization.DeserializeToObject<PacketEnvelope>(payloadBytes));

                    //Zero out the consumed portion of the payload buffer - more for fun than anything else.
                    Array.Clear(state.PayloadBuilder, 0, grossPayloadSize);

                    Buffer.BlockCopy(state.PayloadBuilder, grossPayloadSize, state.PayloadBuilder, 0, state.PayloadBuilderLength - grossPayloadSize);
                    state.PayloadBuilderLength -= grossPayloadSize;
                }
            }
            catch (Exception ex)
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