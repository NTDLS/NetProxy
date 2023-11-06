using NetProxy.MessageHub.MessageFraming.Payloads;
using NetProxy.Service.Proxy;
using System.Net;
using System.Net.Sockets;

namespace NetProxy.Hub
{
    public class HubClient : IHub
    {
        public event NotificationReceivedEvent? OnNotificationReceived;
        public event QueryReceivedEvent? OnQueryReceived;
        public event DisconnectedEvent? OnDisconnected;

        public delegate void DisconnectedEvent(Guid connectionId);
        public delegate void NotificationReceivedEvent(Guid connectionId, IFramePayloadNotification payload);
        public delegate IFramePayloadReply QueryReceivedEvent(Guid connectionId, IFramePayloadQuery payload);

        private readonly TcpClient _client = new();
        private HubConnection? _activeConnection;
        public bool _keepRunning;

        public void Connect(string hostName, int port)
        {
            if (_keepRunning)
            {
                return;
            }
            _keepRunning = true;

            _client.Connect(hostName, port);
            _activeConnection = new HubConnection(this, _client);
            _activeConnection.RunAsync();
        }

        public void Connect(IPAddress ipAddress, int port)
        {
            if (_keepRunning)
            {
                return;
            }
            _keepRunning = true;

            _client.Connect(ipAddress, port);
            _activeConnection = new HubConnection(this, _client);
            _activeConnection.RunAsync();
        }

        public void Disconnect()
        {
            _keepRunning = false;
        }

        public void SendNotification(IFramePayloadNotification notification)
        {
            Utility.EnsureNotNull(_activeConnection);
            _activeConnection.SendNotification(notification);
        }

        public async Task<T?> SendQuery<T>(IFramePayloadQuery query) where T: IFramePayloadReply
        {
            Utility.EnsureNotNull(_activeConnection);
            return await _activeConnection.SendQuery<T>(query);
        }

        public void InvokeOnDisconnected(Guid connectionId)
        {
            _activeConnection = null;
            OnDisconnected?.Invoke(connectionId);
        }

        public void InvokeOnNotificationReceived(Guid connectionId, IFramePayloadNotification payload)
        {
            if (OnNotificationReceived == null)
            {
                throw new Exception("The notification hander event was not handled.");
            }
            OnNotificationReceived.Invoke(connectionId, payload);
        }

        public IFramePayloadReply InvokeOnQueryReceived(Guid connectionId, IFramePayloadQuery payload)
        {
            if (OnQueryReceived == null)
            {
                throw new Exception("The query hander event was not handled.");
            }
            return OnQueryReceived.Invoke(connectionId, payload);
        }
    }
}
