namespace NServiceBus.Unicast.Transport
{
    using System;
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;
    using NServiceBus.Satellites;

    class ExecuteSatelliteHandlerBehavior: IBehavior<IncomingContext>
    {
        public void Invoke(IncomingContext context, Action next)
        {
            context.Set("TransportReceiver.MessageHandledSuccessfully", context.Get<ISatellite>().Handle(context.PhysicalMessage));
        }

        public class ExecuteSatelliteHandlerBehaviorRegistration : RegisterStep
        {
            public ExecuteSatelliteHandlerBehaviorRegistration()
                : base("SatelliteHandlerExecutor", typeof(ExecuteSatelliteHandlerBehavior), "Invokes the decryption logic")
            {
                InsertBefore("HandlerTransactionScopeWrapperBehavior");
            }
        }
    }
}