using System;
using SteeringAI.Core;
using Unity.Entities;
using UnityEngine;

namespace SteeringAI.Defaults
{
    [Serializable]
    public struct SeparationComponent : IComponentData, INeighborBaseBehavior
    {
        [Header("Observability")]
        public float StartAngle;
        public float DistanceP;
        public float AngleP;

        [Header("Activation")]
        public float ActivationP;
        public float ActivationK;

        [field: SerializeField] public NeighborBehaviorData BaseData { get; set; }
        
        public static SeparationComponent Preset => new()
        {
            StartAngle = 72f,
            DistanceP = 2,
            AngleP = 2,
            ActivationP = 6,
            ActivationK = 0.01f,
            BaseData = new NeighborBehaviorData
            {
                Priority = 2,
                MaxDistance = 1.5f,
                MaxAngle = 360,
                DirectionStrength = 0.6f,
                SpeedStrength = 0f,
                IsActive = true,
#if STEERING_DEBUG
                Debug = false,
                DebugScale = 100,
                DebugColor = Color.red
#endif
            }
        };
    }

    [ComponentAuthoring(typeof(SeparationComponent))]
    public class SeparationAuthoring : MonoBehaviour
    {
        public SeparationComponent SeparationComponent = SeparationComponent.Preset;
    }
    
    public class SeparationBaker : Baker<SeparationAuthoring>
    {
        public override void Bake(SeparationAuthoring authoring)
        {
            var e = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(e, authoring.SeparationComponent);
        }
    }
}