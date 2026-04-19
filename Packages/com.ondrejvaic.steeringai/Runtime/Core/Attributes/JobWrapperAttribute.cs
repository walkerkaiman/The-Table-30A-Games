using System;

namespace SteeringAI.Core
{
    public class JobWrapperAttribute : Attribute
    {
        public Type[] TargetTypes { get; }
        public Type[] SourceTypes { get; }

        public JobWrapperAttribute() { }

        public JobWrapperAttribute(Type[] sourceTypes, Type[] targetTypes)
        {
            SourceTypes = sourceTypes;
            TargetTypes = targetTypes;
        }
        
        public JobWrapperAttribute(Type sourceType, Type targetType)
        {
            SourceTypes = new []{ sourceType };
            TargetTypes = new []{ targetType };
        }
        
        public JobWrapperAttribute(Type sourceType)
        {
            SourceTypes = new []{ sourceType };
        }
        
        public JobWrapperAttribute(Type[] sourceTypes)
        {
            SourceTypes = sourceTypes;
        }
    }
}