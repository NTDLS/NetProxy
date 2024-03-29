﻿using NTDLS.StreamFraming.Payloads;

namespace NetProxy.Library.MessageHubPayloads.Queries
{
    public class QueryLogin : IFrameQuery
    {
        public string UserName { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;

        public QueryLogin(string userName, string passwordHash)
        {
            UserName = userName;
            PasswordHash = passwordHash;
        }
    }

    public class QueryLoginReply : IFrameQueryReply
    {
        public bool Result { get; set; } = false;
        public string? Message { get; set; }
    }
}
