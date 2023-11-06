namespace NetProxy.Library.Payloads
{
    [Serializable]
    public class NpUserLogin
    {
        public string UserName { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
    }
}
