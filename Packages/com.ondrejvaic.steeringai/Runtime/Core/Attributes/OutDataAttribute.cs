using System;

namespace SteeringAI.Core
{
    public class OutDataAttribute : Attribute
    {
        public Type[] OutTypes { get; }

        public OutDataAttribute(Type outType)
        {
            this.OutTypes = new []{outType};
        }
        
        public OutDataAttribute(Type[] outTypeses)
        {
            this.OutTypes = outTypeses;
        }
    }
}