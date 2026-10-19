using Topshelf;

namespace NetProxy.Service
{
    static class Program
    {
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    Singletons.Logging.Write("An unhandled exception occurred", ex);
                }
                if (e.IsTerminating)
                {
                    Singletons.ShutdownLogging();
                }
            };

            HostFactory.Run(x =>
            {
                x.StartAutomatically();

                x.EnableServiceRecovery(rc =>
                {
                    rc.RestartService(1);
                });

                x.Service<NpService>(s =>
                {
                    s.ConstructUsing(hostSettings => new NpService());
                    s.WhenStarted(tc => tc.Start());
                    s.WhenStopped(tc => tc.Stop());
                });
                x.RunAsLocalSystem();

                x.SetDescription("Provides TCP/IP v4/v6 routing, proxying, load-balancing, fail-over and shaping services.");
                x.SetDisplayName("NetworkDLS NetProxy");
                x.SetServiceName("NtNetProxy");
            });
        }
    }
}
