using System;
using SteeringAI.Core;
using Unity.Entities;
using UnityEngine;

namespace SteeringAI.Defaults
{
    [Serializable]
    public struct SeekingComponent : IComponentData, INeighborBaseBehavior
    {
        public float MaxSpeedOverPrey; 
        public float MinChaseSpeed; 
            
        [field: SerializeField] public NeighborBehaviorData BaseData { get; set; }

        public static SeekingComponent Preset => new()
        {
            MinChaseSpeed = 8,
            MaxSpeedOverPrey = 6,
            BaseData = new NeighborBehaviorData
            {
                Priority = 2,
                MaxDistance = 25,
                MaxAngle = 145,
                DirectionStrength = 0.6f,
                SpeedStrength = 0.5f,
                IsActive = true,
#if STEERING_DEBUG
                Debug = false,
                DebugScale = 100,
                DebugColor = Color.green
#endif
            }
        };
    }

    [ComponentAuthoring(typeof(SeekingComponent))]
    public class SeekingAuthoring : MonoBehaviour
    {
        public SeekingComponent SeekingComponent = SeekingComponent.Preset;
    }
    
    public class SeekingBaker : Baker<SeekingAuthoring>
    {
        public override void Bake(SeekingAuthoring authoring)
        {
            var e = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(e, authoring.SeekingComponent);
        }
    }
}