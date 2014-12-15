namespace NServiceBus.Unicast.Transport
{
    using NServiceBus.Faults;
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;
    using NServiceBus.Settings;
    using NServiceBus.Transports;

    /// <summary>
    ///     Default implementation of a NServiceBus transport.
    /// </summary>
    public class MainTransportReceiver : TransportReceiver
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="transactionSettings"></param>
        /// <param name="maximumConcurrencyLevel"></param>
        /// <param name="maximumThroughput"></param>
        /// <param name="receiver"></param>
        /// <param name="manageMessageFailures"></param>
        /// <param name="settings"></param>
        /// <param name="config"></param>
        /// <param name="pipelineExecutor"></param>
        public MainTransportReceiver(TransactionSettings transactionSettings, int maximumConcurrencyLevel, int maximumThroughput, IDequeueMessages receiver, IManageMessageFailures manageMessageFailures, ReadOnlySettings settings, Configure config, PipelineExecutor pipelineExecutor)
            :base(transactionSettings, maximumConcurrencyLevel, receiver, manageMessageFailures, settings, config, pipelineExecutor)
        {
            
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void InnerStop()
        {
            if (throughputLimiter != null)
            {
                throughputLimiter.Stop();
                throughputLimiter.Dispose();

                throughputLimiter = null;
            }

            base.InnerStop();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        protected override void InvokePipeline(MessageDequeued value)
        {
            var context = new IncomingContext(pipelineExecutor.CurrentContext);
            context.Set(firstLevelRetries);
            context.Set(currentReceivePerformanceDiagnostics);
            context.Set(TransactionSettings);
            context.Set("TransportReceive.Address", receiveAddress);
            context.Set(throughputLimiter);

            pipelineExecutor.InvokeReceivePhysicalMessagePipeline(context);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="address"></param>
        public override void Start(Address address)
        {
            throughputLimiter = new ThroughputLimiter();

            throughputLimiter.Start(MaximumMessageThroughputPerSecond);

            if (MaximumMessageThroughputPerSecond > 0)
            {
                Logger.InfoFormat("Transport: {0} started with its throughput limited to {1} msg/sec", receiveAddress,
                    MaximumMessageThroughputPerSecond);
            }

            base.Start(address);
        }

        /// <summary>
        ///     Gets the receiving messages rate.
        /// </summary>
        public int MaximumMessageThroughputPerSecond { get; private set; }

        /// <summary>
        /// Updates the MaximumMessageThroughputPerSecond setting.
        /// </summary>
        /// <param name="maximumMessageThroughputPerSecond">The new value.</param>
        public void ChangeMaximumMessageThroughputPerSecond(int maximumMessageThroughputPerSecond)
        {
            if (maximumMessageThroughputPerSecond == MaximumMessageThroughputPerSecond)
            {
                return;
            }

            lock (changeMaximumMessageThroughputPerSecondLock)
            {
                MaximumMessageThroughputPerSecond = maximumMessageThroughputPerSecond;
                if (throughputLimiter != null)
                {
                    throughputLimiter.Stop();
                    throughputLimiter.Start(maximumMessageThroughputPerSecond);
                }
            }
            if (maximumMessageThroughputPerSecond <= 0)
            {
                Logger.InfoFormat("Throughput limit for {0} disabled.", receiveAddress);
            }
            else
            {
                Logger.InfoFormat("Throughput limit for {0} changed to {1} msg/sec", receiveAddress,
                    maximumMessageThroughputPerSecond);
            }
        }

        object changeMaximumMessageThroughputPerSecondLock = new object();

        ThroughputLimiter throughputLimiter;
    }
}