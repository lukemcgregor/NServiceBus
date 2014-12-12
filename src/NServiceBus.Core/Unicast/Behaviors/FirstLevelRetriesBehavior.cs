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
        TransactionSettings TransactionSettings;
        readonly HostInformation hostInformation;
        readonly IManageMessageFailures FailureManager;

        public FirstLevelRetriesBehavior(TransactionSettings settings, HostInformation hostInformation, IManageMessageFailures manageMessageFailures)
        {
            TransactionSettings = settings;
            this.hostInformation = hostInformation;
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
            message.Headers[Headers.HostId] = hostInformation.HostId.ToString("N");
            message.Headers[Headers.HostDisplayName] = hostInformation.DisplayName;

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