using System;
using System.Configuration.Install;
using System.Diagnostics;
using System.Reflection;
using System.ServiceProcess;
using NetProxy.Library;
using NetProxy.Library.Win32;

namespace NetProxy.Service
{
    static class Program
    {
        static void Main(string[] args)
        {
            try
            {
                bool runInConsole = Debugger.IsAttached;

                if (System.Environment.UserInteractive && args.Length > 0 || runInConsole)
                {
                    foreach (string sensitiveArg in args)
                    {
                        string arg = sensitiveArg.ToLower();

                        switch (arg)
                        {
                            case "/console":
                                {
                                    runInConsole = true;
                                    break;
                                }

                            case "/debug":
                                {
                                    while (Debugger.IsAttached == false)
                                    {
                                        System.Threading.Thread.Sleep(500);
                                    }
                                    break;
                                }

                            case "/install":
                                {
                                    try
                                    {
                                        ManagedInstallerClass.InstallHelper(new string[] { Assembly.GetExecutingAssembly().Location });
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine(ex.Message);
                                    }
                                    break;

                                }

                            case "/uninstall":
                                {
                                    try
                                    {
                                        ManagedInstallerClass.InstallHelper(new string[] { "/u", Assembly.GetExecutingAssembly().Location });
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine(ex.Message);
                                    }
                                    break;
                                }

                            case "/start":
                                {
                                    try
                                    {
                                        ServiceController serviceController = new System.ServiceProcess.ServiceController(Constants.ServiceName);
                                        serviceController.Start();
                                        serviceController.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Running, new System.TimeSpan(0, 0, 0, 10)); //10 seconds.
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine(ex.Message);
                                    }
                                    break;
                                }

                            case "/stop":
                                {
                                    try
                                    {
                                        ServiceController serviceController = new System.ServiceProcess.ServiceController(Constants.ServiceName);
                                        serviceController.Stop();
                                        serviceController.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Stopped, new System.TimeSpan(0, 0, 10, 0)); //10 minutes - because this could take some time.
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine(ex.Message);
                                    }
                                    break;
                                }
                        }
                    }
                }
                else
                {
                    ServiceBase[] servicesToRun;
                    servicesToRun = new ServiceBase[]
                        {
                           new NetworkDLSNetProxyService()
                        };
                    ServiceBase.Run(servicesToRun);
                }

                if (runInConsole)
                {
                    RoutingServices routingServices = new RoutingServices();

                    routingServices.Start();

                    Console.WriteLine("Server running... press enter to close.");
                    Console.ReadLine();

                    routingServices.Stop();
                }
            }
            catch(Exception ex)
            {
                Singletons.EventLog.WriteEvent(new EventLogging.EventPayload
                {
                    Severity = EventLogging.Severity.Error,
                    CustomText = "Generic failure.",
                    Exception = ex
                });
            }
        }
    }
}
