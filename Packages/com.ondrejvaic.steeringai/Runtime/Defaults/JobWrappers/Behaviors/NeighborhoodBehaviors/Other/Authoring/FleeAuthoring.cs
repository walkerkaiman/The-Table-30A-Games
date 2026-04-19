using System;
using SteeringAI.Core;
using Unity.Entities;
using UnityEngine;

namespace SteeringAI.Defaults
{
    [Serializable]
    public struct FleeComponent : IComponentData, INeighborBaseBehavior
    {
        public float MinSpeed;
        public float ActivationP;
        public float ActivationK;
        
        [field: SerializeField] public NeighborBehaviorData BaseData { get; set; }
        
        public static FleeComponent Preset => new()
        {
            MinSpeed = 6f,
            ActivationP = 6,
            ActivationK = 0.01f,
            BaseData = new NeighborBehaviorData
            {
                Priority = 2,
                MaxDistance = 15,
                MaxAngle = 315,
                DirectionStrength = 0.8f,
                SpeedStrength = 0.7f,
                IsActive = true,
#if STEERING_DEBUG
                Debug = false,
                DebugScale = 100,
                DebugColor = Color.red
#endif
            }
        };
    }

    [ComponentAuthoring(typeof(FleeComponent))]
    public class FleeAuthoring : MonoBehaviour
    {
        public FleeComponent FleeComponent = FleeComponent.Preset;
    }
    
    public class FleeBaker : Baker<FleeAuthoring>
    {
        public override void Bake(FleeAuthoring authoring)
        {
            var e = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(e, authoring.FleeComponent);
        }
    }
}