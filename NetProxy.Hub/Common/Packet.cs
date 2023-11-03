using ProtoBuf;

namespace NetProxy.Hub.Common
{
    [Serializable]
    [ProtoContract]
    public class Packet
    {
        [ProtoMember(1)]
        public DateTime CreatedTime = DateTime.Now;

        [ProtoMember(2)]
        public string Label { get; set; } = string.Empty;

        [ProtoMember(3)]
        public string Payload { get; set; } = string.Empty;
    }
}
