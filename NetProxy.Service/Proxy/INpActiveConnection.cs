using NetProxy.Library.Payloads;

namespace NetProxy.Service.Proxy
{
    /// <summary>
    /// A connection of a proxy that is open: a TCP connection, or the UDP session of a client. This is what the console lists, what is
    /// closed when a connection is closed by hand, and what a stopping proxy waits for.
    /// </summary>
    internal interface INpActiveConnection
    {
        Guid Id { get; }

        /// <summary>
        /// Completes when the connection is over and its sockets are closed.
        /// </summary>
        Task Completion { get; }

        NpConnectionGridItem GetGridItem();

        /// <summary>
        /// Ends the connection. Whatever is waiting on one of its sockets is released.
        /// </summary>
        void Abort();
    }
}
