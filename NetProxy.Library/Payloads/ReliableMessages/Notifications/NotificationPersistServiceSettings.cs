using NTDLS.ReliableMessaging;

namespace NetProxy.Library.Payloads.ReliableMessages.Notifications
{
    public class NotificationPersistServiceSettings(NpServiceSettings settings) : IRmNotification
    {
        public NpServiceSettings Settings { get; set; } = settings;
    }
}
