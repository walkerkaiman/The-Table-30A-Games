using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace SteeringAI.Core
{
    public struct EntityInformation<ComponentT> where ComponentT : unmanaged, IComponentData
    {
        public LocalToWorld LocalToWorld;
        public VelocityComponent Velocity;
        public float Speed;
        public float Radius;
        public float MaxSpeed;
        public Indexes Indexes; 
        public ArchetypeChunk Chunk;
        public ComponentT Component;

        public float3 Direction => Speed == 0 ? Forward : Velocity.Value / Speed;
        
        public float3 Position => LocalToWorld.Position;
        public float3 Forward => LocalToWorld.Forward;
        public float3 Up => LocalToWorld.Up;
        public float3 Right => LocalToWorld.Right;
    }
}