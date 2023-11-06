using NetProxy.Hub.MessageFraming.FramePayloads;
using NetProxy.MessageHub.MessageFraming.Payloads;
using Newtonsoft.Json;
using NTDLS.Semaphore;
using System.Net.Sockets;
using System.Reflection;
using static NetProxy.Hub.MessageFraming.Types;

namespace NetProxy.Hub.MessageFraming
{
    /// <summary>
    /// TCP packets can be fragmented or combined. The packetizer rebuilds what was originally
    /// sent via the TCP send() call, provides compression and also performs a CRC check.
    /// </summary>
    internal static class Framing
    {
        private static readonly CriticalResource<Dictionary<string, MethodInfo>> _reflectioncache = new();
        private static readonly List<QueryAwaitingReply> _queriesAwaitingReplies = new();

        #region Extension methods.

        public static bool ReceiveAndProcessStreamFrames(this NetworkStream stream,
            FrameBuffer frameBuffer, ProcessFrameNotification processNotificationCallback,
            ProcessFrameQuery processFrameQueryCallback)
        {
            if (stream == null)
            {
                throw new Exception("ReceiveAndProcessStreamFrames: stream can not be null.");
            }

            Array.Clear(frameBuffer.ReceiveBuffer);
            frameBuffer.ReceiveBufferUsed = stream.Read(frameBuffer.ReceiveBuffer, 0, frameBuffer.ReceiveBuffer.Length);
            if (frameBuffer.ReceiveBufferUsed == 0)
            {
                return false;
            }

            ProcessFrameBuffer(stream, frameBuffer, processNotificationCallback, processFrameQueryCallback);

            return true;
        }

        /// <summary>
        /// Sends a INtFramePayloadQuery that expects a INtFramePayloadReply in return.
        /// </summary>
        public static async Task<T> SendQueryFrame<T>(this Stream stream, IFramePayloadQuery payload, int queryTimeout = -1)
        {
            if (stream == null)
            {
                throw new Exception("SendStreamFramePayload stream can not be null.");
            }

            var cmd = new Frame()
            {
                EnclosedPayloadType = payload.GetType()?.AssemblyQualifiedName ?? string.Empty,
                Payload = JsonConvert.SerializeObject(payload)
            };

            var queryAwaitingReply = new QueryAwaitingReply()
            {
                FrameId = cmd.Id,
            };

            _queriesAwaitingReplies.Add(queryAwaitingReply);

            return await Task.Run(() =>
            {
                var frameBytes = AssembleFrame(cmd);
                stream.Write(frameBytes, 0, frameBytes.Length);

                //Wait for a reply. When a reply is received, it will be routed to the correct query via ApplyQueryReply().
                //ApplyQueryReply() will apply the payload data to queryAwaitingReply and trigger the wait event.
                if (queryAwaitingReply.WaitEvent.WaitOne(queryTimeout) == false)
                {
                    _queriesAwaitingReplies.Remove(queryAwaitingReply);
                    throw new Exception("Query timeout expired while waiting on reply.");
                }

                _queriesAwaitingReplies.Remove(queryAwaitingReply);

                if (queryAwaitingReply.ReplyPayload == null)
                {
                    throw new Exception("The reply payload can not be null.");
                }

                return (T)queryAwaitingReply.ReplyPayload;
            });
        }

        /// <summary>
        /// Sends a reply to a INtFramePayloadQuery
        /// </summary>
        public static void SendReplyFrame(this Stream stream, Frame queryFrame, IFramePayloadReply payload)
        {
            if (stream == null)
            {
                throw new Exception("SendStreamFramePayload stream can not be null.");
            }
            var cmd = new Frame()
            {
                Id = queryFrame.Id,
                EnclosedPayloadType = payload.GetType()?.AssemblyQualifiedName ?? string.Empty,
                Payload = JsonConvert.SerializeObject(payload)
            };

            var frameBytes = AssembleFrame(cmd);
            stream.Write(frameBytes, 0, frameBytes.Length);
        }

        /// <summary>
        /// Sends a one way (fire and forget) INtFramePayloadNotification.
        /// </summary>
        public static void SendNotificationFrame(this NetworkStream stream, IFramePayloadNotification payload)
        {
            if (stream == null)
            {
                throw new Exception("SendStreamFramePayload stream can not be null.");
            }
            var cmd = new Frame()
            {
                EnclosedPayloadType = payload.GetType()?.AssemblyQualifiedName ?? string.Empty,
                Payload = JsonConvert.SerializeObject(payload)
            };

            var frameBytes = AssembleFrame(cmd);
            stream.Write(frameBytes, 0, frameBytes.Length);
        }

        #endregion

        private static byte[] AssembleFrame(Frame frame)
        {
            try
            {
                var frameBody = Utility.SerializeToByteArray(frame);
                var frameBytes = Utility.Compress(frameBody);

                var grossFrameSize = frameBytes.Length + NtFrameDefaults.FRAME_HEADER_SIZE;
                var grossFrameBytes = new byte[grossFrameSize];
                var frameCrc = CRC16.ComputeChecksum(frameBytes);

                Buffer.BlockCopy(BitConverter.GetBytes(NtFrameDefaults.FRAME_DELIMITER), 0, grossFrameBytes, 0, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(grossFrameSize), 0, grossFrameBytes, 4, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(frameCrc), 0, grossFrameBytes, 8, 2);
                Buffer.BlockCopy(frameBytes, 0, grossFrameBytes, NtFrameDefaults.FRAME_HEADER_SIZE, frameBytes.Length);

                return grossFrameBytes;
            }
            catch (Exception ex)
            {
                //Core.Logging.Write(Constants.NtLogSeverity.Exception, $"AssembleFrame: {ex.Message}");
                throw;
            }
        }

        private static void SkipFrame(ref FrameBuffer frameBuffer)
        {
            try
            {
                var frameDelimiterBytes = new byte[4];

                for (int offset = 1; offset < frameBuffer.FrameBuilderLength - frameDelimiterBytes.Length; offset++)
                {
                    Buffer.BlockCopy(frameBuffer.FrameBuilder, offset, frameDelimiterBytes, 0, frameDelimiterBytes.Length);

                    var value = BitConverter.ToInt32(frameDelimiterBytes, 0);

                    if (value == NtFrameDefaults.FRAME_DELIMITER)
                    {
                        Buffer.BlockCopy(frameBuffer.FrameBuilder, offset, frameBuffer.FrameBuilder, 0, frameBuffer.FrameBuilderLength - offset);
                        frameBuffer.FrameBuilderLength -= offset;
                        return;
                    }
                }
                Array.Clear(frameBuffer.FrameBuilder, 0, frameBuffer.FrameBuilder.Length);
                frameBuffer.FrameBuilderLength = 0;
            }
            catch (Exception ex)
            {
                //Core.Logging.Write(Constants.NtLogSeverity.Exception, $"SkipFrame: {ex.Message}");
            }
        }

        private static void ProcessFrameBuffer(this NetworkStream stream, FrameBuffer frameBuffer, ProcessFrameNotification processNotificationCallback,
             ProcessFrameQuery processFrameQueryCallback)
        {
            try
            {
                if (frameBuffer.FrameBuilderLength + frameBuffer.ReceiveBufferUsed >= frameBuffer.FrameBuilder.Length)
                {
                    Array.Resize(ref frameBuffer.FrameBuilder, frameBuffer.FrameBuilderLength + frameBuffer.ReceiveBufferUsed);
                }

                Buffer.BlockCopy(frameBuffer.ReceiveBuffer, 0, frameBuffer.FrameBuilder, frameBuffer.FrameBuilderLength, frameBuffer.ReceiveBufferUsed);

                frameBuffer.FrameBuilderLength = frameBuffer.FrameBuilderLength + frameBuffer.ReceiveBufferUsed;

                while (frameBuffer.FrameBuilderLength > NtFrameDefaults.FRAME_HEADER_SIZE) //[FrameSize] and [CRC16]
                {
                    var frameDelimiterBytes = new byte[4];
                    var frameSizeBytes = new byte[4];
                    var expectedCRC16Bytes = new byte[2];

                    Buffer.BlockCopy(frameBuffer.FrameBuilder, 0, frameDelimiterBytes, 0, frameDelimiterBytes.Length);
                    Buffer.BlockCopy(frameBuffer.FrameBuilder, 4, frameSizeBytes, 0, frameSizeBytes.Length);
                    Buffer.BlockCopy(frameBuffer.FrameBuilder, 8, expectedCRC16Bytes, 0, expectedCRC16Bytes.Length);

                    var frameDelimiter = BitConverter.ToInt32(frameDelimiterBytes, 0);
                    var grossFrameSize = BitConverter.ToInt32(frameSizeBytes, 0);
                    var expectedCRC16 = BitConverter.ToUInt16(expectedCRC16Bytes, 0);

                    if (frameDelimiter != NtFrameDefaults.FRAME_DELIMITER)
                    {
                        //tunnel.Core.Logging.Write(Constants.NtLogSeverity.Warning, "ProcessFrameBuffer: Malformed frame, invalid delimiter.");
                        SkipFrame(ref frameBuffer);
                        continue;
                    }

                    if (grossFrameSize < 0)
                    {
                        //tunnel.Core.Logging.Write(Constants.NtLogSeverity.Warning, "ProcessFrameBuffer: Malformed frame, invalid length.");
                        SkipFrame(ref frameBuffer);
                        continue;
                    }

                    if (frameBuffer.FrameBuilderLength < grossFrameSize)
                    {
                        //We have data in the buffer, but it's not enough to make up
                        //  the entire message so we will break and wait on more data.
                        break;
                    }

                    var actualCRC16 = CRC16.ComputeChecksum(frameBuffer.FrameBuilder, NtFrameDefaults.FRAME_HEADER_SIZE, grossFrameSize - NtFrameDefaults.FRAME_HEADER_SIZE);

                    if (actualCRC16 != expectedCRC16)
                    {
                        //Core.Logging.Write(Constants.NtLogSeverity.Warning, "ProcessFrameBuffer: Malformed frame, invalid CRC.");
                        SkipFrame(ref frameBuffer);
                        continue;
                    }

                    var netFrameSize = grossFrameSize - NtFrameDefaults.FRAME_HEADER_SIZE;
                    var frameBytes = new byte[netFrameSize];
                    Buffer.BlockCopy(frameBuffer.FrameBuilder, NtFrameDefaults.FRAME_HEADER_SIZE, frameBytes, 0, netFrameSize);
                    var frameBody = Utility.Decompress(frameBytes);
                    var frame = Utility.DeserializeToObject<Frame>(frameBody);

                    //Zero out the consumed portion of the frame buffer - more for fun than anything else.
                    Array.Clear(frameBuffer.FrameBuilder, 0, grossFrameSize);

                    Buffer.BlockCopy(frameBuffer.FrameBuilder, grossFrameSize, frameBuffer.FrameBuilder, 0, frameBuffer.FrameBuilderLength - grossFrameSize);
                    frameBuffer.FrameBuilderLength -= grossFrameSize;

                    var payload = ExtractFramePayload(frame);

                    if (payload is IFramePayloadQuery query)
                    {
                        var replyPayload = processFrameQueryCallback(query);
                        stream.SendReplyFrame(frame, replyPayload);
                    }
                    else if (payload is IFramePayloadReply reply)
                    {
                        // A reply to a query was received, we need to find the waiting query - set the reply payload data and trigger the wait event.
                        var waitingQuery = _queriesAwaitingReplies.Where(o => o.FrameId == frame.Id).Single();
                        waitingQuery.ReplyPayload = reply;
                        waitingQuery.WaitEvent.Set();
                    }
                    else if (payload is IFramePayloadNotification notification)
                    {
                        processNotificationCallback(notification);
                    }
                    else
                    {
                        throw new Exception("ProcessFrameBuffer: Encountered undefined frame payload type.");
                    }
                }
            }
            catch (Exception ex)
            {
                //Core.Logging.Write(Constants.NtLogSeverity.Exception, $"ProcessFrameBuffer: {ex.Message}");
            }
        }

        private static IFramePayload ExtractFramePayload(Frame frame)
        {
            var genericToObjectMethod = _reflectioncache.Use((o) =>
            {
                if (o.TryGetValue(frame.EnclosedPayloadType, out var method))
                {
                    return method;
                }
                return null;
            });

            if (genericToObjectMethod != null)
            {
                return (IFramePayload?)genericToObjectMethod.Invoke(null, new object[] { frame.Payload })
                    ?? throw new Exception($"ExtractFramePayload: Payload can not be null.");
            }

            AppDomain currentDomain = AppDomain.CurrentDomain;

            // Get a list of loaded assemblies in the current application domain
            Assembly[] loadedAssemblies = currentDomain.GetAssemblies();

            var genericType = Type.GetType(frame.EnclosedPayloadType)
                ?? throw new Exception($"ExtractFramePayload: Unknown payload type {frame.EnclosedPayloadType}.");

            var toObjectMethod = typeof(Utility).GetMethod("JsonDeserializeToObject")
                ?? throw new Exception($"ExtractFramePayload: Could not find JsonDeserializeToObject().");

            genericToObjectMethod = toObjectMethod.MakeGenericMethod(genericType);

            _reflectioncache.Use((o) => o.TryAdd(frame.EnclosedPayloadType, genericToObjectMethod));

            return (IFramePayload?)genericToObjectMethod.Invoke(null, new object[] { frame.Payload })
                ?? throw new Exception($"ExtractFramePayload: Payload can not be null.");
        }
    }
}
