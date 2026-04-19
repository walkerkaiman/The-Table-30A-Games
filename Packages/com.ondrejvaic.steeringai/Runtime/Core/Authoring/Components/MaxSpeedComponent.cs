using Unity.Entities;

namespace SteeringAI.Core
{
    public struct MaxSpeedComponent : IComponentData
    {
        public float MaxSpeed;
        
        public MaxSpeedComponent(float maxSpeed)
        {
            MaxSpeed = maxSpeed;
        }
    }
}