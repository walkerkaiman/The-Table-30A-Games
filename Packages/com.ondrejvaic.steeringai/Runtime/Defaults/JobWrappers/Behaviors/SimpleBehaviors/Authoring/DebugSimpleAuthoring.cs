using System;
using SteeringAI.Core;
using Unity.Entities;
using UnityEngine;

namespace SteeringAI.Defaults
{
    [Serializable]
    public struct DebugSimpleComponent : IComponentData, ISimpleBaseBehavior
    {
        [field: SerializeField] public SimpleBehaviorData BaseData { get; set; }

        public static DebugSimpleComponent Preset => new DebugSimpleComponent
        {
            BaseData = new SimpleBehaviorData
            {
                Priority = 0,
                DirectionStrength = 0f,
                SpeedStrength = 0,
                IsActive = true,
#if STEERING_DEBUG
                Debug = false,
                Color = Color.yellow,
                DebugScale = 1
#endif
            }
        };
    }

    [ComponentAuthoring(typeof(DebugSimpleComponent))]
    public class DebugSimpleAuthoring : MonoBehaviour
    {
        public DebugSimpleComponent DebugSimpleComponent = DebugSimpleComponent.Preset;
    }
    
    public class DebugSimpleBaker : Baker<DebugSimpleAuthoring>
    {
        public override void Bake(DebugSimpleAuthoring authoring)
        {
            var e = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(e, authoring.DebugSimpleComponent);
        }
    }
}