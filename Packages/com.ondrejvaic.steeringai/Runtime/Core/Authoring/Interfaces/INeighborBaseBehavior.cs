using System;
using UnityEngine;

namespace SteeringAI.Core
{
    [Serializable]
    public struct NeighborBehaviorData : IActivable
    {
        public byte Priority;
        public float MaxDistance;
        public float MaxAngle;
        public float DirectionStrength;
        public float SpeedStrength;
        [field: SerializeField] public bool IsActive { get; set; }
        
#if STEERING_DEBUG
        public bool Debug;
        public float DebugScale;
        public Color DebugColor;
#endif
        
        public static NeighborBehaviorData Preset => new()
        {
            Priority = 0,
            MaxDistance = 10,
            MaxAngle = 360,
            DirectionStrength = 0.5f,
            SpeedStrength = 0.5f,
            IsActive = true,
#if STEERING_DEBUG
            Debug = false,
            DebugScale = 100,
            DebugColor = Color.green,
#endif
        };
    }
    
    public interface INeighborBaseBehavior
    {
        public NeighborBehaviorData BaseData { get; set; }
    }
}