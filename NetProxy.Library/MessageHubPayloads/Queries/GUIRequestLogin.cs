﻿using NTDLS.StreamFraming.Payloads;

namespace NetProxy.Library.MessageHubPayloads.Queries
{
    public class GUIRequestLogin : IFramePayloadQuery
    {
        public string UserName { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;

        public GUIRequestLogin(string userName, string passwordHash)
        {
            UserName = userName;
            PasswordHash = passwordHash;
        }
    }

    public class GUIRequestLoginReply : IFramePayloadQueryReply
    {
        public bool Result { get; set; }

        public GUIRequestLoginReply(bool result)
        {
            Result = result;
        }
    }
}
