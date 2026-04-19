using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine.Splines;

namespace SteeringAI.Defaults
{
    [BurstCompile]
    public static class BezierHelpers
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 EvaluateBezierSecondDerivative(in BezierCurve curve, float u)
        {
            return 6f * (1f - u) * (curve.P2 - 2f * curve.P1 + curve.P0) +
                   6f * u * (curve.P3 - 2f * curve.P2 + curve.P1);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (float, float) EvaluateFirstAndSecondDerivative(in BezierCurve curve, float3 point, float x)
        {
            float3 c = EvaluateBezier(curve, x);
            float3 cPrime = EvaluateBezierDerivative(curve, x);
            float3 cDoublePrime = EvaluateBezierSecondDerivative(curve, x);

            float3 diff = c - point;
            
            float fx = math.dot(diff, cPrime);
            float fpx = math.dot(cPrime, cPrime) + math.dot(diff, cDoublePrime);
            return (fx, fpx);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 EvaluateBezier(in BezierCurve curve, float u)
        {
            float oneMinusU = 1f - u;
            return oneMinusU * oneMinusU * oneMinusU * curve.P0 +
                   3f * oneMinusU * oneMinusU * u * curve.P1 +
                   3f * oneMinusU * u * u * curve.P2 +
                   u * u * u * curve.P3;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 EvaluateBezierDerivative(in BezierCurve curve, float u)
        {
            float oneMinusU = 1f - u;
            return 3f * oneMinusU * oneMinusU * (curve.P1 - curve.P0) +
                   6f * oneMinusU * u * (curve.P2 - curve.P1) +
                   3f * u * u * (curve.P3 - curve.P2);
        }
    }
}