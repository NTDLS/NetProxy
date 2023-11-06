using NetProxy.Hub;
using NetProxy.Hub.MessageFraming;
using NetProxy.MessageHub.MessageFraming.Payloads;
using System.Net.Sockets;

namespace NetProxy.Service.Proxy
{
    internal class HubConnection
    {
        private readonly FrameBuffer _frameBuffer = new(4096);
        private readonly TcpClient _tcpclient; //The TCP/IP connection associated with this connection.
        private readonly Thread _dataPumpThread; //The thread that receives data for this connection.
        private readonly NetworkStream _stream; //The stream for the TCP/IP connection (used for reading and writing).
        private readonly IHub _hub;
        private bool _keepRunning;

        public Guid Id { get; private set; }

        public HubConnection(IHub hub, TcpClient tcpClient)
        {
            Id = Guid.NewGuid();
            _hub = hub;
            _tcpclient = tcpClient;
            _dataPumpThread = new Thread(DataPumpThreadProc);
            _keepRunning = true;
            _stream = tcpClient.GetStream();
        }

        public void SendNotification(IFramePayloadNotification notification)
            => _stream.SendNotificationFrame(notification);

        public Task<T> SendQuery<T>(IFramePayloadQuery query) where T : IFramePayloadReply
            => _stream.SendQueryFrame<T>(query);

        //public void SendQueryReply(Frame queryFrame, IFramePayloadReply reply)
        //    => _stream.SendReplyFrame(queryFrame, reply);

        /*
        public void Write(byte[] buffer)
        {
            LastActivityDateTime = DateTime.UtcNow;
            _stream.Write(buffer);
        }

        public void Write(byte[] buffer, int length)
        {
            LastActivityDateTime = DateTime.UtcNow;
            _stream.Write(buffer, 0, length);
        }

        public bool Read(ref byte[] buffer, out int outLength)
        {
            LastActivityDateTime = DateTime.UtcNow;
            outLength = _stream.Read(buffer, 0, buffer.Length);
            return outLength > 0;
        }
        */

        public void RunAsync()
        {
            _dataPumpThread.Start();
        }

        internal void DataPumpThreadProc()
        {
            Thread.CurrentThread.Name = $"DataPumpThreadProc:{Thread.CurrentThread.ManagedThreadId}";

            try
            {
                while (_keepRunning && _stream.ReceiveAndProcessStreamFrames(_frameBuffer,
                    (payload) => _hub.InvokeOnNotificationReceived(Id, payload),
                    (payload) => _hub.InvokeOnQueryReceived(Id, payload)))
                {
                }
            }
            catch
            {
                //TODO: log this.
            }

            _hub.InvokeOnDisconnected(Id);
        }

        public void Stop(bool waitOnThread)
        {
            _keepRunning = false;
            try { _stream.Close(); } catch { }
            try { _tcpclient.Close(); } catch { }

            if (waitOnThread)
            {
                _dataPumpThread.Join();
            }
        }
    }
}
