using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine.Splines;

namespace SteeringAI.Defaults
{
    [BurstCompile]
    public static class ClosestPointOnBezier
    {
        // Credit for root finding method "NewtSafe" goes to "osveliz"
        // The methods "Hybrid" and "ForceBis" are published under MIT license at:
        // https://github.com/osveliz/numerical-veliz/blob/master/src/rootfinding/NewtSafe.adb
        //
        // For this purpose the code was translated from Ada into C# and slightly modified
        //
        // MIT License
        // Copyright (c) 2018 osveliz
        // Permission is hereby granted, free of charge, to any person obtaining a copy
        // of this software and associated documentation files (the "Software"), to deal
        // in the Software without restriction, including without limitation the rights
        // to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
        // copies of the Software, and to permit persons to whom the Software is
        // furnished to do so, subject to the following conditions:
        //
        // The above copyright notice and this permission notice shall be included in all
        // copies or substantial portions of the Software.
        //
        // THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
        // IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
        // FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
        // AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
        // LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
        // OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
        // SOFTWARE.
        public static float Hybrid(float initA, float initB, BezierCurve curve, float3 point, float eps = 1e-3f)
        {
            float a = math.min(initA, 1);
            float b = math.min(initB, 1);
            float fa = f(curve, point, a);
            float fb = f(curve, point, b);
            
            if (math.abs(a - b) < eps)
                return a;

            if (math.abs(fa) < eps) return a;
            if (math.abs(fb) < eps) return b;

            if (fa * fb > 0.0)
            {
                if (F(curve, point, a) < F(curve, point, b))
                    return a;
                
                return b;
            }

            float oldx = b;
            float x = (a + b) / 2.0f;
            
            (float fx, float fpx) = BezierHelpers.EvaluateFirstAndSecondDerivative(curve, point, x);
            
            while (math.abs(fx) > eps)
            {
                if (fpx <= 0.0)
                    return ForceBis(a, b, curve, point);

                float lastStep = math.abs(x - oldx);

                if (math.abs(fx * 2.0) > math.abs(lastStep * fpx))
                    return ForceBis(a, b, curve, point);

                oldx = x;
                x -= fx / fpx;

                if (math.abs(x - oldx) < eps)
                {
                    return x;
                }

                if (x <= a || x >= b)
                    return ForceBis(a, b, curve, point);
                
                (fx, fpx) = BezierHelpers.EvaluateFirstAndSecondDerivative(curve, point, x);
                if (fx * fa < 0.0)
                {
                    b = x;
                    // fb = fx;
                }
                else
                {
                    a = x;
                    fa = fx;
                }
            }
            
            return x;
        }

        static float ForceBis(float a, float b, BezierCurve curve, float3 point)
        {
            float c = (a + b) / 2.0f;

            if (f(curve, point, a) * f(curve, point, c) < 0.0f)
                return Hybrid(a, c, curve, point);
            
            return Hybrid(c, b, curve, point);
        }
        
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float f(in BezierCurve curve, float3 point, float u)
        {
            float3 c = BezierHelpers.EvaluateBezier(curve, u);
            float3 cPrime = BezierHelpers.EvaluateBezierDerivative(curve, u);
            float3 diff = c - point;
                
            // Newton-Raphson: f(u) = (C(u) - P) · C'(u) = 0
            // f'(u) = C'(u) · C'(u) + (C(u) - P) · C''(u)
            float f = math.dot(diff, cPrime);
            return f;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float F(in BezierCurve curve, float3 point, float u)
        {
            float3 c = BezierHelpers.EvaluateBezier(curve, u);
            float dist = math.distancesq(c, point);
            return dist;
        }
    }
}