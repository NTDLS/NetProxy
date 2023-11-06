﻿namespace NetProxy.Service
{
    internal class NpService
    {
        private readonly SemaphoreSlim _semaphoreToRequestStop;
        private readonly Thread _thread;

        private readonly NpManagement _management;

        public NpService()
        {
            _management = new NpManagement();
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
            Thread.CurrentThread.Name = $"ServiceThreadProc:{Thread.CurrentThread.ManagedThreadId}";

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