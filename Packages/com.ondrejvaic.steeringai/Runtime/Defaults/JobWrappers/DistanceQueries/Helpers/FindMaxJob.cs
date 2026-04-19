using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace SteeringAI.Defaults
{
    [BurstCompile]
    public struct FindMaxJob : IJob
    {
        [ReadOnly] public NativeArray<float> RadiusArray;
        public NativeArray<float> Max;
        
        public void Execute()
        {
            float max = float.NegativeInfinity;
            for (int i = 0; i < RadiusArray.Length; i++)
            {
                max = math.max(max, RadiusArray[i]);
            }
            
            Max[0] = max;
        }
    }
}