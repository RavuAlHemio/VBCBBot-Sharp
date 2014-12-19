namespace Messenger.ORM
{
    public static class MessageUtils
    {
        public static T CopyMessage<T>(object fromMessage, T toMessage)
        {
            var whats = new []
            {
                "ID",
                "Timestamp",
                "SenderOriginal",
                "RecipientFolded",
                "Body"
            };

            foreach (var what in whats)
            {
                var fromProp = fromMessage.GetType().GetProperty(what);
                var toProp = toMessage.GetType().GetProperty(what);

                toProp.SetValue(toMessage, fromProp.GetValue(fromMessage));
            }

            return toMessage;
        }
    }
}
