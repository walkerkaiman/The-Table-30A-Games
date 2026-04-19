using SteeringAI.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace SteeringAI.Defaults
{
    [BurstCompile]
    struct CombineVelocitiesJob : IJobParallelFor
    {
        [ReadOnly] public int NumResults;
        [NativeDisableParallelForRestriction] public NativeArray<VelocityResult> MergedResults;
        [ReadOnly] public NativeArray<ArchetypeChunk> ArchetypeChunks;
        [ReadOnly] public NativeArray<int> BaseIndexes;

        public ComponentTypeHandle<DesiredVelocityComponent> DesiredVelocityHandle;
        
#if STEERING_DEBUG
        [ReadOnly] public ComponentTypeHandle<LocalToWorld> LocalToWorldHandle;
#endif
        
        public void Execute(int chunkIndex)
        {
            var chunk = ArchetypeChunks[chunkIndex];
            if(!chunk.Has<DesiredVelocityComponent>()) return;
            
            var desiredVelocities = chunk.GetNativeArray(ref DesiredVelocityHandle);
            
#if STEERING_DEBUG
            var positions = chunk.GetNativeArray(ref LocalToWorldHandle);
#endif

            int baseIndex = BaseIndexes[chunkIndex];
            for (int i = 0; i < chunk.Count; i++)
            {
                int index = baseIndex + i;
                float maxSumDesire = 1;

                float3 sumDirection = float3.zero;
                float sumDirectionDesire = 0;
#if STEERING_DEBUG
                float3 debugStartPosition = positions[i].Position;
#endif
                DesiredVelocityComponent desiredVelocity = desiredVelocities[i];

                NativeSlice<VelocityResult> resultsSlice1 = MergedResults.Slice(index * NumResults, NumResults);
                resultsSlice1.Sort<VelocityResult, PriorityComparerDirection>(default);
                
                for (int resultIndex = 0; resultIndex < NumResults; resultIndex++)
                {
                    var result = MergedResults[NumResults * index + resultIndex];
                    
                    if(sumDirectionDesire + result.DirectionDesire >= maxSumDesire)
                    {
                        float leftOver = maxSumDesire - sumDirectionDesire;
                        sumDirection += result.Direction * leftOver;
                        sumDirectionDesire += leftOver;
#if STEERING_DEBUG
                        if (desiredVelocity.Debug)
                        {
                            float3 newDebugStartPosition = debugStartPosition + result.Direction * leftOver * desiredVelocity.DebugScale;
                            DebugDraw.DrawArrow(debugStartPosition, newDebugStartPosition, result.Color);
                            debugStartPosition = newDebugStartPosition;
                        }
#endif
                        break;
                    }
                    
                    sumDirectionDesire += result.DirectionDesire;
                    sumDirection += result.Direction * result.DirectionDesire;
#if STEERING_DEBUG
                    if (desiredVelocity.Debug)
                    {
                        float3 newDebugStartPosition = debugStartPosition + result.Direction * result.DirectionDesire * desiredVelocity.DebugScale;
                        DebugDraw.DrawArrow(debugStartPosition, newDebugStartPosition, result.Color);
                        debugStartPosition = newDebugStartPosition;
                    }
#endif
                }
                
#if STEERING_DEBUG
                if (desiredVelocity.Debug)
                {
                    DebugDraw.DrawArrow(positions[i].Position, debugStartPosition, desiredVelocity.Color);   
                }
#endif
                if (sumDirectionDesire == 0)
                {
                    desiredVelocity.Value = float3.zero;
                    desiredVelocities[i] = desiredVelocity;
                    continue;
                }
                float3 sumDirectionNormalized = math.normalizesafe(sumDirection, float3.zero);
                
                float sumSpeed = 0;
                float sumSpeedDesire = 0;
                
                NativeSlice<VelocityResult> resultsSlice2 = MergedResults.Slice(index * NumResults, NumResults);
                resultsSlice2.Sort<VelocityResult, PriorityComparerSpeed>(default);
                
                for (int resultIndex = 0; resultIndex < NumResults; resultIndex++)
                {
                    var result = MergedResults[NumResults * index + resultIndex];
                    
                    if(sumSpeedDesire + result.SpeedDesire >= maxSumDesire)
                    {
                        float leftOver = maxSumDesire - sumSpeedDesire;
                        sumSpeedDesire += leftOver;
                        sumSpeed += result.Speed * leftOver;
                        break;
                    }
                    
                    sumSpeedDesire += result.SpeedDesire;
                    sumSpeed += result.Speed * result.SpeedDesire;
                }
                
                if(sumSpeedDesire == 0)
                {
                    desiredVelocity.Value = float3.zero;
                    desiredVelocities[i] = desiredVelocity;
                    continue;
                }
                
                float finalSpeed = sumSpeed / sumSpeedDesire;

                desiredVelocity.Value = sumDirectionNormalized * finalSpeed;
                desiredVelocities[i] = desiredVelocity;
            }
        }
    }
     
    [JobWrapper(typeof(DesiredVelocityComponent))]
    [OutData(typeof(VelocityResults))]
    public class CombineVelocitiesJobWrapper : IMergeJobWrapper
    {
        public JobHandle Schedule(
            SystemBase system,
            in BaseBehaviorParams mainBaseParams,
            in IDelayedDisposable[] results,
            JobHandle dependency)
        {
            var mergedTotalResults = new NativeArray<VelocityResult>(mainBaseParams.EntityCount * results.Length, Allocator.TempJob);

            var totalDependency = dependency;
            for (int i = 0; i < results.Length; i++)
            {
                if (results[i] == null) continue;
                
                totalDependency = new FillVelocityResultsJob
                {
                    SkipLength = results.Length,
                    WriteIndex = i,
                    InArray = ((VelocityResults)results[i]).Results,
                    OutArray = mergedTotalResults,
                }.Schedule(mainBaseParams.EntityCount, 4, totalDependency);
            }
            
            var totalAccumulatorDependency = new CombineVelocitiesJob
            {
                NumResults = results.Length,
                MergedResults = mergedTotalResults,
                ArchetypeChunks = mainBaseParams.ArchetypeChunks,
#if STEERING_DEBUG
                LocalToWorldHandle = system.GetComponentTypeHandle<LocalToWorld>(true),
#endif
                DesiredVelocityHandle = system.GetComponentTypeHandle<DesiredVelocityComponent>(false),
                BaseIndexes = mainBaseParams.ChunkBaseIndexArray,
            }.Schedule(mainBaseParams.ArchetypeChunks.Length, 1, totalDependency);

            mergedTotalResults.Dispose(totalAccumulatorDependency);

            return totalAccumulatorDependency;
        }
    }
}