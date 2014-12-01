using System;

namespace VBCBBot
{
    public struct MessageToDistribute
    {
        public bool IsPartOfInitialSalvo;
        public bool IsEdited;
        public bool IsBanned;
        public ChatboxMessage Message;

        public MessageToDistribute(bool isPartOfInitialSalvo, bool isEdited, bool isBanned, ChatboxMessage message)
        {
            IsPartOfInitialSalvo = isPartOfInitialSalvo;
            IsEdited = isEdited;
            IsBanned = isBanned;
            Message = message;
        }
    }
}

