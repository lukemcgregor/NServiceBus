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
    }
}