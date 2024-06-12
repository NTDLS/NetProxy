namespace NetProxy.Library.Payloads.Routing
{
    public class NpUser
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
