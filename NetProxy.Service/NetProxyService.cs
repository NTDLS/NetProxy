namespace NetProxy.Service
{
    internal class NetProxyService
    {
        private readonly SemaphoreSlim _semaphoreToRequestStop;
        private readonly Thread _thread;

        private readonly Management _management;

        public NetProxyService()
        {
            _management = new Management();
            _semaphoreToRequestStop = new SemaphoreSlim(0);
            _thread = new Thread(DoWork);
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

        private void DoWork()
        {
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
