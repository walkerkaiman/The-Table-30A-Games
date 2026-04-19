using System;
using UnityEngine;

namespace SteeringAI.Core
{
    [Serializable]
    public struct RayBehaviorData : IActivable
    {
        public byte Priority;
        public float MaxDistance;
        public float DirectionStrength;
        public float SpeedStrength;
        
        [field: SerializeField] public bool IsActive { get; set; }
        
#if STEERING_DEBUG
        public bool DebugRays;
        public bool Debug;
        public float DebugScale;
        public Color Color;
#endif
        
        public static RayBehaviorData Preset => new()
        {
            Priority = 6,
            MaxDistance = 2,  
            DirectionStrength = 0.7f,
            SpeedStrength = 0f,
            IsActive = true,
#if STEERING_DEBUG
            DebugRays = false,
            Debug = false,
            DebugScale = 100,
            Color = Color.green,
#endif
        };
    }
    
    public interface IRayBaseBehavior
    {
        public RayBehaviorData BaseData { get; set; }
    }
}