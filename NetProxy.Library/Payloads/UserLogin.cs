namespace NetProxy.Library.Payloads
{
    public class UserLogin
    {
        public string UserName { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
    }
}
