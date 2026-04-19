using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;

namespace SteeringAI.Core
{
    public struct Indexes
    {
        public int ChunkIndex;
        public int EntityInChunkIndex;
        public int EntityInQueryIndex;
    }
    
    public struct BaseBehaviorParams
    {
        [ReadOnly] public NativeArray<LocalToWorld> LocalToWorlds;
        [ReadOnly] public NativeArray<ArchetypeChunk> ArchetypeChunks;
        [ReadOnly] public NativeArray<int> ChunkBaseIndexArray;
        [ReadOnly] public NativeArray<Indexes> IndexesArray;
        [ReadOnly] public NativeArray<VelocityComponent> Velocities;
        [ReadOnly] public NativeArray<float> CachedSpeeds;
        [ReadOnly] public NativeArray<float> MaxSpeeds;
        [ReadOnly] public NativeArray<float> Radii;
        [ReadOnly] public int EntityCount;
        [ReadOnly] public float DeltaTime;

        public void Dispose(JobHandle dependency)
        {
            LocalToWorlds.Dispose(dependency);
            ArchetypeChunks.Dispose(dependency);
            ChunkBaseIndexArray.Dispose(dependency);
            IndexesArray.Dispose(dependency);
            Velocities.Dispose(dependency);
            Radii.Dispose(dependency);
            CachedSpeeds.Dispose(dependency);
            MaxSpeeds.Dispose(dependency);
        }
    }
}