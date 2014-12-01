using System;

namespace VBCBBot
{
    public class MessageUpdatedEventArgs : EventArgs
    {
        public MessageToDistribute Message { get; set; }
    }
}
