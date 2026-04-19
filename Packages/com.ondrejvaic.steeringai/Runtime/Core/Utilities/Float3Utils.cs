using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Mathematics;

namespace SteeringAI.Core
{
    [BurstCompile]
    public struct Float3Utils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNan(float3 vector) =>
            float.IsNaN(vector.x) || float.IsNaN(vector.y) || float.IsNaN(vector.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ClampSize(float3 vector, float minMagnitude, float maxMagnitude)
        {
            float magnitude = math.length(vector);
            if (magnitude < math.EPSILON)
            {
                return vector;
            }

            float newAccelerationMagnitude = math.min(maxMagnitude, magnitude);
            newAccelerationMagnitude = math.max(newAccelerationMagnitude, minMagnitude);

            var clampedVector = vector * (newAccelerationMagnitude / magnitude);
            return clampedVector;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DirectionSimilarity(float3 a, float3 b)
        {
            var max = math.max((1 + math.dot(a, b)) * .5f, 0);
            return max;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ProjectOnPlane(float3 vector, float3 planeUp)
        {
            var projectedVector = vector - planeUp * math.dot(vector, planeUp);
            return projectedVector;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 Rescale(float3 vector, float targetLength)
        {
            return math.normalizesafe(vector) * targetLength;
        }
    }
}