using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace SteeringAI.Defaults
{
    [BurstCompile]
    struct FillVelocityResultsJob : IJobParallelFor
    {
        [ReadOnly] public int SkipLength;
        [ReadOnly] public int WriteIndex;
        [ReadOnly] public NativeArray<VelocityResult> InArray;

        [WriteOnly] [NativeDisableParallelForRestriction] public NativeArray<VelocityResult> OutArray;

        public void Execute(int index)
        {
            OutArray[SkipLength * index + WriteIndex] = InArray[index];
        }
    }
}