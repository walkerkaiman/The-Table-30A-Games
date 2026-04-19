using System;
using SteeringAI.Core;
using Unity.Entities;
using UnityEngine;

namespace SteeringAI.Defaults
{
    [Serializable]
    public struct DebugRaysComponent : IComponentData, IRayBaseBehavior
    {
        [field: SerializeField] public RayBehaviorData BaseData { get; set; }
        
        public static DebugRaysComponent Preset => new()
        {
            BaseData = new RayBehaviorData
            {
                Priority = 0,
                MaxDistance = 100,
                DirectionStrength = 0f,
                SpeedStrength = 0f,
                IsActive = true,
#if STEERING_DEBUG
                DebugRays = false,
                Debug = false,
                DebugScale = 0,
                Color = Color.clear,
#endif
            }
        };
    }
    
    [ComponentAuthoring(typeof(DebugRaysComponent))]
    public class DebugRaysAuthoring : MonoBehaviour
    {
        public DebugRaysComponent DebugRaysComponent = DebugRaysComponent.Preset;
		
        public class DebugRaysBaker : Baker<DebugRaysAuthoring>
        {
            public override void Bake(DebugRaysAuthoring authoring)
            {
                var e = GetEntity(authoring, TransformUsageFlags.Dynamic);
                AddComponent(e, authoring.DebugRaysComponent);
            }
        }
    }
}