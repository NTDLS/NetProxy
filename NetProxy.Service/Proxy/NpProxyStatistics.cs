namespace NetProxy.Service.Proxy
{
    /// <summary>
    /// Counters for a proxy, from the point of view of its clients. They are updated by many connections at once.
    /// </summary>
    public class NpProxyStatistics
    {
        private long _totalConnections;
        private int _currentConnections;
        private long _bytesRead;
        private long _bytesWritten;

        public ulong TotalConnections => (ulong)Interlocked.Read(ref _totalConnections);
        public int CurrentConnections => Volatile.Read(ref _currentConnections);

        /// <summary>
        /// Bytes received from clients.
        /// </summary>
        public ulong BytesRead => (ulong)Interlocked.Read(ref _bytesRead);

        /// <summary>
        /// Bytes sent to clients.
        /// </summary>
        public ulong BytesWritten => (ulong)Interlocked.Read(ref _bytesWritten);

        /// <summary>
        /// Takes the numbers that were kept from before.
        /// </summary>
        internal void Restore(ulong totalConnections, ulong bytesRead, ulong bytesWritten)
        {
            Interlocked.Exchange(ref _totalConnections, (long)totalConnections);
            Interlocked.Exchange(ref _bytesRead, (long)bytesRead);
            Interlocked.Exchange(ref _bytesWritten, (long)bytesWritten);
        }

        /// <summary>
        /// Sets what was counted back to zero. The connections that are open now still are.
        /// </summary>
        internal void Reset() => Restore(0, 0, 0);

        internal void ConnectionOpened()
        {
            Interlocked.Increment(ref _totalConnections);
            Interlocked.Increment(ref _currentConnections);
        }

        internal void ConnectionClosed() => Interlocked.Decrement(ref _currentConnections);
        internal void AddBytesRead(long bytes) => Interlocked.Add(ref _bytesRead, bytes);
        internal void AddBytesWritten(long bytes) => Interlocked.Add(ref _bytesWritten, bytes);
    }
}
