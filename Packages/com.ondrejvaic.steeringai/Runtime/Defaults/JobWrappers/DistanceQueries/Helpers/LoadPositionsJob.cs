using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace SteeringAI.Defaults
{
    [BurstCompile]
    struct LoadPositionsJob : IJobParallelFor 
    {
        [ReadOnly] public NativeArray<LocalToWorld> LocalToWorlds;
        [NativeDisableParallelForRestriction] [WriteOnly] public NativeArray<float3> Positions;

        public void Execute(int index)
        {
            var localToWorld = LocalToWorlds[index];
            Positions[index] = localToWorld.Position;
        }
    }
}