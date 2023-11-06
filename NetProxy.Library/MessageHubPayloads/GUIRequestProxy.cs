﻿using NetProxy.MessageHub.MessageFraming.Payloads;

namespace NetProxy.Library.MessageHubPayloads
{
    public class GUIRequestProxy : IFramePayloadNotification
    {
        public Guid Id { get; set; }

        public GUIRequestProxy(Guid id)
        {
            Id = id;
        }
    }
}
