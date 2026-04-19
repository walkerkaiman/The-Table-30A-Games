using System;
using SteeringAI.Core;
using Unity.Entities;
using UnityEngine;

namespace SteeringAI.Defaults
{
    [Serializable]
    public struct MultiHomingComponent : IComponentData, INeighborBaseBehavior
    {
        public float ActivationP;
        [field: SerializeField] public NeighborBehaviorData BaseData { get; set; }
        
        public static MultiHomingComponent Preset => new()
        {
            ActivationP = 2,
            BaseData = new NeighborBehaviorData
            {
                Priority = 1,
                MaxDistance = 250,
                MaxAngle = 360,
                DirectionStrength = 0.2f,
                SpeedStrength = 0,
                IsActive = true,
#if STEERING_DEBUG
                Debug = false,
                DebugScale = 100,
                DebugColor = Color.magenta
#endif
            }
        };
    }

    [ComponentAuthoring(typeof(MultiHomingComponent))]
    public class MultiHomingAuthoring : MonoBehaviour
    {
        public MultiHomingComponent MultiHomingComponent = MultiHomingComponent.Preset;
    }
    
    public class MultiHomingBaker : Baker<MultiHomingAuthoring>
    {
        public override void Bake(MultiHomingAuthoring authoring)
        {
            var e = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(e, authoring.MultiHomingComponent);
        }
    }
}