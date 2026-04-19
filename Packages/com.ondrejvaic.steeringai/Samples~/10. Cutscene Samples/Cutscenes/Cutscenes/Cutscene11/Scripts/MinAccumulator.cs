using System.Runtime.CompilerServices;
using SteeringAI.Core;
using Unity.Mathematics;

namespace SteeringAI.Defaults
{
    public struct MinAccumulator : IAccumulator
    {
        public float MinMagnitude;
        public float3 Direction;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(float3 direction, float magnitude)
        {
            if (magnitude < MinMagnitude)
            {
                MinMagnitude = magnitude;
                Direction = direction;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Init()
        {
            MinMagnitude = float.PositiveInfinity;
        }
    }
}