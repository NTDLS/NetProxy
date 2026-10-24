using Xunit;

namespace NetProxy.Tests.Support
{
    /// <summary>
    /// The tests that use the plugin manager of the service or its configuration files. There is one of each in a process, so these
    /// tests run one at a time.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class ServiceCollection
    {
        public const string Name = "Service";
    }
}
