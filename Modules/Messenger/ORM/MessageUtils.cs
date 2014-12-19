namespace Messenger.ORM
{
    public static class MessageUtils
    {
        public static void CopyMessage(IMessage fromMessage, IMessage toMessage)
        {
            toMessage.ID = fromMessage.ID;
            toMessage.Timestamp = fromMessage.Timestamp;
            toMessage.SenderOriginal = fromMessage.SenderOriginal;
            toMessage.RecipientFolded = fromMessage.RecipientFolded;
            toMessage.Body = fromMessage.Body;
        }
    }
}
