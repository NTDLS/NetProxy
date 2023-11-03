namespace NetProxy.Service.Routing
{
    public class RouterStatistics
    {
        public ulong TotalConnections = 0;
        public ulong BytesSent = 0;
        public ulong BytesReceived = 0;
        public ulong PacketMalformedCount = 0;
        public ulong PacketSizeExceededCount = 0;
        public ulong PacketFragmentCount = 0;
        public ulong PacketCrcFailureCount = 0;
        public ulong DroppedPreNegotiatePacket = 0;
        public ulong DroppedPreNegotiateRawData = 0;
    }
}
