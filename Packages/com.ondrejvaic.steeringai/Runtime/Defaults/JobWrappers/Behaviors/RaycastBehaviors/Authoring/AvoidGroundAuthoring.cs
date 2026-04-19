using System;
using SteeringAI.Core;
using Unity.Entities;
using UnityEngine;

namespace SteeringAI.Defaults
{
    [Serializable]
    public struct AvoidGroundComponent : IComponentData, IRayBaseBehavior
    {
        [field: SerializeField] public RayBehaviorData BaseData { get; set; }
        
        public static AvoidGroundComponent Preset => new()
        {
            BaseData = new RayBehaviorData
            {
                Priority = 5,
                MaxDistance = 20,
                DirectionStrength = 0.5f,
                SpeedStrength = 0f,
                IsActive = true,
#if STEERING_DEBUG
                DebugRays = false,
                Debug = false,
                DebugScale = 100,
                Color = Color.red,
#endif
            }
        };
    }
    
    [ComponentAuthoring(typeof(AvoidGroundComponent))]
    public class AvoidGroundAuthoring : MonoBehaviour
    {
        public AvoidGroundComponent AvoidGroundComponent = AvoidGroundComponent.Preset;
		
        public class AvoidGroundBaker : Baker<AvoidGroundAuthoring>
        {
            public override void Bake(AvoidGroundAuthoring authoring)
            {
                var e = GetEntity(authoring, TransformUsageFlags.Dynamic);
                AddComponent(e, authoring.AvoidGroundComponent);
            }
        }
    }
}