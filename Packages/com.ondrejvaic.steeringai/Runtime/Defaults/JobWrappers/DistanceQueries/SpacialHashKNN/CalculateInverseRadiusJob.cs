using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace SteeringAI.Defaults
{ 
    [BurstCompile]
    public struct CalculateInverseRadiusJob : IJob
    {
        [ReadOnly] public float MaxDistance;   
        [ReadOnly] public NativeArray<float> MaxRadius1;
        [ReadOnly] public NativeArray<float> MaxRadius2;
        [WriteOnly] public NativeArray<float> InverseRadius;
        [WriteOnly] public NativeArray<float> MaxTotalRadius;
    
        public void Execute()
        {
            float maxTotal = MaxDistance + MaxRadius1[0] + MaxRadius2[0];
            MaxTotalRadius[0] = maxTotal;
            InverseRadius[0] = 1 / maxTotal;
        }
    }
}