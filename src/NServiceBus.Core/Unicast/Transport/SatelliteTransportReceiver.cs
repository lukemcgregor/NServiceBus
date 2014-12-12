namespace NServiceBus.Unicast.Transport
{
    using NServiceBus.Faults;
    using NServiceBus.Pipeline;
    using NServiceBus.Satellites;
    using NServiceBus.Settings;
    using NServiceBus.Transports;

    class SatelliteTransportReceiver : TransportReceiver
    {
        ISatellite satellite;

        /// <summary>
        /// Creates an instance of <see cref="TransportReceiver"/>
        /// </summary>
        /// <param name="transactionSettings">The transaction settings to use for this <see cref="TransportReceiver"/>.</param>
        /// <param name="maximumConcurrencyLevel">The maximum number of messages to process in parallel.</param>
        /// <param name="receiver">The <see cref="IDequeueMessages"/> instance to use.</param>
        /// <param name="manageMessageFailures">The <see cref="IManageMessageFailures"/> instance to use.</param>
        /// <param name="settings">The current settings</param>
        /// <param name="config">Configure instance</param>
        /// <param name="pipelineExecutor"></param>
        public SatelliteTransportReceiver(TransactionSettings transactionSettings, int maximumConcurrencyLevel, IDequeueMessages receiver, IManageMessageFailures manageMessageFailures, ReadOnlySettings settings, Configure config, PipelineExecutor pipelineExecutor)
            : base(transactionSettings, maximumConcurrencyLevel, receiver, manageMessageFailures, settings, config, pipelineExecutor)
        {
            MoreContext = c => c.Set(satellite);
        }

        public void SetSatellite(ISatellite satellite)
        {
            this.satellite = satellite;
        }
    }
}