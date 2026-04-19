 using System;
using SteeringAI.Core;
using Unity.Entities;
using UnityEngine;

namespace SteeringAI.Defaults
{
    [Serializable]
    public struct EnvironmentAvoidanceComponent : IComponentData, IRayBaseBehavior
    {
        [Header("Observability")]
        public float StartAngle;
        public float DistanceP;
        public float AngleP;
        public float MaxAngle;
            
        [Header("Activation")]
        public float ActivationP;
        public float ActivationK;
        
        [field: SerializeField] public RayBehaviorData BaseData { get; set; }
        
        public static EnvironmentAvoidanceComponent Preset => new()
        {
            StartAngle = 30f,
            DistanceP = 2,
            AngleP = 2,
            ActivationP = 3,
            ActivationK = 0.003f,
            MaxAngle = 270,
            BaseData = RayBehaviorData.Preset
        };
    }

    [ComponentAuthoring(typeof(EnvironmentAvoidanceComponent))]
    public class EnvironmentAvoidanceAuthoring : MonoBehaviour
    {
        public EnvironmentAvoidanceComponent EnvironmentAvoidanceComponent = EnvironmentAvoidanceComponent.Preset;
    }
    
    public class EnvironmentAvoidanceBaker : Baker<EnvironmentAvoidanceAuthoring>
    {
        public override void Bake(EnvironmentAvoidanceAuthoring authoring)
        {
            var e = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(e, authoring.EnvironmentAvoidanceComponent);
        }
    }
}
