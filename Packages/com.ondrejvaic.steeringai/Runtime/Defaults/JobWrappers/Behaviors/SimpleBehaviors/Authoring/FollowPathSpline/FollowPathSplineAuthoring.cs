using System;
using SteeringAI.Core;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SteeringAI.Defaults
{
    [Serializable]
    public struct FollowPathSplineComponent : IComponentData, ISimpleBaseBehavior
    {
        public float Speed;
        public float DistanceThreshold;
        public float DistanceThresholdWidth;
        public float Power;
        public float LookAhead;
        public bool Loops;

        [HideInInspector] public float3 Dir;
        [HideInInspector] public float LastU;
        [HideInInspector] public int CurveIndex;
        
        [field: SerializeField] public SimpleBehaviorData BaseData { get; set; }

        public static FollowPathSplineComponent Preset => new()
        {
            Speed = 2,
            DistanceThreshold = 3, 
            DistanceThresholdWidth = 2,
            Power = 2,
            LookAhead = 3,
            Loops = true,
            Dir = new float3(float.NaN, float.NaN, float.NaN),
            LastU = 0,
            CurveIndex = 0,
            BaseData = SimpleBehaviorData.Preset,
        };
    }

    public struct FollowPathSplineIndex : ISharedComponentData
    {
        public int Index;
    }
    
    [ComponentAuthoring(typeof(FollowPathSplineComponent))]
    public class FollowPathSplineAuthoring : MonoBehaviour
    {
        public FollowPathSplineComponent FollowPathSplineComponent = FollowPathSplineComponent.Preset;
        public int SplineIndex;
    }
    
    public class FollowPathSplineBaker : Baker<FollowPathSplineAuthoring>
    {
        public override void Bake(FollowPathSplineAuthoring authoring)
        {
            var e = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(e, authoring.FollowPathSplineComponent);
            AddSharedComponent(e, new FollowPathSplineIndex
            {
                Index = authoring.SplineIndex
            });
        }
    }
}
