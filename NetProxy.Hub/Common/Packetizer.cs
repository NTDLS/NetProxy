using System;
using System.IO;
using System.IO.Compression;
using NetProxy.Library.Routing;

namespace NetProxy.Hub.Common
{
    internal static class Packetizer
    {
        public delegate void ProcessPayloadCallback(SocketState state, Packet packet);

        public static byte[] AssembleMessagePacket(Packet packet)
        {
            try
            {
                byte[] payloadBody = Serialization.ObjectToByteArray(packet);

                byte[] payloadBytes = Zip(payloadBody);
                int grossPacketSize = payloadBytes.Length + Constants.PAYLOAD_HEADEER_SIZE;

                byte[] packetBytes = new byte[grossPacketSize];

                UInt16 payloadCrc = CRC16.ComputeChecksum(payloadBytes);

                Buffer.BlockCopy(BitConverter.GetBytes(Constants.PAYLOAD_DELIMITER), 0, packetBytes, 0, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(grossPacketSize), 0, packetBytes, 4, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(payloadCrc), 0, packetBytes, 8, 2);
                Buffer.BlockCopy(payloadBytes, 0, packetBytes, Constants.PAYLOAD_HEADEER_SIZE, payloadBytes.Length);

                return packetBytes;
            }
            catch
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

        public static void DissasemblePacketData(SocketState state, ProcessPayloadCallback processPayload)
        {
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
                        continue;
                    }

                    if (grossPayloadSize < Constants.DEFAULT_MIN_MSG_SIZE || grossPayloadSize > Constants.DEFAULT_MAX_MSG_SIZE)
                    {
                        SkipPacket(ref state);
                        //throw new Exception("Malformed payload packet, invalid length."); 
                        continue;
                    }

                    if (state.PayloadBuilderLength < grossPayloadSize)
                    {
                        //We have data in the buffer, but it's not enough to make up
                        //  the entire message so we will break and wait on more data.
                        break;
                    }

                    UInt16 actualCRC16 = CRC16.ComputeChecksum(state.PayloadBuilder, Constants.PAYLOAD_HEADEER_SIZE, grossPayloadSize - Constants.PAYLOAD_HEADEER_SIZE);

                    if (actualCRC16 != expectedCRC16)
                    {
                        SkipPacket(ref state);
                        //throw new Exception("Malformed payload packet, invalid CRC.");
                        continue;
                    }

                    int netPayloadSize = grossPayloadSize - Constants.PAYLOAD_HEADEER_SIZE;
                    byte[] payloadBytes = new byte[netPayloadSize];

                    Buffer.BlockCopy(state.PayloadBuilder, Constants.PAYLOAD_HEADEER_SIZE, payloadBytes, 0, netPayloadSize);

                    byte[] payloadBody = Unzip(payloadBytes);

                    Packet packet = (Packet)Serialization.ByteArrayToObject(payloadBody);

                    processPayload(state, packet);

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
        }

        public static byte[] Zip(byte[] bytes)
        {
            using (var msi = new MemoryStream(bytes))
            using (var mso = new MemoryStream())
            {
                using (var gs = new GZipStream(mso, CompressionMode.Compress))
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
