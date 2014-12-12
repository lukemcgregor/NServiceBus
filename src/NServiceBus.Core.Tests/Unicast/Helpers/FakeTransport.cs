namespace NServiceBus.Unicast.Tests.Helpers
{
    using System;
    using NServiceBus.Faults;
    using NServiceBus.Settings;
    using NServiceBus.Transports;
    using Transport;
    using TransactionSettings = NServiceBus.Unicast.Transport.TransactionSettings;

    public class FakeTransport : TransportReceiver
    {
        public FakeTransport(TransactionSettings transactionSettings, int maximumConcurrencyLevel, int maximumThroughput, IDequeueMessages receiver, IManageMessageFailures manageMessageFailures, ReadOnlySettings settings, Configure config) : base(transactionSettings, maximumConcurrencyLevel, receiver, manageMessageFailures, settings, config, null)
        {
        }

        public override void Start(Address localAddress)
        {
        }

        public override int MaximumConcurrencyLevel
        {
            get { return 1; }
        }

        public override void ChangeMaximumConcurrencyLevel(int maximumConcurrencyLevel)
        {
            
        }

        public void AbortHandlingCurrentMessage()
        {
           
        }

        public override void Stop()
        {
        }

        public void ChangeMaximumMessageThroughputPerSecond(int maximumMessageThroughputPerSecond)
        {
            throw new NotImplementedException();
        }

        
        public void FakeMessageBeingProcessed(TransportMessage transportMessage)
        {
        }

        public void FakeMessageBeingPassedToTheFaultManager(TransportMessage transportMessage)
        {
        }
        public int MaximumMessageThroughputPerSecond { get; private set; }
    }
}