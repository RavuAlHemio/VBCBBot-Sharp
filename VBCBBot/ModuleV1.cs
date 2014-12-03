using System;

namespace VBCBBot
{
    /// <summary>
    /// A module that can be loaded into the bot.
    /// </summary>
    public abstract class ModuleV1
    {
        /// <summary>
        /// The chatbox connector relevant to this instance of the module.
        /// </summary>
        protected ChatboxConnector Connector;

        public ModuleV1(ChatboxConnector connector)
        {
            Connector.MessageUpdated += MessageUpdated;
        }

        private void MessageUpdated(object sender, MessageUpdatedEventArgs e)
        {
            ProcessUpdatedMessage(e.Message.Message, e.Message.IsPartOfInitialSalvo, e.Message.IsEdited, e.Message.IsBanned);
        }

        protected abstract void ProcessUpdatedMessage(ChatboxMessage message, bool isPartOfInitialSalvo = false, bool isEdited = false, bool isBanned = false);
    }
}

