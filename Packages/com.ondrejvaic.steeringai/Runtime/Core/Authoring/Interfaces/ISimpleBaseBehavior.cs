using System;
using UnityEngine;

namespace SteeringAI.Core
{
    [Serializable]
    public struct SimpleBehaviorData : IActivable
    {
        public byte Priority;
        public float DirectionStrength;
        public float SpeedStrength;
      
        [field: SerializeField] public bool IsActive { get; set; }
        
#if STEERING_DEBUG
        public bool Debug;
        public Color Color;
        public float DebugScale;
#endif

        public static SimpleBehaviorData Preset => new()
        {
            Priority = 0,
            DirectionStrength = 0.5f,
            SpeedStrength = 0.5f,
            IsActive = true,
#if STEERING_DEBUG
            Debug = false,
            DebugScale = 10,
            Color = Color.green,
#endif
        };
    }
    
    public interface ISimpleBaseBehavior
    {
        public SimpleBehaviorData BaseData { get; set; }
    }
}