using System;

namespace XPlan
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class NotifyHandlerAttribute : Attribute
    {
        public Type MessageType { get; }

        public NotifyHandlerAttribute(Type messageType)
        {
            MessageType = messageType;
        }
    }
}