using System.Runtime.CompilerServices;
using SteeringAI.Core;
using Unity.Mathematics;

namespace SteeringAI.Defaults
{
    public struct MaxAccumulator : IAccumulator
    {
        public float MaxMagnitude;
        public float3 Direction;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(float3 direction, float magnitude)
        {
            if (magnitude > MaxMagnitude)
            {
                MaxMagnitude = magnitude;
                Direction = direction;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Init()
        {
            MaxMagnitude = float.NegativeInfinity;
        }
    }
}