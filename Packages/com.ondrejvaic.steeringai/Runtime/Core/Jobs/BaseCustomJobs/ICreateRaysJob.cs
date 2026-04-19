using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

namespace SteeringAI.Core
{
    [JobProducerType(typeof(CreateRaysJobExtensions.CreateRaysJobProducer<>))]
    public interface ICreateRaysJob
    {
        public RayData Execute(int rayIndex, in EntityInformation<SteeringEntityTagComponent> entity);
    }

    [BurstCompile]
    public static class CreateRaysJobExtensions
    {
        internal struct JobWrapper<T>
            where T : struct, ICreateRaysJob
        {
            [ReadOnly] public RaycastSettings RayCreationSettings;
            
            [ReadOnly] public CreateRaysParams CreateRaysParams;
            
            [NativeDisableParallelForRestriction] [WriteOnly] public NativeArray<RayData> RayDatas;

            public T JobData;
        }
        
        public static unsafe JobHandle Schedule<T>(
            this T jobData,
            in CreateRaysParams createRaysParams,
            in NativeArray<RayData> rayDatas,
            int minIndicesPerJobCount,
            JobHandle dependsOn = default)
            where T : struct, ICreateRaysJob
        {
            var jobWrapper = new JobWrapper<T>
            {
                RayCreationSettings = createRaysParams.RaySettings,
                RayDatas = rayDatas,
                CreateRaysParams = createRaysParams,
                JobData = jobData,
            };

            CreateRaysJobProducer<T>.Initialize();
            var reflectionData = CreateRaysJobProducer<T>.reflectionData.Data;
            CollectionHelper.CheckReflectionDataCorrect<T>(reflectionData);

            var scheduleParams = new JobsUtility.JobScheduleParameters(
                UnsafeUtility.AddressOf(ref jobWrapper),
                reflectionData,
                dependsOn,
                ScheduleMode.Parallel);

            return JobsUtility.ScheduleParallelFor(ref scheduleParams, createRaysParams.MainBaseParams.ArchetypeChunks.Length, minIndicesPerJobCount);
        }


        [BurstCompile]
        internal struct CreateRaysJobProducer<T>
            where T : struct, ICreateRaysJob
        {
            internal static readonly SharedStatic<IntPtr> reflectionData = SharedStatic<IntPtr>.GetOrCreate<CreateRaysJobProducer<T>>();

            [BurstDiscard]
            internal static void Initialize()
            {
                if (reflectionData.Data == IntPtr.Zero)
                    reflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(JobWrapper<T>), typeof(T), (ExecuteJobFunction)Execute);
            }

            delegate void ExecuteJobFunction(ref JobWrapper<T> jobWrapper, IntPtr additionalPtr, IntPtr bufferRangePatchData,
                ref JobRanges ranges, int jobIndex);

            [BurstCompile]
            public static unsafe void Execute(ref JobWrapper<T> jobWrapper, IntPtr additionalPtr, IntPtr bufferRangePatchData,
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
                    
                    int numRays = jobWrapper.RayCreationSettings.NumRays;

                    for (int chunkIndex = begin; chunkIndex < end; chunkIndex++)
                    {
                        var archetypeChunk = jobWrapper.CreateRaysParams.MainBaseParams.ArchetypeChunks[chunkIndex];
                        int baseIndex = jobWrapper.CreateRaysParams.MainBaseParams.ChunkBaseIndexArray[chunkIndex];

                        for (int entityInChunkIndex = 0; entityInChunkIndex < archetypeChunk.Count; entityInChunkIndex++)
                        {
                            int entityInQueryIndex = baseIndex + entityInChunkIndex;
                            
                            var entityInformation = new EntityInformation<SteeringEntityTagComponent>
                            {
                                LocalToWorld = jobWrapper.CreateRaysParams.MainBaseParams.LocalToWorlds[entityInQueryIndex],
                                Velocity = jobWrapper.CreateRaysParams.MainBaseParams.Velocities[entityInQueryIndex],
                                Speed = jobWrapper.CreateRaysParams.MainBaseParams.CachedSpeeds[entityInQueryIndex],
                                Chunk = archetypeChunk,
                                Indexes = jobWrapper.CreateRaysParams.MainBaseParams.IndexesArray[entityInQueryIndex],
                            };

                            for (int rayIndex = 0; rayIndex < jobWrapper.RayCreationSettings.NumRays; rayIndex++)
                            {
                                RayData rayData = jobWrapper.JobData.Execute(rayIndex, entityInformation);
                                jobWrapper.RayDatas[entityInQueryIndex * numRays + rayIndex] = rayData;
                            }
                        }
                    }
                }
            }
        }
    }
}
