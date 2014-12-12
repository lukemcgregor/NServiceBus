namespace NServiceBus.Unicast.Transport
{
    using System;
    using Faults;
    using Logging;
    using Monitoring;
    using NServiceBus.Pipeline;
    using Settings;
    using Transports;

    /// <summary>
    ///     Default implementation of a NServiceBus transport.
    /// </summary>
    public abstract class TransportReceiver : IDisposable, IObserver<MessageDequeued>
    {
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
        protected TransportReceiver(TransactionSettings transactionSettings, int maximumConcurrencyLevel, IDequeueMessages receiver, IManageMessageFailures manageMessageFailures, ReadOnlySettings settings, Configure config, PipelineExecutor pipelineExecutor)
        {
            this.settings = settings;
            this.config = config;
            this.pipelineExecutor = pipelineExecutor;
            TransactionSettings = transactionSettings;
            MaximumConcurrencyLevel = maximumConcurrencyLevel;
            FailureManager = manageMessageFailures;
            Receiver = receiver;
        }

        internal BusNotifications Notifications { get; set; }

        /// <summary>
        ///     The receiver responsible for notifying the transport when new messages are available
        /// </summary>
        public IDequeueMessages Receiver { get; set; }

        /// <summary>
        ///     Manages failed message processing.
        /// </summary>
        public IManageMessageFailures FailureManager { get; set; }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        void IDisposable.Dispose()
        {
            //Injected at compile time
        }

        /// <summary>
        ///     Gets the maximum concurrency level this <see cref="TransportReceiver" /> is able to support.
        /// </summary>
        public virtual int MaximumConcurrencyLevel { get; private set; }

        /// <summary>
        ///     Updates the maximum concurrency level this <see cref="TransportReceiver" /> is able to support.
        /// </summary>
        /// <param name="maximumConcurrencyLevel">The new maximum concurrency level for this <see cref="TransportReceiver" />.</param>
        public virtual void ChangeMaximumConcurrencyLevel(int maximumConcurrencyLevel)
        {
            if (MaximumConcurrencyLevel == maximumConcurrencyLevel)
            {
                return;
            }

            MaximumConcurrencyLevel = maximumConcurrencyLevel;

            if (isStarted)
            {
                Receiver.Stop();
                Receiver.Start(maximumConcurrencyLevel);
                Logger.InfoFormat("Maximum concurrency level for '{0}' changed to {1}.", receiveAddress,
                    maximumConcurrencyLevel);
            }
        }

        

        /// <summary>
        /// Starts the transport listening for messages on the given local address.
        /// </summary>
        public virtual void Start(Address address)
        {
            if (isStarted)
            {
                throw new InvalidOperationException("The transport is already started");
            }

            receiveAddress = address;

            var returnAddressForFailures = address;

            var workerRunsOnThisEndpoint = settings.GetOrDefault<bool>("Worker.Enabled");

            if (workerRunsOnThisEndpoint
                && (returnAddressForFailures.Queue.ToLower().EndsWith(".worker") || address == config.LocalAddress))
            //this is a hack until we can refactor the SLR to be a feature. "Worker" is there to catch the local worker in the distributor
            {
                returnAddressForFailures = settings.Get<Address>("MasterNode.Address");

                Logger.InfoFormat("Worker started, failures will be redirected to {0}", returnAddressForFailures);
            }

            FailureManager.Init(returnAddressForFailures);

            firstLevelRetries = new FirstLevelRetries(TransactionSettings.MaxRetries, FailureManager, CriticalError, Notifications);

            InitializePerformanceCounters();

            StartReceiver();

            isStarted = true;
        }

        /// <summary>
        ///     Stops the transport.
        /// </summary>
        public virtual void Stop()
        {
            InnerStop();
        }

        void InitializePerformanceCounters()
        {
            currentReceivePerformanceDiagnostics = new ReceivePerformanceDiagnostics(receiveAddress);

            currentReceivePerformanceDiagnostics.Initialize();
        }

        void StartReceiver()
        {
            Receiver.Init(receiveAddress, TransactionSettings);
            Receiver.Subscribe(this);
            Receiver.Start(MaximumConcurrencyLevel);
        }

        /// <summary>
        /// 
        /// </summary>
        protected virtual void InnerStop()
        {
            if (!isStarted)
            {
                return;
            }

            Receiver.Stop();

            isStarted = false;
        }

        void DisposeManaged()
        {
            InnerStop();

            if (currentReceivePerformanceDiagnostics != null)
            {
                currentReceivePerformanceDiagnostics.Dispose();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        protected static ILog Logger = LogManager.GetLogger<TransportReceiver>();
        ReceivePerformanceDiagnostics currentReceivePerformanceDiagnostics;
        FirstLevelRetries firstLevelRetries;
        /// <summary>
        /// 
        /// </summary>
        protected bool isStarted;
        
        /// <summary>
        /// 
        /// </summary>
protected Address receiveAddress;
        readonly ReadOnlySettings settings;
        readonly Configure config;
        readonly PipelineExecutor pipelineExecutor;

        /// <summary>
        /// The <see cref="TransactionSettings"/> being used.
        /// </summary>
        public TransactionSettings TransactionSettings { get; private set; }

        internal CriticalError CriticalError { get; set; }

        /// <summary>
        /// 
        /// </summary>
        protected Action<BehaviorContext> MoreContext = context => { };

        void IObserver<MessageDequeued>.OnNext(MessageDequeued value)
        {
            //todo: I want to start a new instance of a pipeline and not use thread statics 

            var behaviorContext = pipelineExecutor.CurrentContext;
            behaviorContext.Set(firstLevelRetries);
            behaviorContext.Set(currentReceivePerformanceDiagnostics);
            behaviorContext.Set(TransactionSettings);
            behaviorContext.Set("TransportReceive.Address", receiveAddress);
            MoreContext(behaviorContext);
            
            try
            {
                pipelineExecutor.InvokeReceivePhysicalMessagePipeline();
            }
            finally 
            {
                pipelineExecutor.CompletePhysicalMessagePipelineContext();
            }
        }

        void IObserver<MessageDequeued>.OnError(Exception error)
        {
        }

        void IObserver<MessageDequeued>.OnCompleted()
        {
        }
    }
}
