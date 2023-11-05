using NetProxy.Hub.Common;
using ProtoBuf;
using System.IO.Compression;

namespace NetProxy.Hub.MessageFraming
{
    internal static class NpFraming
    {
        public delegate void ProcessFramePayloadCallback(NpHubSocketState state, NpFrame frame);

        public static byte[] AssembleFrame(NpFrame frame)
        {
            var payloadBody = SerializeToByteArray(frame);
            var payloadBytes = Zip(payloadBody);
            int grossFrameSize = payloadBytes.Length + NpConstants.FrameHeaderSize;
            var frameBytes = new byte[grossFrameSize];

            var payloadCrc = NpFrameChecksum.ComputeCRC16(payloadBytes);

            Buffer.BlockCopy(BitConverter.GetBytes(NpConstants.FrameDelimiter), 0, frameBytes, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(grossFrameSize), 0, frameBytes, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(payloadCrc), 0, frameBytes, 8, 2);
            Buffer.BlockCopy(payloadBytes, 0, frameBytes, NpConstants.FrameHeaderSize, payloadBytes.Length);

            return frameBytes;
        }

        private static void SkipFrame(ref NpHubSocketState state)
        {
            try
            {
                byte[] payloadDelimiterBytes = new byte[4];

                for (int offset = 1; offset < state.PayloadBuilderLength - payloadDelimiterBytes.Length; offset++)
                {
                    Buffer.BlockCopy(state.PayloadBuilder, offset, payloadDelimiterBytes, 0, payloadDelimiterBytes.Length);

                    int value = BitConverter.ToInt32(payloadDelimiterBytes, 0);

                    if (value == NpConstants.FrameDelimiter)
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

        public static void ProcessFrameBuffer(NpHubSocketState state, ProcessFramePayloadCallback processPayload)
        {
            try
            {
                if (state.PayloadBuilderLength + state.BytesReceived >= state.PayloadBuilder.Length)
                {
                    Array.Resize(ref state.PayloadBuilder, state.PayloadBuilderLength + state.BytesReceived);
                }

                Buffer.BlockCopy(state.Buffer, 0, state.PayloadBuilder, state.PayloadBuilderLength, state.BytesReceived);

                state.PayloadBuilderLength = state.PayloadBuilderLength + state.BytesReceived;

                while (state.PayloadBuilderLength > NpConstants.FrameHeaderSize) //[PayloadSize] and [CRC16]
                {
                    var payloadDelimiterBytes = new byte[4];
                    var payloadSizeBytes = new byte[4];
                    var expectedCrc16Bytes = new byte[2];

                    Buffer.BlockCopy(state.PayloadBuilder, 0, payloadDelimiterBytes, 0, payloadDelimiterBytes.Length);
                    Buffer.BlockCopy(state.PayloadBuilder, 4, payloadSizeBytes, 0, payloadSizeBytes.Length);
                    Buffer.BlockCopy(state.PayloadBuilder, 8, expectedCrc16Bytes, 0, expectedCrc16Bytes.Length);

                    int payloadDelimiter = BitConverter.ToInt32(payloadDelimiterBytes, 0);
                    int grossPayloadSize = BitConverter.ToInt32(payloadSizeBytes, 0);
                    ushort expectedCrc16 = BitConverter.ToUInt16(expectedCrc16Bytes, 0);

                    if (payloadDelimiter != NpConstants.FrameDelimiter)
                    {
                        SkipFrame(ref state);
                        //throw new Exception("Malformed payload frame, invalid delimiter.");
                        continue;
                    }

                    if (state.PayloadBuilderLength < grossPayloadSize)
                    {
                        //We have data in the buffer, but it's not enough to make up
                        //  the entire message so we will break and wait on more data.
                        break;
                    }

                    ushort actualCrc16 = NpFrameChecksum.ComputeCRC16(state.PayloadBuilder, NpConstants.FrameHeaderSize, grossPayloadSize - NpConstants.FrameHeaderSize);

                    if (actualCrc16 != expectedCrc16)
                    {
                        SkipFrame(ref state);
                        //throw new Exception("Malformed payload frame, invalid CRC.");
                        continue;
                    }

                    int netPayloadSize = grossPayloadSize - NpConstants.FrameHeaderSize;
                    var payloadBytes = new byte[netPayloadSize];

                    Buffer.BlockCopy(state.PayloadBuilder, NpConstants.FrameHeaderSize, payloadBytes, 0, netPayloadSize);

                    var payloadBody = Unzip(payloadBytes);

                    processPayload(state, DeserializeToObject<NpFrame>(payloadBody));

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
        }

        private static byte[] Zip(byte[] bytes)
        {
            using var msi = new MemoryStream(bytes);
            using var mso = new MemoryStream();
            using (var gs = new DeflateStream(mso, CompressionLevel.SmallestSize))
            {
                msi.CopyTo(gs);
            }
            return mso.ToArray();
        }

        private static byte[] Unzip(byte[] bytes)
        {
            using var msi = new MemoryStream(bytes);
            using var mso = new MemoryStream();
            using (var gs = new DeflateStream(msi, CompressionMode.Decompress))
            {
                gs.CopyTo(mso);
            }
            return mso.ToArray();
        }

        private static byte[] SerializeToByteArray(object obj)
        {
            if (obj == null) return Array.Empty<byte>();
            using var stream = new MemoryStream();
            Serializer.Serialize(stream, obj);
            return stream.ToArray();
        }

        private static T DeserializeToObject<T>(byte[] arrBytes)
        {
            using var stream = new MemoryStream();
            stream.Write(arrBytes, 0, arrBytes.Length);
            stream.Seek(0, SeekOrigin.Begin);
            return Serializer.Deserialize<T>(stream);
        }
    }
}
