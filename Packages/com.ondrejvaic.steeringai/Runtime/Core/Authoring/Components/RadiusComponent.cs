using System;
using Unity.Entities;

namespace SteeringAI.Core
{
    [Serializable]
    public struct RadiusComponent : IComponentData
    {
        public float Radius;
    }
}