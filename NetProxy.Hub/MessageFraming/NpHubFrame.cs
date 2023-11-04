using ProtoBuf;

namespace NetProxy.Hub.MessageFraming
{
    [Serializable]
    [ProtoContract]
    public class NpHubFrame
    {
        [ProtoMember(1)]
        public DateTime CreatedTime = DateTime.Now;

        [ProtoMember(2)]
        public string Label { get; set; } = string.Empty;

        [ProtoMember(3)]
        public string Payload { get; set; } = string.Empty;
    }
}
