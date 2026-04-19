using System;
using SteeringAI.Core;
using Unity.Entities;
using UnityEngine;

namespace SteeringAI.Defaults
{
    [Serializable]
    public struct KeepHeightComponent : IComponentData, ISimpleBaseBehavior
    {
        public float MaxY;
        public float MinY;
        public float CalmZoneHeight;
        
        [field: SerializeField] public SimpleBehaviorData BaseData { get; set; }
        public static KeepHeightComponent Preset => new()
        {
            MaxY = 10,
            MinY = 0,
            CalmZoneHeight = 3,
            BaseData = new SimpleBehaviorData
            {
                Priority = 4,
                DirectionStrength = 0.75f,
                SpeedStrength = 0,
                IsActive = true,
#if STEERING_DEBUG
                Debug = false,
                Color = Color.red,
                DebugScale = 100
#endif
            }
        };
    }

    [ComponentAuthoring(typeof(KeepHeightComponent))]
    public class KeepHeightAuthoring : MonoBehaviour
    {
        public KeepHeightComponent KeepHeightComponent = KeepHeightComponent.Preset; 
    }
    
    public class KeepHeightBaker : Baker<KeepHeightAuthoring>
    {
        public override void Bake(KeepHeightAuthoring authoring)
        {
            var e = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(e, authoring.KeepHeightComponent);
        }
    }
}