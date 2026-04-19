using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace SteeringAI.Defaults
{
    [BurstCompile]
    public struct AnimationCurveUnmanaged
    {
        [NativeDisableParallelForRestriction] [ReadOnly] private NativeArray<float> values;

        private readonly int numSamples;
        private readonly float startTime;
        private readonly float animationLength;
        
        public AnimationCurveUnmanaged(AnimationCurve animationCurve, int numSamples)
        {
            if(numSamples < 2)
                throw new System.ArgumentException("numSamples must be greater than 1");

            this.numSamples = numSamples; 
            this.values = new NativeArray<float>(numSamples, Allocator.TempJob);
            this.startTime = animationCurve[0].time;
            this.animationLength = animationCurve[animationCurve.length - 1].time - startTime;
            
            for (int i = 0; i < numSamples; i++)
            {
                float t = startTime + (float)i / (numSamples - 1) * animationLength;
                this.values[i] = animationCurve.Evaluate(t);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Sample(float t)
        {
            float normalizedTime = (t - startTime) / animationLength;
            float sampleT = normalizedTime * (numSamples - 1);
            float sampleTFloor = math.floor(sampleT);
            float interpolationT = sampleT - sampleTFloor;
            
            int index = (int)math.clamp(sampleTFloor, 0, numSamples - 1);
            int index2 = (int)math.clamp(math.ceil(sampleT), 0, numSamples - 1);
            
            return math.lerp(values[index], values[index2], interpolationT);
        }
        
        public void Dispose()
        {
            values.Dispose();
        }
        
        public void Dispose(JobHandle dependency)
        {
            values.Dispose(dependency);
        }
    }
}