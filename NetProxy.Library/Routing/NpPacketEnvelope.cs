using ProtoBuf;

namespace NetProxy.Library.Routing
{
    [Serializable]
    [ProtoContract]
    public class NpPacketEnvelope
    {
        [ProtoMember(1)]
        public DateTime CreatedTime = DateTime.UtcNow;

        [ProtoMember(2)]
        public string? Label { get; set; }

        [ProtoMember(3)]
        public byte[]? Payload { get; set; }
    }
}