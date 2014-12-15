namespace NServiceBus.Unicast.Transport
{
    using System;
    using System.Transactions;
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;

    class HandlerTransactionScopeWrapperBehavior : IBehavior<IncomingContext>
    {
        public TransactionSettings TransactionSettings { get; set; }

        public void Invoke(IncomingContext context, Action next)
        {
            using (var tx = GetTransactionScope())
            {
                next();

                tx.Complete();
            }
        }

        TransactionScope GetTransactionScope()
        {
            if (TransactionSettings.DoNotWrapHandlersExecutionInATransactionScope)
            {
                return new TransactionScope(TransactionScopeOption.Suppress);
            }

            return new TransactionScope(TransactionScopeOption.Required, new TransactionOptions
            {
                IsolationLevel = TransactionSettings.IsolationLevel,
                Timeout = TransactionSettings.TransactionTimeout
            });
        }
    }
}