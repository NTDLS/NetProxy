using ProtoBuf;

namespace NetProxy.Library.Routing
{
    [Serializable]
    [ProtoContract]
    public class PacketEnvelope
    {
        [ProtoMember(1)]
        public DateTime CreatedTime = DateTime.UtcNow;

        [ProtoMember(2)]
        public string Label { get; set; } = string.Empty;

        [ProtoMember(3)]
        public byte[] Payload { get; set; } = new byte[0];
    }
}