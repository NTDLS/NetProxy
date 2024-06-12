namespace NetProxy.Service
{
    internal class NpService
    {
        private readonly SemaphoreSlim _semaphoreToRequestStop;
        private readonly Thread _thread;

        private readonly NpServiceManager _management;

        public NpService()
        {
            _management = new NpServiceManager();
            _semaphoreToRequestStop = new SemaphoreSlim(0);
            _thread = new Thread(ServiceThreadProc);
        }

        public void Start()
        {
            _thread.Start();
        }

        public void Stop()
        {
            _management.Stop();
            _semaphoreToRequestStop.Release();
            _thread.Join();
        }

        private void ServiceThreadProc()
        {
            Thread.CurrentThread.Name = $"ServiceThreadProc:{Environment.CurrentManagedThreadId}";

            _management.Start();

            while (true)
            {
                if (_semaphoreToRequestStop.Wait(500))
                {
                    break;
                }
            }
        }
    }
}
