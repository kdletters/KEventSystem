using System;

namespace Kdletters.EventSystem
{
    [AttributeUsage(AttributeTargets.Method)]
    public class KEventListenerAttribute : Attribute
    {
        public Type EventFlag { get; private set; }

        public KEventListenerAttribute(Type flag)
        {
            EventFlag = flag;
        }
    }
}