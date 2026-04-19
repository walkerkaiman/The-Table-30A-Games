using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using RaycastHit = Unity.Physics.RaycastHit;

namespace SteeringAI.Core
{
    [JobProducerType(typeof(RayCastBehaviorJobExtensions.RaycastBehaviorJobProducer<,,,,>))]
    public interface IRaycastBehaviorJob<ComponentT, AccumulatorHitT, AccumulatorMissT, out SteeringDataT>
        where ComponentT : unmanaged, IRayBaseBehavior, IComponentData
        where AccumulatorHitT : IAccumulator
        where AccumulatorMissT : IAccumulator
        where SteeringDataT : struct
#if STEERING_DEBUG
        ,IDebugDrawable, IValidable, IToStringFixed
#endif
    {
        public void OnHit(
            in EntityInformation<ComponentT> entity,
            in RayData rayData,
            in RaycastHit hit,
            ref AccumulatorHitT hitA);
        
        public void OnMiss(
            in EntityInformation<ComponentT> entity,
            in RayData rayData,
            ref AccumulatorMissT missA);

        SteeringDataT Finalize(
            in EntityInformation<ComponentT> entity,
            in AccumulatorHitT hitA,
            in AccumulatorMissT missA);
    }

    [BurstCompile]
    public static class RayCastBehaviorJobExtensions
    {
        internal struct JobWrapper<T, ComponentT, AccumulatorHitT, AccumulatorMissT, SteeringDataT>
            where T : struct, IRaycastBehaviorJob<ComponentT, AccumulatorHitT, AccumulatorMissT, SteeringDataT>
            where ComponentT : unmanaged, IRayBaseBehavior, IComponentData
            where AccumulatorHitT : struct, IAccumulator
            where AccumulatorMissT : struct, IAccumulator
            where SteeringDataT : struct
#if STEERING_DEBUG
            ,IDebugDrawable, IValidable, IToStringFixed
#endif
        {
            [ReadOnly] public RaycastBehaviorParams RaycastBehaviorParams;
            [ReadOnly] public ComponentTypeHandle<ComponentT> ComponentHandle;
            
            [NativeDisableParallelForRestriction] [WriteOnly] public NativeArray<SteeringDataT> FinalizedResults;
            
            public T JobData;
        }
        
        public static unsafe JobHandle Schedule<T, ComponentT, AccumulatorHitT, AccumulatorMissT, SteeringDataT>(
            this T jobData,
            SystemBase systemBase,
            in RaycastBehaviorParams raycastBehaviorParams,
            in IBehaviorResults<SteeringDataT> finalizedBehaviorResults,
            int minIndicesPerJobCount,
            JobHandle dependsOn = default)
            where T : struct, IRaycastBehaviorJob<ComponentT, AccumulatorHitT, AccumulatorMissT, SteeringDataT>
            where ComponentT : unmanaged, IRayBaseBehavior, IComponentData
            where AccumulatorHitT : struct, IAccumulator
            where AccumulatorMissT : struct, IAccumulator
            where SteeringDataT : struct
#if STEERING_DEBUG
            ,IDebugDrawable, IValidable, IToStringFixed
#endif
        {
            var jobWrapper = new JobWrapper<T, ComponentT, AccumulatorHitT, AccumulatorMissT, SteeringDataT>
            {
                RaycastBehaviorParams = raycastBehaviorParams,
                ComponentHandle = systemBase.GetComponentTypeHandle<ComponentT>(true),
                FinalizedResults = finalizedBehaviorResults.Results,
                JobData = jobData,
            };

            RaycastBehaviorJobProducer<T, ComponentT, AccumulatorHitT, AccumulatorMissT, SteeringDataT>.Initialize();
            var reflectionData = RaycastBehaviorJobProducer<T, ComponentT, AccumulatorHitT, AccumulatorMissT, SteeringDataT>.reflectionData.Data;
            CollectionHelper.CheckReflectionDataCorrect<T>(reflectionData);

            var scheduleParams = new JobsUtility.JobScheduleParameters(
                UnsafeUtility.AddressOf(ref jobWrapper),
                reflectionData,
                dependsOn,
                ScheduleMode.Parallel);

            return JobsUtility.ScheduleParallelFor(ref scheduleParams, raycastBehaviorParams.MainBaseParams.ArchetypeChunks.Length, minIndicesPerJobCount);
        }


        [BurstCompile]
        internal struct RaycastBehaviorJobProducer<T, ComponentT, AccumulatorHitT, AccumulatorMissT, SteeringDataT>
            where T : struct, IRaycastBehaviorJob<ComponentT, AccumulatorHitT, AccumulatorMissT, SteeringDataT>
            where ComponentT : unmanaged, IRayBaseBehavior, IComponentData
            where AccumulatorHitT : struct, IAccumulator
            where AccumulatorMissT : struct, IAccumulator
            where SteeringDataT : struct
#if STEERING_DEBUG
            ,IDebugDrawable, IValidable, IToStringFixed
#endif
        {
            internal static readonly SharedStatic<IntPtr> reflectionData = SharedStatic<IntPtr>.GetOrCreate<RaycastBehaviorJobProducer<T, ComponentT, AccumulatorHitT, AccumulatorMissT, SteeringDataT>>();

            [BurstDiscard]
            internal static void Initialize()
            {
                if (reflectionData.Data == IntPtr.Zero)
                    reflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(JobWrapper<T, ComponentT, AccumulatorHitT, AccumulatorMissT, SteeringDataT>), typeof(T), (ExecuteJobFunction)Execute);
            }

            delegate void ExecuteJobFunction(ref JobWrapper<T, ComponentT, AccumulatorHitT, AccumulatorMissT, SteeringDataT> jobWrapper, IntPtr additionalPtr, IntPtr bufferRangePatchData,
                ref JobRanges ranges, int jobIndex);

            [BurstCompile]
            public static unsafe void Execute(ref JobWrapper<T, ComponentT, AccumulatorHitT, AccumulatorMissT, SteeringDataT> jobWrapper, IntPtr additionalPtr, IntPtr bufferRangePatchData,
                ref JobRanges ranges, int jobIndex)
            {
                while (true)
                {
                    int begin;
                    int end;

                    if (!JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out begin, out end))
                    {
                        return;
                    }

                    RaycastBehaviorParams raycastBehaviorParams = jobWrapper.RaycastBehaviorParams;

                    for (int chunkIndex = begin; chunkIndex < end; chunkIndex++)
                    {
                        var archetypeChunk = raycastBehaviorParams.MainBaseParams.ArchetypeChunks[chunkIndex];
                        if(!archetypeChunk.Has<ComponentT>()) continue;
                        
                        int chunkBaseIndex = raycastBehaviorParams.MainBaseParams.ChunkBaseIndexArray[chunkIndex];
                        var components = archetypeChunk.GetNativeArray(ref jobWrapper.ComponentHandle);
                        
                        for (int entityInChunkIndex = 0; entityInChunkIndex < archetypeChunk.Count; entityInChunkIndex++)
                        {
                            int entityInQueryIndex = chunkBaseIndex + entityInChunkIndex;
                            ComponentT behaviorComponent = components[entityInChunkIndex];

                            if (!behaviorComponent.BaseData.IsActive) continue;

                            EntityInformation<ComponentT> entity = new EntityInformation<ComponentT>
                            {
                                LocalToWorld = raycastBehaviorParams.MainBaseParams.LocalToWorlds[entityInQueryIndex], 
                                Velocity = raycastBehaviorParams.MainBaseParams.Velocities[entityInQueryIndex],
                                Speed = raycastBehaviorParams.MainBaseParams.CachedSpeeds[entityInQueryIndex],
                                Radius = raycastBehaviorParams.MainBaseParams.Radii[entityInQueryIndex],
                                MaxSpeed = raycastBehaviorParams.MainBaseParams.MaxSpeeds[entityInQueryIndex],
                                Chunk = archetypeChunk,
                                Indexes = raycastBehaviorParams.MainBaseParams.IndexesArray[entityInQueryIndex],
                                Component = behaviorComponent,
                            };
                           
                            NativeSlice<RaycastHit> raycastHits = raycastBehaviorParams.RaycastHits.Slice(
                                entityInQueryIndex * raycastBehaviorParams.RaySettings.NumRays,
                                raycastBehaviorParams.RaySettings.NumRays);
                            
                            NativeSlice<RayData> rayDatas = raycastBehaviorParams.RaycastDatas.Slice(
                                entityInQueryIndex * raycastBehaviorParams.RaySettings.NumRays,
                                raycastBehaviorParams.RaySettings.NumRays);

                            var hitAccumulator = new AccumulatorHitT();
                            hitAccumulator.Init();
                            var missAccumulator = new AccumulatorMissT();
                            missAccumulator.Init();
                            
                            for (int rayIndex = 0; rayIndex < raycastHits.Length; rayIndex++)
                            {
                                var raycastHit = raycastHits[rayIndex];
                                var rayData = rayDatas[rayIndex];
                                
                                if (raycastHit.Entity != Entity.Null && raycastHit.Fraction * rayData.MaxDistance < behaviorComponent.BaseData.MaxDistance)
                                {
                                    jobWrapper.JobData.OnHit(entity, rayData, raycastHit, ref hitAccumulator);
#if STEERING_DEBUG
                                    if (entity.Component.BaseData.DebugRays)
                                    {
                                        DebugDraw.DrawArrow(entity.Position, entity.Position + rayData.Direction * raycastHit.Fraction * raycastBehaviorParams.RaySettings.MaxDistance, Color.red);
                                    }
#endif
                                }
                                else
                                {
                                    jobWrapper.JobData.OnMiss(entity, rayData, ref missAccumulator);
#if STEERING_DEBUG
                                    if (entity.Component.BaseData.DebugRays)
                                    {
                                        DebugDraw.DrawArrow(entity.Position, entity.Position + rayData.Direction * math.min(behaviorComponent.BaseData.MaxDistance, raycastBehaviorParams.RaySettings.MaxDistance) , Color.green);
                                    }
#endif
                                }
                            }
                                
                            SteeringDataT result = jobWrapper.JobData.Finalize(entity, hitAccumulator, missAccumulator);
#if STEERING_DEBUG
                            result.Color = entity.Component.BaseData.Color;
#endif
                            jobWrapper.FinalizedResults[entityInQueryIndex] = result;
                            
#if STEERING_DEBUG
                            if (!result.IsValid())
                            {
                                result.ToStringFixed(out var errorMessage);
                                Debug.LogWarning("invalid result ");
                                Debug.LogWarning(errorMessage); 
                            }
                            if (entity.Component.BaseData.Debug)
                            {
                                result.Draw(entity.Position, entity.Component.BaseData.DebugScale);
                                DebugDraw.DrawCircleXZ(entity.Position, entity.Component.BaseData.MaxDistance, entity.Component.BaseData.Color);
                            }
#endif
                        }
                    }
                }
            }
        }
    }
}
