using System;
using SteeringAI.Core;
using Unity.Entities;
using UnityEngine;

namespace SteeringAI.Defaults
{
    [Serializable]
    public struct SeekComponent : IComponentData, INeighborBaseBehavior
    {
        public float Speed;
        [field: SerializeField] public NeighborBehaviorData BaseData { get; set; }

        public static SeekComponent Preset => new()
        {
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

    [ComponentAuthoring(typeof(SeekComponent))]
    public class SeekAuthoring : MonoBehaviour
    {
        public SeekComponent SeekComponent = SeekComponent.Preset;
    }
    
    public class SeekingBaker : Baker<SeekAuthoring>
    {
        public override void Bake(SeekAuthoring authoring)
        {
            var e = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(e, authoring.SeekComponent);
        }
    }
}