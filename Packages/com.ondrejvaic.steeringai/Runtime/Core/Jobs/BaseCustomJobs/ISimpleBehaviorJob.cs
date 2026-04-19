using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;

namespace SteeringAI.Core
{
    [JobProducerType(typeof(SimpleBehaviorJobExtensions.ISimpleBehaviorJobProducer<,,>))]
    public interface ISimpleBehaviorJob<ComponentT, out SteeringDataT>
        where ComponentT : unmanaged, ISimpleBaseBehavior, IComponentData
        where SteeringDataT : struct
#if STEERING_DEBUG
        ,IDebugDrawable, IValidable, IToStringFixed
#endif
    {
        SteeringDataT Execute(EntityInformation<ComponentT> entity);
    }

    [BurstCompile]
    public static class SimpleBehaviorJobExtensions
    {
        internal struct JobWrapper<T, ComponentT, SteeringDataT>
            where T : struct, ISimpleBehaviorJob<ComponentT, SteeringDataT>
            where ComponentT : unmanaged, ISimpleBaseBehavior, IComponentData
            where SteeringDataT : struct
#if STEERING_DEBUG
            ,IDebugDrawable, IValidable, IToStringFixed
#endif
        {
            [ReadOnly] public BaseBehaviorParams BaseBehaviorParams;
            [ReadOnly] public ComponentTypeHandle<ComponentT> ComponentHandle;
            
            [NativeDisableParallelForRestriction] [WriteOnly] public NativeArray<SteeringDataT> FinalizedResults;
            
            public T JobData;
        }

        public static unsafe JobHandle Schedule<T, ComponentT, SteeringDataT>(
            this T jobData, 
            SystemBase systemBase,
            in BaseBehaviorParams baseBehaviorParams,
            in IBehaviorResults<SteeringDataT> finalizedBehaviorResults,
            int minIndicesPerJobCount,
            JobHandle dependsOn = default)
            where T : struct, ISimpleBehaviorJob<ComponentT, SteeringDataT>
            where ComponentT : unmanaged, ISimpleBaseBehavior, IComponentData
            where SteeringDataT : struct
#if STEERING_DEBUG
            ,IDebugDrawable, IValidable, IToStringFixed
#endif
        {
            var jobWrapper = new JobWrapper<T, ComponentT, SteeringDataT>
            {
                BaseBehaviorParams = baseBehaviorParams,
                ComponentHandle = systemBase.GetComponentTypeHandle<ComponentT>(true),
                FinalizedResults = finalizedBehaviorResults.Results,
                JobData = jobData,
            };
            
            ISimpleBehaviorJobProducer<T, ComponentT, SteeringDataT>.Initialize();
            var reflectionData = ISimpleBehaviorJobProducer<T, ComponentT, SteeringDataT>.reflectionData.Data;
            CollectionHelper.CheckReflectionDataCorrect<T>(reflectionData);

            var scheduleParams = new JobsUtility.JobScheduleParameters(
                UnsafeUtility.AddressOf(ref jobWrapper),
                reflectionData,
                dependsOn, 
                ScheduleMode.Parallel);

            return JobsUtility.ScheduleParallelFor(ref scheduleParams, baseBehaviorParams.ArchetypeChunks.Length, minIndicesPerJobCount);
        }

        [BurstCompile]
        internal struct ISimpleBehaviorJobProducer<T, ComponentT, SteeringDataT>
            where T : struct, ISimpleBehaviorJob<ComponentT, SteeringDataT>
            where ComponentT : unmanaged, ISimpleBaseBehavior, IComponentData
            where SteeringDataT : struct
#if STEERING_DEBUG
            ,IDebugDrawable, IValidable, IToStringFixed
#endif
        {
            internal static readonly SharedStatic<IntPtr> reflectionData = 
                SharedStatic<IntPtr>.GetOrCreate<ISimpleBehaviorJobProducer<T, ComponentT, SteeringDataT>>();

            [BurstDiscard]
            internal static void Initialize()
            {
                if (reflectionData.Data == IntPtr.Zero)
                    reflectionData.Data = JobsUtility.CreateJobReflectionData(
                        typeof(JobWrapper<T, ComponentT, SteeringDataT>),
                        typeof(T),
                        (ExecuteJobFunction)Execute);
            }

            delegate void ExecuteJobFunction(ref JobWrapper<T, ComponentT, SteeringDataT> jobWrapper, IntPtr additionalPtr, IntPtr bufferRangePatchData,
                ref JobRanges ranges, int jobIndex);

            [BurstCompile]
            public static unsafe void Execute(ref JobWrapper<T, ComponentT, SteeringDataT> jobWrapper, IntPtr additionalPtr, IntPtr bufferRangePatchData,
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

                    var behaviorParams = jobWrapper.BaseBehaviorParams;
                    
                    for (int chunkIndex = begin; chunkIndex < end; chunkIndex++)
                    {
                        var archetypeChunk = behaviorParams.ArchetypeChunks[chunkIndex];
                        if(!archetypeChunk.Has<ComponentT>()) continue;
                        
                        int chunkBaseIndex = behaviorParams.ChunkBaseIndexArray[chunkIndex];
                        var components = archetypeChunk.GetNativeArray(ref jobWrapper.ComponentHandle);
                        
                        for (int entityInChunkIndex = 0; entityInChunkIndex < archetypeChunk.Count; entityInChunkIndex++)
                        {
                            int entityInQueryIndex = chunkBaseIndex + entityInChunkIndex;
                            
                            ComponentT behaviorComponent = components[entityInChunkIndex];
                                
                            if (!behaviorComponent.BaseData.IsActive) continue;
                        
                            var entity = new EntityInformation<ComponentT>
                            {
                                LocalToWorld = behaviorParams.LocalToWorlds[entityInQueryIndex],
                                Velocity = behaviorParams.Velocities[entityInQueryIndex],
                                Speed = behaviorParams.CachedSpeeds[entityInQueryIndex],
                                Radius = behaviorParams.Radii[entityInQueryIndex],
                                MaxSpeed = behaviorParams.MaxSpeeds[entityInQueryIndex],
                                Chunk = archetypeChunk,
                                Component = behaviorComponent,
                                Indexes = behaviorParams.IndexesArray[entityInQueryIndex],
                            };
                            
                            SteeringDataT result = jobWrapper.JobData.Execute(entity);
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
                            }
#endif
                        }
                    }
                }
            }
        }
    }
}
 