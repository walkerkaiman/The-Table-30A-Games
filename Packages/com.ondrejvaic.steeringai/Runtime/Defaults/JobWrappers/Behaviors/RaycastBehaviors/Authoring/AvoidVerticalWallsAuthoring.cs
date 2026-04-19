using System;
using SteeringAI.Core;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SteeringAI.Defaults
{
    [Serializable]
    public struct AvoidVerticalWallsComponent : IComponentData, IRayBaseBehavior
    {
        [Header("Observability")]
        public float StartAngle;
        public float DistanceP;
        public float AngleP;
        public float MaxAngle;
            
        [Header("Activation")]
        public float ActivationP;
        public float ActivationK;
        
        public float MinSlope;
        public float SlopeActivationP;
            
        [field: SerializeField] public RayBehaviorData BaseData { get; set; }
        
        public static AvoidVerticalWallsComponent Preset => new()
        {
            MinSlope = 25f,
            SlopeActivationP = 6,
            StartAngle = 30f,
            DistanceP = 2,
            AngleP = 2,
            ActivationP = 3,
            ActivationK = 0.003f,
            MaxAngle = 270,
            BaseData = RayBehaviorData.Preset
        };
    }
    
    [ComponentAuthoring(typeof(AvoidVerticalWallsComponent))]
    public class AvoidVerticalWallsAuthoring : MonoBehaviour
    {
        public AvoidVerticalWallsComponent AvoidVerticalWallsComponent = AvoidVerticalWallsComponent.Preset;
		
        public class AvoidVerticalWallsBaker : Baker<AvoidVerticalWallsAuthoring>
        {
            public override void Bake(AvoidVerticalWallsAuthoring authoring)
            {
                var e = GetEntity(authoring, TransformUsageFlags.Dynamic);
                var component = authoring.AvoidVerticalWallsComponent;
                component.MinSlope = authoring.AvoidVerticalWallsComponent.MinSlope / 180 * math.PI;
                AddComponent(e, component);
            }
        }
    }
}
