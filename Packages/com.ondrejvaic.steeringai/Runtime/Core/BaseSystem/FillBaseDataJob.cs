using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace SteeringAI.Core
{
    [BurstCompile]
    struct FillBaseDataJob : IJobParallelFor
    {
        [ReadOnly] public ComponentTypeHandle<LocalToWorld> LocalToWorldTypeHandle;
        [ReadOnly] public ComponentTypeHandle<VelocityComponent> VelocityTypeHandle;
        [ReadOnly] public ComponentTypeHandle<RadiusComponent> RadiusTypeHandle;
        [ReadOnly] public ComponentTypeHandle<MaxSpeedComponent> SpeedSettingsTypeHandle;
        [ReadOnly] public NativeArray<int> ChunkBaseIndexArray;
        [ReadOnly] public NativeArray<ArchetypeChunk> ArchetypeChunks;
        
        [WriteOnly] [NativeDisableParallelForRestriction] public NativeArray<LocalToWorld> LocalToWorlds;
        [WriteOnly] [NativeDisableParallelForRestriction] public NativeArray<Indexes> IndexesArray;
        [WriteOnly] [NativeDisableParallelForRestriction] public NativeArray<VelocityComponent> Velocities;
        [WriteOnly] [NativeDisableParallelForRestriction] public NativeArray<float> Radii;
        [WriteOnly] [NativeDisableParallelForRestriction] public NativeArray<float> CachedSpeeds;
        [WriteOnly] [NativeDisableParallelForRestriction] public NativeArray<float> MaxSpeeds;

        public void Execute(int chunkIndex)
        {
            var chunk = ArchetypeChunks[chunkIndex];
            int chunkBaseIndex = ChunkBaseIndexArray[chunkIndex];
            
            var transforms = chunk.GetNativeArray(ref LocalToWorldTypeHandle);
            var velocities = chunk.GetNativeArray(ref VelocityTypeHandle);
            var radii = chunk.GetNativeArray(ref RadiusTypeHandle);
            var speedSettings = chunk.GetNativeArray(ref SpeedSettingsTypeHandle);
            bool hasSpeedSettings = speedSettings.Length != 0;
            
            for (int i = 0; i < chunk.Count; i++)
            {
                int index = chunkBaseIndex + i;
                IndexesArray[index] = new Indexes
                {
                    ChunkIndex = chunkIndex,
                    EntityInChunkIndex = i,
                    EntityInQueryIndex = index,
                };
                
                LocalToWorlds[index] = transforms[i];
                Velocities[index] = velocities[i];
                CachedSpeeds[index] = math.length(velocities[i].Value);
                Radii[index] = radii[i].Radius;

                if (hasSpeedSettings)
                {
                    MaxSpeeds[index] = speedSettings[i].MaxSpeed;   
                }
                else
                {
                    MaxSpeeds[index] = 0;
                }
            }
        }
    }
}