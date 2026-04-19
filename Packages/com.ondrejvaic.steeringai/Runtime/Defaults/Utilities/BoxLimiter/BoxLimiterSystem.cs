using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace SteeringAI.Defaults
{
    [UpdateAfter(typeof(UpdatePositionSystem))]
    public partial struct BoxLimiterSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (boxLimiter, transform) in SystemAPI.Query<RefRO<BoxLimiterComponent>, RefRW<LocalTransform>>())
            {
                transform.ValueRW.Position = ClampPosition(transform.ValueRO.Position, boxLimiter.ValueRO.Size);
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) { }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ClampPosition(float3 position, float3 size)
        {
            if (position.x > size.x)
            {
                return new float3(-size.x + 0.5f, position.y, position.z);
            }
            
            if (position.x < -size.x)
            {
                
                return new float3(size.x - 0.5f, position.y, position.z);
            }
            
            if (position.y > size.y)
            {
                
                return new float3(position.x, -size.y + 0.5f, position.z);
            }

            if (position.y < -size.y)
            {
                
                return new float3(position.x, size.y - 0.5f, position.z);
            }

            if (position.z > size.z)
            {
                return new float3(position.x, position.y, -size.z + 0.5f);
            }

            if (position.z < -size.z)
            {
                return new float3(position.x, position.y, size.z - 0.5f); 
            }
            
            return position;
        }
    }
}