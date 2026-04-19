using Unity.Entities;
using Unity.Mathematics;

namespace SteeringAI.Core
{
    public struct VelocityComponent : IComponentData
    {
        public float3 Value;
    }
}