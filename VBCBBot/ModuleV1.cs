using System;

namespace VBCBBot
{
    /// <summary>
    /// A module that can be loaded into the bot.
    /// </summary>
    public abstract class ModuleV1 : IDisposable
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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Called when the object is being disposed.
        /// </summary>
        /// <param name="disposing">If <c>true</c>, this method is being called because <see cref="Dispose()"/>
        /// was called. If <c>false</c>, this method is being called because the finalizer was called.</param>
        protected virtual void Dispose(bool disposing)
        {
            // subclasses might find this more interesting...
        }
    }
}
