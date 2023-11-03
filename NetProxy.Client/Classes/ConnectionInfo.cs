﻿namespace NetProxy.Client.Classes
{
    public class ConnectionInfo
    {
        public string ServerName { get; set; } = string.Empty;
        public int Port { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
