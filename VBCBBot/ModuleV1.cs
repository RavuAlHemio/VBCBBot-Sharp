using System;
using System.Data.Entity.Validation;
using log4net;

namespace VBCBBot
{
    /// <summary>
    /// A module that can be loaded into the bot.
    /// </summary>
    public abstract class ModuleV1 : IDisposable
    {
        private static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// The chatbox connector relevant to this instance of the module.
        /// </summary>
        protected ChatboxConnector Connector;

        public ModuleV1(ChatboxConnector connector)
        {
            Connector = connector;
            Connector.MessageUpdated += MessageUpdated;
        }

        private void MessageUpdated(object sender, MessageUpdatedEventArgs e)
        {
            try
            {
                ProcessUpdatedMessage(e.Message.Message, e.Message.IsPartOfInitialSalvo, e.Message.IsEdited,
                    e.Message.IsBanned);
            }
            catch (DbEntityValidationException exc)
            {
                // special-case this for better error messages
                Logger.ErrorFormat("module {0} failed to process updated message due to validation error:\n{1}", this.GetType().Name, exc);
                foreach (var eve in exc.EntityValidationErrors)
                {
                    Logger.InfoFormat("-- entity of type {0} in state {1}", eve.Entry.Entity.GetType().Name, eve.Entry.State);
                    foreach (var ve in eve.ValidationErrors)
                    {
                        Logger.InfoFormat("---- property {0} error {1}", ve.PropertyName, ve.ErrorMessage);
                    }
                }
            }
            catch (Exception exc)
            {
                Logger.ErrorFormat("module {0} failed to process updated message:\n{1}", this.GetType().Name, exc);
            }
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
