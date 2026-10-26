namespace NetProxy.Service.Proxy
{
    /// <summary>
    /// Counters for one endpoint of a proxy, from the point of view of the proxy.
    /// </summary>
    public class NpProxyEndpointStatistics
    {
        private long _totalConnections;
        private int _currentConnections;
        private long _bytesRead;
        private long _bytesWritten;
        private long _lastFailureTicks;

        public ulong TotalConnections => (ulong)Interlocked.Read(ref _totalConnections);
        public int CurrentConnections => Volatile.Read(ref _currentConnections);

        /// <summary>
        /// Bytes received from the endpoint.
        /// </summary>
        public ulong BytesRead => (ulong)Interlocked.Read(ref _bytesRead);

        /// <summary>
        /// Bytes sent to the endpoint.
        /// </summary>
        public ulong BytesWritten => (ulong)Interlocked.Read(ref _bytesWritten);

        /// <summary>
        /// When the proxy last failed to connect to the endpoint (UTC), or null if it never has, or has succeeded since.
        /// </summary>
        public DateTime? LastFailure
        {
            get
            {
                var ticks = Interlocked.Read(ref _lastFailureTicks);
                return ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
            }
        }

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

        /// <summary>
        /// Counts a connection against the endpoint from the moment it is chosen, so that connections that arrive together
        /// are spread out, rather than all sent to the endpoint that had the fewest when they arrived.
        /// </summary>
        internal void Reserve() => Interlocked.Increment(ref _currentConnections);

        /// <summary>
        /// The reserved connection could not be made.
        /// </summary>
        internal void ReleaseFailed()
        {
            Interlocked.Decrement(ref _currentConnections);
            Interlocked.Exchange(ref _lastFailureTicks, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// The reserved connection was made.
        /// </summary>
        internal void Established()
        {
            Interlocked.Increment(ref _totalConnections);
            Interlocked.Exchange(ref _lastFailureTicks, 0);
        }

        /// <summary>
        /// The endpoint refused or did not answer an established connection (UDP: an ICMP port unreachable came back), so that new
        /// connections are sent to the other endpoints first for a while.
        /// </summary>
        internal void MarkFailed() => Interlocked.Exchange(ref _lastFailureTicks, DateTime.UtcNow.Ticks);

        internal void ConnectionClosed() => Interlocked.Decrement(ref _currentConnections);
        internal void AddBytesRead(long bytes) => Interlocked.Add(ref _bytesRead, bytes);
        internal void AddBytesWritten(long bytes) => Interlocked.Add(ref _bytesWritten, bytes);
    }
}
