namespace NServiceBus.Transports
{
    using System;
    using Unicast.Transport;

    /// <summary>
    /// Interface to implement when developing custom dequeuing strategies.
    /// </summary>
    public interface IDequeueMessages : IObservable<MessageDequeued>
    {
        /// <summary>
        /// Initializes the <see cref="IDequeueMessages"/>.
        /// </summary>
        /// <param name="address">The address to listen on.</param>
        /// <param name="transactionSettings">The <see cref="TransactionSettings"/> to be used by <see cref="IDequeueMessages"/>.</param>
        void Init(Address address, TransactionSettings transactionSettings);
        
        /// <summary>
        /// Starts the dequeuing of message using the specified <paramref name="maximumConcurrencyLevel"/>.
        /// </summary>
        /// <param name="maximumConcurrencyLevel">Indicates the maximum concurrency level this <see cref="IDequeueMessages"/> is able to support.</param>
        void Start(int maximumConcurrencyLevel);
        
        /// <summary>
        /// Stops the dequeuing of messages.
        /// </summary>
        void Stop();
    }

    /// <summary>
    /// 
    /// </summary>
    public struct MessageDequeued
    {
        
    }
}