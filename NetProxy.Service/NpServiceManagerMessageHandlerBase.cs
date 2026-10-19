using NTDLS.Helpers;
using NTDLS.ReliableMessaging;

namespace NetProxy.Service
{
    internal class NpServiceManagerMessageHandlerBase
    {
        public NpServiceManager GetServiceManager(RmContext context)
            => (context.Messenger.Parameter as NpServiceManager).EnsureNotNull();

        public NpServiceManager EnforceLoginAndGetServiceManager(RmContext context)
        {
            var serviceManager = GetServiceManager(context);
            if (serviceManager.IsAuthenticated(context.ConnectionId) == false)
            {
                throw new Exception("Login has not been completed.");
            }
            return serviceManager;
        }
    }
}
