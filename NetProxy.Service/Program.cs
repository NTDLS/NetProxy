using Topshelf;

namespace NetProxy.Service
{
    static class Program
    {
        static void Main(string[] args)
        {
            HostFactory.Run(x =>
            {
                x.StartAutomatically();

                x.EnableServiceRecovery(rc =>
                {
                    rc.RestartService(1);
                });

                x.Service<NetProxyService>(s =>
                {
                    s.ConstructUsing(hostSettings => new NetProxyService());
                    s.WhenStarted(tc => tc.Start());
                    s.WhenStopped(tc => tc.Stop());
                });
                x.RunAsLocalSystem();

                x.SetDescription("Provides TCP/IP v4/v6/tunneling and routing services.");
                x.SetDisplayName("NetworkDLS NetProxy");
                x.SetServiceName("NtNetProxy");
            });
        }
    }
}
