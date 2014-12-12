namespace NServiceBus.Unicast.Transport
{
    using System;
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;
    using NServiceBus.Unicast.Transport.Monitoring;

    class ReceivePerformanceDiagnosticsBehavior : IBehavior<IncomingContext>
    {
        public void Invoke(IncomingContext context, Action next)
        {
            context.Get<ReceivePerformanceDiagnostics>().MessageDequeued();
            next();
        }
    }
}