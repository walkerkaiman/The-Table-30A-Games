using System.Runtime.CompilerServices;
using SteeringAI.Core;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SteeringAI.Defaults
{
    [BurstCompile]
    public static class WeightFunctions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Observability<ComponentT, OtherComponentT>(
            in NeighborBehaviorData<ComponentT, OtherComponentT> match, float startAngle, float maxAngle, float distanceP, float angleP)
            where ComponentT : unmanaged, INeighborBaseBehavior, IComponentData
            where OtherComponentT : unmanaged, IComponentData
        {
         
            float3 toOther = (match.OtherEntity.Position - match.Entity.Position) / match.DistanceToOrigin;

            float transitionStart = 0.1f;
            float distanceFraction;
            
            if(match.DistanceToSurface >= 0)
            {
                float d = match.DistanceToSurface / match.Entity.Component.BaseData.MaxDistance;
                distanceFraction = math.saturate(math.remap(0, 1, transitionStart, 1, d));
            }
            else
            {
                distanceFraction = math.remap(match.Entity.Radius + match.OtherEntity.Radius, 0, transitionStart, 0, match.DistanceToOrigin);
            }

            float weight = InverseSquare(distanceFraction, distanceP);
            weight *= DotStrengthToZeroNew(match.Entity.Direction, toOther, startAngle, maxAngle, angleP);
            
            return weight;
        }
        
        // alignment dir
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float AngleStrengthToOne(float3 v1, float3 v2, float startAngle, float minWeight, float angleP)
        {
            float angleFraction = calculateAngleFraction(v1, v2);
            return fq1Shaped(angleFraction, angleP, startAngle / 360f, 1, minWeight, 1);
        }
        
        // alignment speed
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SpeedStrengthToOne(float speed1, float speed2, float maxSpeed, float startX, float minWeight, float angleP)
        {
            float diff = math.saturate(math.abs(speed1 - speed2) / maxSpeed);
            
            return fq1Shaped(diff, angleP, startX, 1, minWeight, 1);
        }

        // obs dir
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DotStrengthToZeroNew(float3 v1, float3 v2, float startAngle, float maxAngle, float angleP)
        {
            float angleFraction = calculateAngleFraction(v1, v2);
            return fq2Shaped(angleFraction, angleP, startAngle / 360f, maxAngle / 360f, 0, 1);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float calculateAngleFraction(float3 v1, float3 v2)
        {
            float dot = math.dot(v1, v2);
            dot = math.clamp(dot, -1, 1);
            float angle = math.acos(dot);
            float angleFraction = math.saturate(angle / math.PI);
            
            return angleFraction;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CalculateDistanceFraction(float distance, float maxDistance)
        {
            distance = math.min(math.max(math.EPSILON, distance), maxDistance);
            float distanceFraction = distance / maxDistance;
            
            return distanceFraction;
        }
        
        // sep
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float InverseSquareToOne(float distance, float p, float k)
        {
            return InverseSquareToOne(distance * k, p);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float fq1Shaped(float x, float p, float xStart, float xEnd, float yStart, float yEnd)
        {
#if STEERING_DEBUG
            if (xStart > xEnd)
            {
                Debug.Log($"xStart {xStart} > xEnd {xEnd}");
            }
            
            if (yStart > yEnd)
            {
                Debug.Log($"yStart {yStart} > yEnd {yEnd}");
            }
#endif
            
            return fq1(math.saturate((x - xStart) / (xEnd - xStart)), p) * (yEnd - yStart) + yStart;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float fq2Shaped(float x, float p, float xStart, float xEnd, float yStart, float yEnd)
        {
            
#if STEERING_DEBUG
            if (xStart > xEnd)
            {
                Debug.Log($"xStart {xStart} > xEnd {xEnd}");
            }
            
            if (yStart > yEnd)
            {
                Debug.Log($"yStart {yStart} > yEnd {yEnd}");
            }
#endif
            
            return fq2(math.saturate((x - xStart) / (xEnd - xStart)), p) * (yEnd - yStart) + yStart;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float fq2(float x, float p)
        {
            return 1 - math.pow(x, p);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float fq1(float x, float p)
        {
            return math.pow(x, p);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float fq4(float x, float p)
        {
            return 1 - math.pow(1 - x, p);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float fs3(float x, float p)
        {
            if(x < 0.5f)
            {
                return fq1(2 * x, p) * 0.5f;
            }
                
            return (fq4(2 * x - 1, p) + 1) * 0.5f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Fb(float x, float maxDistance, float p)
        {
            float distanceFraction = CalculateDistanceFraction(x, maxDistance);
            return Fb(distanceFraction, p);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Fb(float x, float p)
        {
            return fs3(1 - math.abs(2 * x - 1), p);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FSpike(float x, float p)
        {
            if (x < 0.5f)
            {
                return fq1(2 * x, p);
            }

            return fq1(2 * (1 - x), p);
        }
        
        
        //obs 1
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float InverseSquare(float t, float p)
        {
            float result = math.pow(t, -p) - 1;
            return result;
        }

        //obs 1
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float InverseSquare(float distance, float maxDistance, float p)
        {
            distance = math.max(math.EPSILON, distance);
            
            float distanceFraction = distance / maxDistance;
            float result = math.pow(distanceFraction, -p) - 1;
            return result;
        }
        
        // obs 2
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SquareToZero(float distance, float maxDistance)
        {
            float distanceFraction = distance / maxDistance;
            float d1 = distanceFraction - 1;
            float result = d1 * d1;
            return result;
        }
        
        // obs dir
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DotStrengthToZero(float3 v1, float3 v2, float offset)
        {
            float result = math.dot(v1, v2) * .5f * (1 - offset) + (1 + offset) * .5f;
            return result;
        }
        
        // obs dir
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DotStrengthToZero(float3 v1, float3 v2, float offset, float maxAngle)
        {
            float a = math.acos(math.dot(v1, v2)) * math.PI / maxAngle;
            float result = math.cos(math.max(math.min(a, math.PI), -math.PI)) * .5f * (1 - offset) + (1 + offset) * .5f;
            return math.saturate(result);
        }
        
        // sep
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float InverseSquareToOne(float distanceFraction, float p)
        {
            float result = -1 / math.pow(distanceFraction + 1, p) + 1;
            return result;
        }

        // coh 1
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float BellCurveSquare(float distance, float maxDistance)
        {
            float distanceFraction = distance / maxDistance;
            float d1 = distanceFraction - 0.5f;
            float result = -4 * d1 * d1 + 1;
            return result;
        }
        
        // coh 2
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float BellCurveQuad(float distance, float maxDistance)
        {
            float distanceFraction = distance / maxDistance;
            float d1 = distanceFraction - 0.5f;
            float result = 16 * d1 * d1 * d1 * d1 - 8 * d1 * d1 + 1;
            return result;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SquareToOne(float distance, float maxDistance)
        {
            float distanceFraction = distance / maxDistance;
            float result = distanceFraction * distanceFraction;
            return result;
        }
    }
}