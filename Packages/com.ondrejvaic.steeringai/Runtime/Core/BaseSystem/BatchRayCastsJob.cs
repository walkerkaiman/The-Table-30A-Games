using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Physics;
using UnityEngine;
using RaycastHit = Unity.Physics.RaycastHit;

namespace SteeringAI.Core
{
    [BurstCompile]
    public struct BatchRayCastsJob : IJobParallelForBatch
    {
        [ReadOnly] private CollisionWorld collisionWorld;
        [ReadOnly] private CollisionFilter collisionFilter;
        [ReadOnly] private NativeArray<RayData> rayDatas;
        [WriteOnly] private NativeArray<RaycastHit> results;
        
        public static JobHandle Schedule(
            CollisionWorld collisionWorld,
            NativeArray<RayData> rayDatas,
            NativeArray<RaycastHit> results,
            LayerMask layerMask, 
            int minCommandsPerJob,
            JobHandle dependency)
        {
            CollisionFilter filter = CollisionFilter.Default;
            filter.CollidesWith = (uint)layerMask.value;
            
            BatchRayCastsJob job = new BatchRayCastsJob
            {
                collisionWorld = collisionWorld,
                results = results,
                rayDatas = rayDatas,
                collisionFilter = filter
            };
            
            return job.Schedule(rayDatas.Length, minCommandsPerJob, dependency);
        }

        public void Execute(int startIndex, int count)
        {
            for (int i = startIndex; i < startIndex + count; i++)
            {
                var rayData = rayDatas[i];
                collisionWorld.CastRay(new RaycastInput
                {
                    Start = rayData.Origin,
                    End = rayData.Origin + rayData.Direction * rayData.MaxDistance,
                    Filter = collisionFilter
                }, out var hit);
                
                results[i] = hit;
            }
        }
    }
}