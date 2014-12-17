namespace NServiceBus
{
    using System;
    using System.Linq.Expressions;

    /// <summary>
    /// 
    /// </summary>
    public static class ConfigureHandlerSettings
    {
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="THandler"></typeparam>
        /// <param name="config"></param>
        /// <param name="property"></param>
        /// <param name="value"></param>
        public static void InitializeHandlerProperty<THandler>(this BusConfiguration config, Expression<Func<THandler, object>> property, object value)
        {
            
        }
    }
}