using NTDLS.NullExtensions;
using NTDLS.ReliableMessaging;

namespace NetProxy.Service
{
    internal class NpServiceManagerMessageHandlerBase
    {
        public NpServiceManager EnforceLoginAndGetServiceManager(RmContext context)
        {
            var serviceManager = (context.Endpoint.Parameter as NpServiceManager).EnsureNotNull();
            if (serviceManager.AuthenticatedConnections.Contains(context.ConnectionId) == false)
            {
                throw new Exception("Login has not been completed.");
            }
            return serviceManager;
        }

    }
}