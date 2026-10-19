namespace NetProxy.Service.Http
{
    /// <summary>
    /// Follows the HTTP traffic of one direction of a proxied connection and applies the header rules to it.
    /// </summary>
    internal interface IHttpStreamProcessor
    {
        /// <summary>
        /// Processes bytes received from one side and returns the bytes that should be sent to the other.
        /// The returned length may be zero when the bytes are being held while waiting on the rest of a message.
        /// </summary>
        (byte[] Bytes, int Length) Process(byte[] bytes, int length);
    }
}
