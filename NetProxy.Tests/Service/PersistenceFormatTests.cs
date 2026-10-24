using NetProxy.Library;
using NetProxy.Library.Payloads;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Library.Utilities;
using NetProxy.Service;
using NetProxy.Tests.Support;
using Newtonsoft.Json;
using NTDLS.Persistence;
using Xunit;

namespace NetProxy.Tests.Service
{
    /// <summary>
    /// The configuration files are written and read by NTDLS.Persistence, which is not the serializer of the management connection.
    /// What a person types into the files, and what the documentation says, has to be what it reads and writes: names, not numbers.
    /// </summary>
    [Collection(ServiceCollection.Name)]
    public class PersistenceFormatTests : IDisposable
    {
        public PersistenceFormatTests() => TestEnvironment.ResetConfiguration();

        public void Dispose() => TestEnvironment.ResetConfiguration();

        private static string FileText(string namePart)
            => File.ReadAllText(Directory.GetFiles(TestEnvironment.ConfigurationFolder, $"*{namePart}*").Single());

        [Fact]
        public void The_proxy_file_is_written_with_names_for_every_enum_and_reads_back_the_same()
        {
            var proxy = ProxyBuilder.Configuration("p", 443, TrafficType.Https, 9000, c =>
            {
                c.BindingProtocol = BindingProtocol.Both;
                c.ForwardedHeaders = ForwardedHeadersMode.Append;
                c.Endpoints.ConnectionPattern = ConnectionPattern.Balanced;
                c.HttpHeaderRules.Add(new NpHttpHeaderRule
                {
                    Name = "X-A", Value = "1", Action = HttpHeaderAction.Upsert, HeaderType = HttpHeaderType.Request, Verb = HttpVerb.Post, Enabled = true
                });
            });

            CommonApplicationData.SaveToDisk(TestEnvironment.ConfigurationName, new List<NpProxyConfiguration> { proxy });
            var text = FileText("NpProxyConfiguration");

            foreach (var expected in new[]
            {
                "\"TrafficType\": \"Https\"", "\"TrafficType\": \"Http\"", "\"BindingProtocol\": \"Both\"", "\"ForwardedHeaders\": \"Append\"",
                "\"ConnectionPattern\": \"Balanced\"", "\"Action\": \"Upsert\"", "\"HeaderType\": \"Request\"", "\"Verb\": \"Post\""
            })
            {
                Assert.Contains(expected, text);
            }

            var loaded = CommonApplicationData.LoadFromDisk(TestEnvironment.ConfigurationName, new List<NpProxyConfiguration>());
            Assert.Equal(JsonConvert.SerializeObject(proxy), JsonConvert.SerializeObject(loaded.Single()));
        }

        [Fact]
        public void A_proxy_file_written_by_hand_with_names_loads()
        {
            Directory.CreateDirectory(TestEnvironment.ConfigurationFolder);
            File.WriteAllText(Path.Combine(TestEnvironment.ConfigurationFolder, "NpProxyConfiguration.json"), """
                [
                  {
                    "Name": "by-hand", "TrafficType": "Udp", "ListenPort": 53, "BindingProtocol": "IPv6", "ForwardedHeaders": "Off",
                    "Endpoints": { "ConnectionPattern": "FailOver", "Collection": [ { "Address": "10.0.0.1", "Port": 53, "TrafficType": "Tcp" } ] }
                  }
                ]
                """);

            var loaded = CommonApplicationData.LoadFromDisk(TestEnvironment.ConfigurationName, new List<NpProxyConfiguration>()).Single();

            Assert.Equal(TrafficType.Udp, loaded.TrafficType);
            Assert.Equal(BindingProtocol.IPv6, loaded.BindingProtocol);
            Assert.Equal(ConnectionPattern.FailOver, loaded.Endpoints.ConnectionPattern);
            Assert.Equal(TrafficType.Tcp, loaded.Endpoints.Collection[0].TrafficType);
            Assert.True(loaded.Endpoints.Collection[0].Enabled); //Switched on unless the file says otherwise.
        }

        [Fact]
        public void The_service_file_has_the_log_level_as_a_name()
        {
            var configuration = new NpConfiguration { ManagementPort = 5854, Settings = new NpServiceSettings { LogLevel = NpLogging.Severity.Warning } };
            configuration.Users.Add(new NpUser { UserName = "a", PasswordHash = "h" });

            CommonApplicationData.SaveToDisk(TestEnvironment.ConfigurationName, configuration);

            Assert.Contains("\"LogLevel\": \"Warning\"", FileText("NpConfiguration"));
            var loaded = CommonApplicationData.LoadFromDisk(TestEnvironment.ConfigurationName, new NpConfiguration());
            Assert.Equal(NpLogging.Severity.Warning, loaded.Settings.LogLevel);
        }

        [Fact]
        public void The_wire_format_of_the_management_connection_uses_names_too()
        {
            var json = JsonConvert.SerializeObject(ProxyBuilder.Configuration("p", 53, TrafficType.Udp, 9000));

            Assert.Contains("\"TrafficType\":\"Udp\"", json);
            Assert.Contains("\"BindingProtocol\":\"IPv4\"", json);
        }
    }
}
