namespace NServiceBus.Unicast.Behaviors
{
    using System;
    using System.Runtime.Serialization;
    using NServiceBus.Faults;
    using NServiceBus.Hosting;
    using NServiceBus.Logging;
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;
    using NServiceBus.Unicast.Transport;

    class FirstLevelRetriesBehavior : IBehavior<IncomingContext>
    {
        FirstLevelRetries firstLevelRetries;
        ILog Logger = LogManager.GetLogger<FirstLevelRetriesBehavior>();
        public TransactionSettings TransactionSettings { get; set; }
        public HostInformation HostInformation { get; set; }
        readonly IManageMessageFailures FailureManager;

        public FirstLevelRetriesBehavior(IManageMessageFailures manageMessageFailures)
        {
            FailureManager = manageMessageFailures;
        }

        public void Invoke(IncomingContext context, Action next)
        {
            firstLevelRetries = context.Get<FirstLevelRetries>();
            ProcessMessage(context.PhysicalMessage, next);
        }

        bool ShouldExitBecauseOfRetries(TransportMessage message)
        {
            if (TransactionSettings.IsTransactional)
            {
                if (firstLevelRetries.HasMaxRetriesForMessageBeenReached(message))
                {
                    return true;
                }
            }
            return false;
        }

        void ProcessMessage(TransportMessage message, Action next)
        {
            message.Headers[Headers.HostId] = HostInformation.HostId.ToString("N");
            message.Headers[Headers.HostDisplayName] = HostInformation.DisplayName;

            if (string.IsNullOrWhiteSpace(message.Id))
            {
                Logger.Error("Message without message id detected");

                FailureManager.SerializationFailedForMessage(message,
                    new SerializationException("Message without message id received."));

                return;
            }

            if (ShouldExitBecauseOfRetries(message))
            {
                return;
            }

            try
            {
                next();
            }
            catch (MessageDeserializationException serializationException)
            {
                Logger.Error("Failed to deserialize message with ID: " + message.Id, serializationException);

                message.RevertToOriginalBodyIfNeeded();

                FailureManager.SerializationFailedForMessage(message, serializationException);
            }
        }
    }
}