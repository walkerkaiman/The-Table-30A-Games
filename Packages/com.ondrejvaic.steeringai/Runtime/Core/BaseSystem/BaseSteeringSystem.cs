using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using RaycastHit = Unity.Physics.RaycastHit;

namespace SteeringAI.Core
{
    public partial class SteeringSystemsGroup : ComponentSystemGroup { }

    [UpdateAfter(typeof(SteeringSystemsGroup))]
    public partial class AfterSteeringSystemsGroup : ComponentSystemGroup { }
    
    [UpdateInGroup(typeof(SteeringSystemsGroup))]
    public abstract partial class BaseSteeringSystem : SystemBase
    {
        protected abstract string getAssetReferenceName();
        private SteeringSystemAsset steeringSystemAsset;

        private EntityQuery entityQuery;
        private readonly List<EntityQuery> neighborQueries = new();
        private bool isInitialized;
        private bool isValid;
        
        protected override void OnCreate()
        {
            isInitialized = false;

            AsyncOperationHandle<SteeringSystemAsset> handle = Addressables.LoadAssetAsync<SteeringSystemAsset>(getAssetReferenceName());
            handle.WaitForCompletion();

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                steeringSystemAsset = handle.Result;
                steeringSystemAsset.Init();
                isValid = steeringSystemAsset.Load();

                if (!isValid)
                {
                    Debug.LogError(steeringSystemAsset.name + " is invalid, please open it in the Steering System Editor to resolve this.");
                    return;   
                }
            }
            else
            {
                Debug.LogError("Failed to load addressable asset: " + handle.OperationException);
                return;
            }

            List<ComponentType> componentTypes = new List<ComponentType>
            {
                new (typeof(VelocityComponent), ComponentType.AccessMode.ReadWrite),
                new (typeof(LocalTransform), ComponentType.AccessMode.ReadOnly),
                new (typeof(SteeringEntityTagComponent), ComponentType.AccessMode.ReadOnly),
                new (typeof(IsSimulatingTagComponent), ComponentType.AccessMode.ReadOnly),
                new (Type.GetType(steeringSystemAsset.MainTagDescription.AssemblyQualifiedName), ComponentType.AccessMode.ReadOnly)
            };

            var componentQueryDesc = new EntityQueryDesc
            {
                All = componentTypes.ToArray()
            };
            entityQuery = GetEntityQuery(componentQueryDesc);

            foreach (var neighborBehaviorGroup in steeringSystemAsset.NeighborBehaviorGroups)
            {
                neighborQueries.Add(GetEntityQuery(neighborBehaviorGroup.TargetQueryDesc));
            }
        }

        protected override void OnUpdate()
        {
            if(!isValid) return;
            
            if(entityQuery.IsEmpty)
            {
                return;
            }
            
            if (!isInitialized)
            {
                isInitialized = true;
                return;
            }
            
            var entityCount = entityQuery.CalculateEntityCount();
            var initializationHandle = initializeData(entityQuery, entityCount, out var baseBehaviorParams, Dependency);
            
            var collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            
            var raycastHandle = calculateRaycastsBehaviorResults(baseBehaviorParams, collisionWorld, out var raycastFinalizedResults, initializationHandle);
            var neighborsHandle = calculateNeighborResults(baseBehaviorParams, out var neighborsFinalizedResults, initializationHandle);
            var simpleHandle = calculateSimpleResults(baseBehaviorParams, out var simpleResults, initializationHandle);
            
            var completeTotalResultsHandle = combineTotalResults(
                baseBehaviorParams,
                combineDependencies(raycastHandle, neighborsHandle, simpleHandle),
                raycastFinalizedResults,
                neighborsFinalizedResults,
                simpleResults);

            Dependency = completeTotalResultsHandle;
            baseBehaviorParams.Dispose(Dependency);
        }

        private JobHandle runNeighborBehaviorGroup(
            in EntityQuery otherQuery,
            in BaseBehaviorParams baseBehaviorParams,
            in NeighborBehaviorGroup neighborBehaviorGroup,
            out IDelayedDisposable[] neighborsFinalizedResults,
            in JobHandle dependency)
        {
            if (otherQuery.IsEmpty)
            {
                neighborsFinalizedResults = new IDelayedDisposable[neighborBehaviorGroup.BehaviorJobRunners.Length];
                return dependency;
            }
            
            int numOtherEntities = otherQuery.CalculateEntityCount();
            
            var initializeDataHandle = initializeData(otherQuery, numOtherEntities, out var otherBaseData, dependency);
            
            NeighborQueryParams neighborQueryParams = new NeighborQueryParams
            {
                MainBaseParams = baseBehaviorParams,
                OtherBaseParams = otherBaseData,
                NeighborhoodSettings = neighborBehaviorGroup.NeighborhoodSettings
            };
            
            var neighbors = new NativeArray<NeighborMatch>(baseBehaviorParams.EntityCount * neighborBehaviorGroup.NeighborhoodSettings.MaxNumNeighbors, Allocator.TempJob);
            var neighborhoodDataConstructionJobHandle = neighborBehaviorGroup.NeighborQueryJobWrapper.Schedule(
                this,
                neighborQueryParams,
                neighbors,
                initializeDataHandle);

            NeighborBehaviorParams neighborBehaviorParams = new NeighborBehaviorParams
            {
                Neighbors = neighbors,
                MaxNumNeighbors = neighborBehaviorGroup.NeighborhoodSettings.MaxNumNeighbors,
                NeighborQueryParams = neighborQueryParams
            };
            
            neighborsFinalizedResults = new IDelayedDisposable[neighborBehaviorGroup.BehaviorJobRunners.Length];
            var combinedBehaviorJobHandle = neighborhoodDataConstructionJobHandle;
            for (int i = 0; i < neighborBehaviorGroup.BehaviorJobRunners.Length; i++)
            {
                var jobHandle = neighborBehaviorGroup.BehaviorJobRunners[i].Schedule(
                    this,
                    neighborBehaviorParams,
                    out var result,
                    neighborhoodDataConstructionJobHandle);
                
                neighborsFinalizedResults[i] = result;
                combinedBehaviorJobHandle = JobHandle.CombineDependencies(combinedBehaviorJobHandle, jobHandle);
            }

            otherBaseData.Dispose(combinedBehaviorJobHandle);
            neighbors.Dispose(combinedBehaviorJobHandle);
            return combinedBehaviorJobHandle;
        }
        
        private JobHandle calculateNeighborResults(
            in BaseBehaviorParams baseBehaviorParams,
            out IDelayedDisposable[] neighborsResults,
            in JobHandle dependency)
        {
            var neighborBehaviorGroups = steeringSystemAsset.NeighborBehaviorGroups;

            int totalNumBehaviors = 0;
            foreach (var neighborBehaviorGroup in neighborBehaviorGroups)
            {
                totalNumBehaviors += neighborBehaviorGroup.BehaviorJobRunners.Length;
            }

            neighborsResults = new IDelayedDisposable[totalNumBehaviors];
            int neighborResultIndex = 0;
            
            var combinedBehaviorJobHandle = dependency;
            for (int i = 0; i < neighborBehaviorGroups.Length; i++)
            {
                var jobHandle = runNeighborBehaviorGroup(
                    neighborQueries[i],
                    baseBehaviorParams,
                    neighborBehaviorGroups[i],
                    out var neighborsFinalizedResults,
                    dependency);

                foreach (var neighborsResult in neighborsFinalizedResults)
                {
                    neighborsResults[neighborResultIndex++] = neighborsResult;
                }
                
                combinedBehaviorJobHandle = JobHandle.CombineDependencies(combinedBehaviorJobHandle, jobHandle);
            }

            return combinedBehaviorJobHandle;
        }

        private JobHandle raycast(
            in BaseBehaviorParams baseBehaviorParams,
            in RaycastSettings raycastSettings,
            ICreateRaysJobWrapper jobWrapper,
            in CollisionWorld collisionWorld,
            out NativeArray<RayData> rayDatas,
            out NativeArray<RaycastHit> raycastHits,
            JobHandle dependency)
        {
            rayDatas = new NativeArray<RayData>(baseBehaviorParams.EntityCount * raycastSettings.NumRays, Allocator.TempJob);
            raycastHits = new NativeArray<RaycastHit>(baseBehaviorParams.EntityCount * raycastSettings.NumRays, Allocator.TempJob);

            if (raycastSettings.NumRays <= 0)
            {
                return dependency;
            }

            CreateRaysParams createRaysParams = new CreateRaysParams
            {
                MainBaseParams = baseBehaviorParams,
                RaySettings = raycastSettings
            };
            
            var createRaycastCommandsJobHandle = jobWrapper.Schedule(this, createRaysParams, rayDatas, dependency);
            var rayCommandJobHandle = BatchRayCastsJob.Schedule(
                collisionWorld,
                rayDatas,
                raycastHits,
                raycastSettings.LayerMask,
                1,
                createRaycastCommandsJobHandle);
            
            return rayCommandJobHandle;
        }

        private JobHandle calculateSimpleResults(in BaseBehaviorParams baseBehaviorParams, out IDelayedDisposable[] results, JobHandle dependency)
        {
            var behaviorJobRunners = steeringSystemAsset.SimpleBehaviors;

            results = new IDelayedDisposable[behaviorJobRunners.Length];
            var combinedBehaviorJobHandle = dependency;
            for (int i = 0; i < behaviorJobRunners.Length; i++)
            {
                var jobHandle = behaviorJobRunners[i].Schedule(
                    this,
                    baseBehaviorParams,
                    out var result,
                    dependency);

                results[i] = result;
                combinedBehaviorJobHandle = JobHandle.CombineDependencies(combinedBehaviorJobHandle, jobHandle);
            }

            return combinedBehaviorJobHandle;
        }

        private JobHandle runRaycastBehaviorGroup(
            in BaseBehaviorParams baseBehaviorParams,
            in RaycastBehaviorGroup raycastBehaviorGroup,
            in CollisionWorld collisionWorld,
            out IDelayedDisposable[] raycastResults,
            in JobHandle dependency)
        {
            JobHandle raycastJobHandle = raycast(
                baseBehaviorParams,
                raycastBehaviorGroup.RaySettings,
                raycastBehaviorGroup.CreateRaysJobWrapper,
                collisionWorld,
                out var rayDatas,
                out var raycastHits,
                dependency);
            
            RaycastBehaviorParams raycastBehaviorParams = new RaycastBehaviorParams
            {
                MainBaseParams = baseBehaviorParams,
                RaySettings = raycastBehaviorGroup.RaySettings,
                RaycastHits = raycastHits,
                RaycastDatas = rayDatas,
            };
            
            var raycastJobRunners = raycastBehaviorGroup.BehaviorJobRunners;
            raycastResults = new IDelayedDisposable[raycastJobRunners.Length];
            
            var combinedRaycastBehaviorJobHandle = raycastJobHandle;
            for (int i = 0; i < raycastJobRunners.Length; i++)
            {
                var jobHandle = raycastJobRunners[i].Schedule(
                    this,
                    raycastBehaviorParams,
                    out var result,
                    raycastJobHandle);
                
                raycastResults[i] = result;
                combinedRaycastBehaviorJobHandle = JobHandle.CombineDependencies(combinedRaycastBehaviorJobHandle, jobHandle);
            }

            rayDatas.Dispose(combinedRaycastBehaviorJobHandle);
            raycastHits.Dispose(combinedRaycastBehaviorJobHandle);
            return combinedRaycastBehaviorJobHandle;
        }
        
        private JobHandle calculateRaycastsBehaviorResults(
            in BaseBehaviorParams baseBehaviorParams,
            in CollisionWorld collisionWorld,
            out IDelayedDisposable[] raycastResults,
            in JobHandle dependency)
        {
            var raycastJobGroups = steeringSystemAsset.RaycastBehaviorGroups;   
            
            int totalNumBehaviors = raycastJobGroups.Sum(raycastBehaviorGroup => raycastBehaviorGroup.BehaviorJobRunners.Length);

            raycastResults = new IDelayedDisposable[totalNumBehaviors];
            int raycastResultIndex = 0;
            
            var combinedBehaviorJobHandle = dependency;
            foreach (var raycastJobGroup in raycastJobGroups)
            {
                var jobHandle = runRaycastBehaviorGroup(
                    baseBehaviorParams,
                    raycastJobGroup,
                    collisionWorld,
                    out var raycastFinalizedResults,
                    dependency); 
                
                foreach (var neighborsResult in raycastFinalizedResults)
                {
                    raycastResults[raycastResultIndex++] = neighborsResult;
                }
                
                combinedBehaviorJobHandle = JobHandle.CombineDependencies(combinedBehaviorJobHandle, jobHandle);
            }

            return combinedBehaviorJobHandle;
        }

        private JobHandle combineDependencies(params JobHandle[] dependencies)
        {
            NativeArray<JobHandle> dependenciesNativeArray = new NativeArray<JobHandle>(dependencies.Length, Allocator.Temp);
            for (int i = 0; i < dependencies.Length; i++)
            {
                dependenciesNativeArray[i] = dependencies[i];
            }
            
            return JobHandle.CombineDependencies(dependenciesNativeArray);
        }
        
        private JobHandle combineTotalResults(
            in BaseBehaviorParams baseBehaviorParams,
            JobHandle dependency,
            params IDelayedDisposable[][] totalResults)
        {
            
            if (steeringSystemAsset.MergeJobWrapper.JobRunner == null)
            {
                Debug.LogError("Missing CombineJobRunner in SteeringSystemAsset");
                return dependency;
            }
            
            int totalNumResults = totalResults.Sum(t => t.Length);
            IDelayedDisposable[] totalResultsArray = new IDelayedDisposable[totalNumResults];
            int writeIndex = 0;
            
            foreach (var totalResult in totalResults)
            {
                foreach (var result in totalResult)
                {
                    totalResultsArray[writeIndex] = result;
                    writeIndex++;
                }
            }
            
            var combineResultsDependency = steeringSystemAsset.MergeJobWrapper.JobRunner.Schedule(
                this,
                baseBehaviorParams,
                totalResultsArray,
                dependency);

            foreach (var resultsArray in totalResultsArray)
            {
                resultsArray?.Dispose(combineResultsDependency);
            }
            
            return combineResultsDependency;
        }

        private JobHandle initializeData(in EntityQuery query, int boidCount, out BaseBehaviorParams baseBehaviorParams, JobHandle dependency)
        {
            baseBehaviorParams = new BaseBehaviorParams
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                EntityCount = boidCount,
                LocalToWorlds = new NativeArray<LocalToWorld>(boidCount, Allocator.TempJob),
                Radii = new NativeArray<float>(boidCount, Allocator.TempJob),
                Velocities = new NativeArray<VelocityComponent>(boidCount, Allocator.TempJob),
                CachedSpeeds = new NativeArray<float>(boidCount, Allocator.TempJob),
                MaxSpeeds = new NativeArray<float>(boidCount, Allocator.TempJob),
                ArchetypeChunks = query.ToArchetypeChunkArray(Allocator.TempJob),
                ChunkBaseIndexArray = query.CalculateBaseEntityIndexArrayAsync(Allocator.TempJob, dependency, out var indexJobHandle),
                IndexesArray = new NativeArray<Indexes>(boidCount, Allocator.TempJob),
            };

            var fillDataHandle = new FillBaseDataJob
            {
                VelocityTypeHandle = GetComponentTypeHandle<VelocityComponent>(true),
                LocalToWorldTypeHandle = GetComponentTypeHandle<LocalToWorld>(true),
                RadiusTypeHandle = GetComponentTypeHandle<RadiusComponent>(true),
                SpeedSettingsTypeHandle = GetComponentTypeHandle<MaxSpeedComponent>(true),
                LocalToWorlds = baseBehaviorParams.LocalToWorlds,
                ArchetypeChunks = baseBehaviorParams.ArchetypeChunks,
                ChunkBaseIndexArray = baseBehaviorParams.ChunkBaseIndexArray,
                IndexesArray = baseBehaviorParams.IndexesArray,
                Velocities = baseBehaviorParams.Velocities,
                CachedSpeeds = baseBehaviorParams.CachedSpeeds,
                Radii = baseBehaviorParams.Radii,
                MaxSpeeds = baseBehaviorParams.MaxSpeeds,
            }.Schedule(baseBehaviorParams.ArchetypeChunks.Length, 1, indexJobHandle);
            
            return fillDataHandle;
        }
    }
}