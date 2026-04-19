using System.Runtime.CompilerServices;
using SteeringAI.Core;
using Unity.Mathematics;

namespace SteeringAI.Defaults
{
    public struct SimpleAccumulator : IAccumulator
    {
        public float3 SumVector;
        public float SumMagnitude;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(float3 vector, float magnitude)
        {
            SumVector += vector;
            SumMagnitude += magnitude;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Init()
        {
            SumVector = new float3();
            SumMagnitude = 0;
        }
    }
}