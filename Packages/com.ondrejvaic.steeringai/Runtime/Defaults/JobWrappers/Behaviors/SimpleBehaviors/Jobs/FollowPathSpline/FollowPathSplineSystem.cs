using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using SteeringAI.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.Splines;

namespace SteeringAI.Defaults
{
    [UpdateAfter(typeof(AfterSteeringSystemsGroup))]
    public partial class FollowPathSplineIndexSystem : SystemBase
    {
        private EntityQuery query;
        
        private FieldInfo fieldInfo1;
        private FieldInfo fieldInfo2;
        
        protected override void OnCreate()
        {
            query = SystemAPI.QueryBuilder().WithAll<FollowPathSplineIndex, LocalToWorld, FollowPathSplineComponent>().Build();
            
            Type type = typeof(NativeSpline);
            fieldInfo1 = type.GetField("m_SegmentLengthsLookupTable", BindingFlags.NonPublic | BindingFlags.Instance);
            fieldInfo2 = type.GetField("m_UpVectorsLookupTable", BindingFlags.NonPublic | BindingFlags.Instance);
        }
        
        protected override void OnUpdate()
        {
            if (FollowPathSplineManager.Instance == null) { return; }
            
            EntityManager.GetAllUniqueSharedComponents(out NativeList<FollowPathSplineIndex> variations, Allocator.Temp);
            
            foreach (var splineIndex in variations)
            {
                query.SetSharedComponentFilter(splineIndex);

                var spline = FollowPathSplineManager.Instance.GetSpline(splineIndex.Index);
                var lengths = FollowPathSplineManager.Instance.GetSplineLengths(splineIndex.Index);
                    
                var dependency = new UpdateSplineData
                {
                    Spline = spline,
                    Lengths = lengths,
                }.ScheduleParallel(query, Dependency);

                spline.Knots.Dispose(dependency);
                spline.Curves.Dispose(dependency);
                
                var segmentLengthsLookupTable = (NativeArray<DistanceToInterpolation>)fieldInfo1.GetValue(spline);
                segmentLengthsLookupTable.Dispose(dependency);
                
                var upVectorsLookupTable = (NativeArray<float3>)fieldInfo2.GetValue(spline);
                upVectorsLookupTable.Dispose(dependency);
                lengths.Dispose(dependency);
                
                Dependency = dependency;
            }
        }
    }

    [BurstCompile]
    public partial struct UpdateSplineData : IJobEntity
    {
        [ReadOnly] public NativeSpline Spline;
        [ReadOnly] public NativeArray<float> Lengths;

        public void Execute(in LocalToWorld transform, ref FollowPathSplineComponent component)
        {
            if (!component.Loops && component.CurveIndex == Spline.Curves.Length - 1)
            {
                component.Dir = new float3(float.NaN, float.NaN, float.NaN);
                return;
            }
            
            BezierCurve curve = Spline.Curves[component.CurveIndex];
            float length = Lengths[component.CurveIndex];
            float uOffset = component.LookAhead / length;

            float eps = 1e-3f;
            float u = ClosestPointOnBezier.Hybrid(component.LastU, component.LastU + uOffset, curve, transform.Position, eps);
            float3 onCurve = BezierHelpers.EvaluateBezier(curve, u);
            float distance = math.distance(onCurve, transform.Position);

            float distanceFraction = math.remap(
                component.DistanceThreshold,
                component.DistanceThreshold + component.DistanceThresholdWidth, 
                0, 1, distance);
            distanceFraction = math.saturate(distanceFraction);
            distanceFraction = WeightFunctions.fq1(distanceFraction, component.Power);
            
            float3 cp = BezierHelpers.EvaluateBezierDerivative(curve, component.LastU);
            float3 alongCurve = math.normalizesafe(cp, transform.Forward);
            
            float3 c = BezierHelpers.EvaluateBezier(curve, component.LastU);
            float3 toCurve = math.normalizesafe(c - transform.Position, transform.Forward);

            float3 direction = math.lerp(alongCurve, toCurve, distanceFraction);
            component.Dir = math.normalizesafe(direction, transform.Forward);
            
            // DebugDraw.DrawArrow(transform.Position, transform.Position + alongCurve, Color.blue);
            // DebugDraw.DrawArrow(transform.Position, transform.Position + toCurve, Color.red);
            // DebugDraw.DrawArrow(transform.Position, transform.Position + component.Dir, Color.yellow);
            
            if (distanceFraction < 1 - eps)
            {
                if (u > 1 - eps) 
                {
                    component.CurveIndex++;
                    component.LastU = 0;
                    if (component.Loops && component.CurveIndex == Spline.Curves.Length)
                    {
                        component.CurveIndex = 0;
                    }
                }
                else
                {
                    component.LastU = u;
                }
            }
        }
    }
}




































