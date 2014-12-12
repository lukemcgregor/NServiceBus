namespace NServiceBus.Transports.Msmq
{
    using System;
    using System.Messaging;
    using System.Security.Principal;
    using System.Threading;
    using Janitor;
    using NServiceBus.CircuitBreakers;
    using NServiceBus.Logging;
    using NServiceBus.Support;
    using NServiceBus.Unicast.Transport;

    /// <summary>
    ///     Default implementation of <see cref="IDequeueMessages" /> for MSMQ.
    /// </summary>
    public class MsmqDequeueStrategy : IDequeueMessages, IDisposable
    {
        /// <summary>
        ///     Creates an instance of <see cref="MsmqDequeueStrategy" />.
        /// </summary>
        /// <param name="configure">Configure</param>
        /// <param name="criticalError">CriticalError</param>
        public MsmqDequeueStrategy(Configure configure, CriticalError criticalError)
        {
            this.configure = configure;
            this.criticalError = criticalError;
        }

        /// <summary>
        ///     Initializes the <see cref="IDequeueMessages" />.
        /// </summary>
        /// <param name="address">The address to listen on.</param>
        /// <param name="settings">The <see cref="TransactionSettings" /> to be used by <see cref="IDequeueMessages" />.</param>
        public void Init(Address address, TransactionSettings settings)
        {
            transactionSettings = settings;

            if (address == null)
            {
                throw new ArgumentException("Input queue must be specified");
            }

            if (!address.Machine.Equals(RuntimeEnvironment.MachineName, StringComparison.OrdinalIgnoreCase))
            {
                var error = string.Format("Input queue [{0}] must be on the same machine as this process [{1}].", address, RuntimeEnvironment.MachineName);
                throw new InvalidOperationException(error);
            }

            queue = new MessageQueue(NServiceBus.MsmqUtilities.GetFullPath(address), false, true, QueueAccessMode.Receive);

            if (transactionSettings.IsTransactional && !QueueIsTransactional())
            {
                throw new ArgumentException(
                    "Queue must be transactional if you configure your endpoint to be transactional (" + address + ").");
            }

            var messageReadPropertyFilter = new MessagePropertyFilter
            {
                Body = true,
                TimeToBeReceived = true,
                Recoverable = true,
                Id = true,
                ResponseQueue = true,
                CorrelationId = true,
                Extension = true,
                AppSpecific = true
            };

            queue.MessageReadPropertyFilter = messageReadPropertyFilter;

            if (configure.PurgeOnStartup())
            {
                queue.Purge();
            }
        }

        /// <summary>
        ///     Starts the dequeuing of message using the specified <paramref name="maximumConcurrencyLevel" />.
        /// </summary>
        /// <param name="maximumConcurrencyLevel">The maximum concurrency level supported.</param>
        public void Start(int maximumConcurrencyLevel)
        {
            MessageQueue.ClearConnectionCache();

            this.maximumConcurrencyLevel = maximumConcurrencyLevel;
            throttlingSemaphore = new SemaphoreSlim(maximumConcurrencyLevel, maximumConcurrencyLevel);

            queue.PeekCompleted += OnPeekCompleted;

            CallPeekWithExceptionHandling(() => queue.BeginPeek());
        }

        /// <summary>
        ///     Stops the dequeuing of messages.
        /// </summary>
        public void Stop()
        {
            queue.PeekCompleted -= OnPeekCompleted;

            stopResetEvent.WaitOne();
            DrainStopSemaphore();
            queue.Dispose();
        }

        /// <summary>
        ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            // Injected
        }

        void DrainStopSemaphore()
        {
            Logger.Debug("Drain stopping 'Throttling Semaphore'.");
            for (var index = 0; index < maximumConcurrencyLevel; index++)
            {
                Logger.Debug(string.Format("Claiming Semaphore thread {0}/{1}.", index + 1, maximumConcurrencyLevel));
                throttlingSemaphore.Wait();
            }
            Logger.Debug("Releasing all claimed Semaphore threads.");
            throttlingSemaphore.Release(maximumConcurrencyLevel);

            throttlingSemaphore.Dispose();
        }

        bool QueueIsTransactional()
        {
            try
            {
                return queue.Transactional;
            }
            catch (Exception ex)
            {
                var error = string.Format("There is a problem with the input queue: {0}. See the enclosed exception for details.", queue.Path);
                throw new InvalidOperationException(error, ex);
            }
        }

        void OnPeekCompleted(object sender, PeekCompletedEventArgs peekCompletedEventArgs)
        {
            stopResetEvent.Reset();

            CallPeekWithExceptionHandling(() => queue.EndPeek(peekCompletedEventArgs.AsyncResult));

            throttlingSemaphore.Wait();

            observable.OnNext(new MessageDequeued());

            //We using an AutoResetEvent here to make sure we do not call another BeginPeek before the Receive has been called
            peekResetEvent.WaitOne();

            CallPeekWithExceptionHandling(() => queue.BeginPeek());

            stopResetEvent.Set();
        }

        void CallPeekWithExceptionHandling(Action action)
        {
            try
            {
                action();
            }
            catch (MessageQueueException messageQueueException)
            {
                RaiseCriticalException(messageQueueException);
            }
        }

        void RaiseCriticalException(MessageQueueException messageQueueException)
        {
            var errorException = string.Format("Failed to peek messages from [{0}].", queue.FormatName);

            if (messageQueueException.MessageQueueErrorCode == MessageQueueErrorCode.AccessDenied)
            {
                errorException =
                    string.Format(
                        "Do not have permission to access queue [{0}]. Make sure that the current user [{1}] has permission to Send, Receive, and Peek  from this queue.",
                        queue.FormatName, GetUserName());
            }

            circuitBreaker.Execute(() => criticalError.Raise("Error in receiving messages.", new InvalidOperationException(errorException, messageQueueException)));
        }

        static string GetUserName()
        {
            var windowsIdentity = WindowsIdentity.GetCurrent();
            return windowsIdentity != null
                ? windowsIdentity.Name
                : "Unknown User";
        }

        static ILog Logger = LogManager.GetLogger<MsmqDequeueStrategy>();
        Configure configure;
        CriticalError criticalError;
        [SkipWeaving]
        CircuitBreaker circuitBreaker = new CircuitBreaker(100, TimeSpan.FromSeconds(30));
        int maximumConcurrencyLevel;
        AutoResetEvent peekResetEvent = new AutoResetEvent(false);
        MessageQueue queue;
        ManualResetEvent stopResetEvent = new ManualResetEvent(true);
        SemaphoreSlim throttlingSemaphore;
        TransactionSettings transactionSettings;

        Observable<MessageDequeued> observable = new Observable<MessageDequeued>();

        /// <summary>
        /// b
        /// </summary>
        /// <param name="observer"></param>
        /// <returns></returns>
        public IDisposable Subscribe(IObserver<MessageDequeued> observer)
        {
            return observable.Subscribe(observer);
        }
    }
}