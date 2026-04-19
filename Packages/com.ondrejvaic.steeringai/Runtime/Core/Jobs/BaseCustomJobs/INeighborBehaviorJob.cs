using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace SteeringAI.Core
{
    [JobProducerType(typeof(NeighborBehaviorJobExtensions.NeighborBehaviorJobProducer<,,,,>))]
    public interface INeighborBehaviorJob<ComponentT, OtherComponentT, AccumulatorT, out SteeringDataT>
        where ComponentT : unmanaged, INeighborBaseBehavior, IComponentData
        where OtherComponentT : unmanaged, IComponentData
        where AccumulatorT : IAccumulator
        where SteeringDataT : struct 
#if STEERING_DEBUG
        ,IDebugDrawable, IValidable
#endif
    {
        void Execute(in NeighborBehaviorData<ComponentT, OtherComponentT> pair, ref AccumulatorT accumulator);

        SteeringDataT Finalize(in EntityInformation<ComponentT> entity, in AccumulatorT accumulator);
    }

    [BurstCompile]
    public static class NeighborBehaviorJobExtensions
    {
        internal struct JobWrapper<T, ComponentT, OtherComponentT, AccumulatorT, SteeringDataT>
            where T : struct, INeighborBehaviorJob<ComponentT, OtherComponentT, AccumulatorT, SteeringDataT>
            where ComponentT : unmanaged, INeighborBaseBehavior, IComponentData
            where OtherComponentT : unmanaged, IComponentData
            where AccumulatorT : struct, IAccumulator
            where SteeringDataT : struct
#if STEERING_DEBUG
            ,IDebugDrawable, IValidable, IToStringFixed
#endif
        {
            [ReadOnly] public NeighborBehaviorParams NeighborBehaviorParams;
            [ReadOnly] public ComponentTypeHandle<ComponentT> ComponentHandle;
            [ReadOnly] public ComponentTypeHandle<OtherComponentT> OtherComponentHandle;

            [ReadOnly] [DeallocateOnJobCompletion] public NativeArray<bool> OtherHasComponent;
            [ReadOnly] public bool IsOtherComponentTag;
            
            [NativeDisableParallelForRestriction] [WriteOnly] public NativeArray<SteeringDataT> FinalizedResults;
            
            public T JobData;
        }
        
        // public static void EarlyJobInit<T, AccumulatorT, SingleResultT>()
        //     where T : struct, INeighborBehaviorJob<AccumulatorT, SingleResultT>
        //     where AccumulatorT : struct, IAccumulator<SingleResultT>
        //     where SingleResultT : struct
        // {
        //     NeighborBehaviorJobProducer<T, AccumulatorT, SingleResultT>.Initialize();
        // }

        public static unsafe JobHandle Schedule<T, ComponentT, OtherComponentT, AccumulatorT, SteeringDataT>(
            this T jobData, 
            SystemBase systemBase,
            in NeighborBehaviorParams neighborBehaviorParams,
            in IBehaviorResults<SteeringDataT> finalizedBehaviorResults,
            int minIndicesPerJobCount,
            JobHandle dependsOn = default)
            where T : struct, INeighborBehaviorJob<ComponentT, OtherComponentT, AccumulatorT, SteeringDataT>
            where ComponentT : unmanaged, INeighborBaseBehavior, IComponentData
            where OtherComponentT : unmanaged, IComponentData
            where AccumulatorT : struct, IAccumulator
            where SteeringDataT : struct
#if STEERING_DEBUG
            ,IDebugDrawable, IValidable, IToStringFixed
#endif
        {
            var jobWrapper = new JobWrapper<T, ComponentT, OtherComponentT, AccumulatorT, SteeringDataT>
            {
                ComponentHandle = systemBase.GetComponentTypeHandle<ComponentT>(true),
                OtherComponentHandle = systemBase.GetComponentTypeHandle<OtherComponentT>(true),
                NeighborBehaviorParams = neighborBehaviorParams,
                FinalizedResults = finalizedBehaviorResults.Results,
                IsOtherComponentTag = TypeManager.IsZeroSized(TypeManager.GetTypeIndex(typeof(OtherComponentT))),
                OtherHasComponent = new NativeArray<bool>(neighborBehaviorParams.NeighborQueryParams.OtherBaseParams.ArchetypeChunks.Length, Allocator.TempJob),
                JobData = jobData,
            };
            NeighborBehaviorJobProducer<T, ComponentT, OtherComponentT, AccumulatorT, SteeringDataT>.Initialize();
            var reflectionData = NeighborBehaviorJobProducer<T, ComponentT, OtherComponentT, AccumulatorT, SteeringDataT>.reflectionData.Data;
            CollectionHelper.CheckReflectionDataCorrect<T>(reflectionData);

            var scheduleParams = new JobsUtility.JobScheduleParameters(
                UnsafeUtility.AddressOf(ref jobWrapper),
                reflectionData,
                dependsOn, 
                ScheduleMode.Parallel);

            for (int i = 0; i < neighborBehaviorParams.NeighborQueryParams.OtherBaseParams.ArchetypeChunks.Length; i++)
            {
                jobWrapper.OtherHasComponent[i] = neighborBehaviorParams.NeighborQueryParams.OtherBaseParams.ArchetypeChunks[i].Has<OtherComponentT>();
            }

            return JobsUtility.ScheduleParallelFor(ref scheduleParams, neighborBehaviorParams.NeighborQueryParams.MainBaseParams.ArchetypeChunks.Length, minIndicesPerJobCount);
        }

        [BurstCompile]
        internal struct NeighborBehaviorJobProducer<T, ComponentT, OtherComponentT, AccumulatorT, SteeringDataT>
            where T : struct, INeighborBehaviorJob<ComponentT, OtherComponentT, AccumulatorT, SteeringDataT>
            where ComponentT : unmanaged, INeighborBaseBehavior, IComponentData
            where OtherComponentT : unmanaged, IComponentData
            where AccumulatorT : struct, IAccumulator
            where SteeringDataT : struct
#if STEERING_DEBUG
            ,IDebugDrawable, IValidable, IToStringFixed
#endif
        {
            internal static readonly SharedStatic<IntPtr> reflectionData = 
                SharedStatic<IntPtr>.GetOrCreate<NeighborBehaviorJobProducer<T, ComponentT, OtherComponentT, AccumulatorT, SteeringDataT>>();

            [BurstDiscard]
            internal static void Initialize()
            {
                if (reflectionData.Data == IntPtr.Zero)
                    reflectionData.Data = JobsUtility.CreateJobReflectionData(
                        typeof(JobWrapper<T, ComponentT, OtherComponentT, AccumulatorT, SteeringDataT>),
                        typeof(T),
                        (ExecuteJobFunction)Execute);
            }

            delegate void ExecuteJobFunction(ref JobWrapper<T, ComponentT, OtherComponentT, AccumulatorT, SteeringDataT> jobWrapper, IntPtr additionalPtr, IntPtr bufferRangePatchData,
                ref JobRanges ranges, int jobIndex);

            [BurstCompile]
            public static unsafe void Execute(ref JobWrapper<T, ComponentT, OtherComponentT, AccumulatorT, SteeringDataT> jobWrapper, IntPtr additionalPtr, IntPtr bufferRangePatchData,
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

                    var neighborBehaviorParams = jobWrapper.NeighborBehaviorParams;
                    
                    for (int chunkIndex = begin; chunkIndex < end; chunkIndex++)
                    {
                        var archetypeChunk = neighborBehaviorParams.NeighborQueryParams.MainBaseParams.ArchetypeChunks[chunkIndex];
                        if(!archetypeChunk.Has<ComponentT>()) continue;
                        
                        int chunkBaseIndex = neighborBehaviorParams.NeighborQueryParams.MainBaseParams.ChunkBaseIndexArray[chunkIndex];
                        var components = archetypeChunk.GetNativeArray(ref jobWrapper.ComponentHandle);
                        
                        for (int entityInChunkIndex = 0; entityInChunkIndex < archetypeChunk.Count; entityInChunkIndex++)
                        {
                            int entityInQueryIndex = chunkBaseIndex + entityInChunkIndex;

                            var component = components[entityInChunkIndex];
                            if (!component.BaseData.IsActive) continue;
                    
                            NeighborBehaviorData<ComponentT, OtherComponentT> neighborData = new NeighborBehaviorData<ComponentT, OtherComponentT>
                            {
                                Entity = new EntityInformation<ComponentT>
                                {
                                    LocalToWorld = neighborBehaviorParams.NeighborQueryParams.MainBaseParams.LocalToWorlds[entityInQueryIndex],
                                    Velocity = neighborBehaviorParams.NeighborQueryParams.MainBaseParams.Velocities[entityInQueryIndex],
                                    Speed = neighborBehaviorParams.NeighborQueryParams.MainBaseParams.CachedSpeeds[entityInQueryIndex],
                                    Radius = neighborBehaviorParams.NeighborQueryParams.MainBaseParams.Radii[entityInQueryIndex],
                                    MaxSpeed = neighborBehaviorParams.NeighborQueryParams.MainBaseParams.MaxSpeeds[entityInQueryIndex],
                                    Chunk = archetypeChunk,
                                    Indexes = neighborBehaviorParams.NeighborQueryParams.MainBaseParams.IndexesArray[entityInQueryIndex],
                                    Component = component,
                                },
                                OtherEntity = new EntityInformation<OtherComponentT>(),
                            };
                    
                            var accumulator = new AccumulatorT();
                            accumulator.Init();
                            
                            float CosOfFOV = math.cos(math.radians(component.BaseData.MaxAngle * .5f));
                            
                            for (int j = 0; j < neighborBehaviorParams.MaxNumNeighbors; j++)
                            {
                                NeighborMatch neighborMatch = neighborBehaviorParams.Neighbors[entityInQueryIndex * neighborBehaviorParams.MaxNumNeighbors + j];
                    
                                if (neighborMatch.OtherIndex == -1) continue;
                                Indexes neighborIndex = neighborBehaviorParams.NeighborQueryParams.OtherBaseParams.IndexesArray[neighborMatch.OtherIndex];
                                
                                if(!jobWrapper.OtherHasComponent[neighborIndex.ChunkIndex]) continue;

                                float maxDistance = neighborData.Entity.Component.BaseData.MaxDistance;
                                
                                if (neighborMatch.DistanceToSurface + math.EPSILON < maxDistance && neighborMatch.DistanceToOrigin > math.EPSILON)
                                {
                                    LocalToWorld otherLocalToWorld = neighborBehaviorParams.NeighborQueryParams.OtherBaseParams.LocalToWorlds[neighborIndex.EntityInQueryIndex];
                                    float3 toOther = otherLocalToWorld.Position - neighborData.Entity.Position;
                                    float3 toOtherNormalized = toOther / neighborMatch.DistanceToOrigin;
                                    
                                    float dot = math.dot(toOtherNormalized, neighborData.Entity.Direction);
							
                                    if(dot >= CosOfFOV) {
                                        var otherArchetypeChunk = neighborBehaviorParams.NeighborQueryParams.OtherBaseParams.ArchetypeChunks[neighborIndex.ChunkIndex];
                                        
                                        neighborData.OtherEntity.LocalToWorld = otherLocalToWorld;
                                        neighborData.OtherEntity.Velocity = neighborBehaviorParams.NeighborQueryParams.OtherBaseParams.Velocities[neighborIndex.EntityInQueryIndex];
                                        neighborData.OtherEntity.Speed = neighborBehaviorParams.NeighborQueryParams.OtherBaseParams.CachedSpeeds[neighborIndex.EntityInQueryIndex];
                                        neighborData.OtherEntity.Radius = neighborBehaviorParams.NeighborQueryParams.OtherBaseParams.Radii[neighborIndex.EntityInQueryIndex];
                                        neighborData.OtherEntity.MaxSpeed = neighborBehaviorParams.NeighborQueryParams.OtherBaseParams.MaxSpeeds[neighborIndex.EntityInQueryIndex];
                                        neighborData.OtherEntity.Chunk = otherArchetypeChunk;
                                        neighborData.OtherEntity.Indexes = neighborIndex;
                                        
                                        neighborData.DistanceToSurface = neighborMatch.DistanceToSurface;
                                        neighborData.DistanceToOrigin = neighborMatch.DistanceToOrigin;
                                        neighborData.ToOther = toOther;
                                        neighborData.ToOtherNormalized = toOtherNormalized;
                                    
                                        if (!jobWrapper.IsOtherComponentTag)
                                        {
                                            var otherComponents = neighborData.OtherEntity.Chunk.GetNativeArray(ref jobWrapper.OtherComponentHandle);
                                            neighborData.OtherEntity.Component = otherComponents[neighborIndex.EntityInChunkIndex];   
                                        }

                                        jobWrapper.JobData.Execute(neighborData, ref accumulator);
                                    }
                                }
                            }
                            
                            SteeringDataT result = jobWrapper.JobData.Finalize(neighborData.Entity, accumulator);
#if STEERING_DEBUG
                            result.Color = neighborData.Entity.Component.BaseData.DebugColor;
#endif
                            jobWrapper.FinalizedResults[entityInQueryIndex] = result;
                            
#if STEERING_DEBUG
                            if (!result.IsValid())
                            {
                                result.ToStringFixed(out var errorMessage);
                                Debug.LogWarning("invalid result");
                                Debug.LogWarning(errorMessage); 
                            }
                            if (neighborData.Entity.Component.BaseData.Debug)
                            {
                                result.Draw(neighborData.Entity.Position, neighborData.Entity.Component.BaseData.DebugScale);
                                DebugDraw.DrawArcXZ(
                                    neighborData.Entity.Position,
                                    neighborData.Entity.Direction,
                                    math.min(neighborData.Entity.Component.BaseData.MaxDistance, neighborBehaviorParams.NeighborQueryParams.NeighborhoodSettings.MaxNeighborDistance),
                                    math.min(neighborData.Entity.Component.BaseData.MaxAngle, neighborBehaviorParams.NeighborQueryParams.NeighborhoodSettings.MaxFOV),
                                    neighborData.Entity.Component.BaseData.DebugColor);
                            }
#endif
                        }
                    }
                }
            }
        }
    }
    
}


