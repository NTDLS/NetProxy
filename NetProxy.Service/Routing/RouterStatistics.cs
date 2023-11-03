namespace NetProxy.Service.Routing
{
    public class RouterStatistics
    {
        public UInt64 TotalConnections = 0;
        public UInt64 BytesSent = 0;
        public UInt64 BytesReceived = 0;
        public UInt64 PacketMalformedCount = 0;
        public UInt64 PacketSizeExceededCount = 0;
        public UInt64 PacketFragmentCount = 0;
        public UInt64 PacketCrcFailureCount = 0;
        public UInt64 DroppedPreNegotiatePacket = 0;
        public UInt64 DroppedPreNegotiateRawData = 0;
    }
}
