using System;

namespace SteeringAI.Core
{
    public class ComponentAuthoringAttribute : Attribute
    {
        public Type TargetComponentType { get; }

        public ComponentAuthoringAttribute(Type targetComponentType)
        {
            TargetComponentType = targetComponentType;
        }
    }
}