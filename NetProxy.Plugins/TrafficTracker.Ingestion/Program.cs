using Microsoft.Extensions.Options;
using NetProxy.TrafficTracker.Ingestion.Commands;
using NetProxy.TrafficTracker.Ingestion.Database;
using NetProxy.TrafficTracker.Ingestion.Messaging;
using NetProxy.TrafficTracker.Ingestion.Retention;
using Serilog;

namespace NetProxy.TrafficTracker.Ingestion
{
    internal class Program
    {
        public static async Task<int> Main(string[] args)
        {
            //A service starts in the system folder (C:\Windows\System32), where the relative paths of the settings (the log folder) would
            //  be. They are relative to the program instead.
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);

            try
            {
                var host = CreateHost(args);

                if (string.IsNullOrWhiteSpace(host.Services.GetRequiredService<IOptions<IngestionOptions>>().Value.ConnectionString))
                {
                    Log.Fatal("The connection string ConnectionStrings:Traffic is not set (appsettings.json, or the environment variable ConnectionStrings__Traffic).");
                    return 1;
                }

                await host.RunAsync();
                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "The ingestion service stopped because it could not start.");
                return 1;
            }
            finally
            {
                await Log.CloseAndFlushAsync();
            }
        }

        private static IHost CreateHost(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            builder.Services.AddSerilog((services, configuration) => configuration
                .ReadFrom.Configuration(builder.Configuration)
                .Enrich.FromLogContext());

            //A Windows service or a systemd service when it is run as one, and an ordinary program (a console) when it is not.
            builder.Services.AddWindowsService(o => o.ServiceName = "NetProxyTrafficIngestion");
            builder.Services.AddSystemd();

            //Stopping the service lets the batch that is being written finish, and does not wait for ever for it.
            builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(30));

            builder.Services.AddOptions<IngestionOptions>().Configure(options => ReadOptions(builder.Configuration, options));

            builder.Services.AddSingleton<DatabaseMigrator>();
            builder.Services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
            builder.Services.AddSingleton<ICommandHandler<IngestWebRequests>, IngestWebRequestsHandler>();
            builder.Services.AddSingleton<ICommandHandler<PurgeOldRequests>, PurgeOldRequestsHandler>();

            builder.Services.AddHostedService<TrafficSubscriber>();
            builder.Services.AddHostedService<RetentionService>();

            return builder.Build();
        }

        private static void ReadOptions(IConfiguration configuration, IngestionOptions options)
        {
            options.ConnectionString = configuration.GetConnectionString("Traffic") ?? string.Empty;
            options.MqHost = configuration["Mq:Host"] ?? options.MqHost;
            options.MqPort = int.TryParse(configuration["Mq:Port"], out var port) ? port : options.MqPort;
            options.Queue = configuration["Mq:Queue"] ?? options.Queue;
            options.CreateDatabaseIfMissing = bool.TryParse(configuration["Ingestion:CreateDatabaseIfMissing"], out var create) && create;
            options.RetentionDays = int.TryParse(configuration["Ingestion:RetentionDays"], out var days) ? days : 0;
            options.PurgeBatchSize = int.TryParse(configuration["Ingestion:PurgeBatchSize"], out var batch) ? batch : options.PurgeBatchSize;
        }
    }
}
