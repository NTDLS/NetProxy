using NetProxy.Library;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Service.Proxy;

namespace NetProxy.Tests.Support
{
    /// <summary>
    /// Keeps what a test starts, so that it is all stopped when the test is over.
    /// </summary>
    public sealed class TestHost : IDisposable
    {
        private readonly List<IDisposable> _disposables = [];
        private readonly List<NpProxy> _proxies = [];

        public TempDirectory Directory { get; } = new();

        public EchoBackend Backend(string name)
        {
            var backend = new EchoBackend(name);
            _disposables.Add(backend);
            return backend;
        }

        public NpProxy Start(NpProxyConfiguration configuration)
        {
            var proxy = ProxyBuilder.Start(configuration);
            _proxies.Add(proxy);
            return proxy;
        }

        /// <summary>
        /// A proxy that is not started, but is stopped with the others if the test starts it.
        /// </summary>
        public NpProxy Create(NpProxyConfiguration configuration)
        {
            var proxy = new NpProxy(configuration);
            _proxies.Add(proxy);
            return proxy;
        }

        public T Own<T>(T disposable) where T : IDisposable
        {
            _disposables.Add(disposable);
            return disposable;
        }

        public void StopProxies()
        {
            foreach (var proxy in _proxies)
            {
                try { proxy.Stop(); } catch { }
            }
        }

        public void Dispose()
        {
            StopProxies();

            foreach (var disposable in _disposables)
            {
                try { disposable.Dispose(); } catch { }
            }

            Directory.Dispose();
        }
    }
}
