namespace NetProxy.Hub.Common
{
    [Serializable]
    public class Packet
    {
        public DateTime CreatedTime = DateTime.Now;
        public string Label { get; set; }
        public string Payload { get; set; }
    }
}
