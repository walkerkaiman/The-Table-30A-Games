using System;
using SteeringAI.Core;
using Unity.Entities;
using UnityEngine;

namespace SteeringAI.Defaults
{
    [Serializable]
    public struct AlignUpComponent : IComponentData, ISimpleBaseBehavior
    {
        [field: SerializeField] public SimpleBehaviorData BaseData { get; set; }

        public static AlignUpComponent Preset => new AlignUpComponent
        {
            BaseData = new SimpleBehaviorData
            {
                Priority = 0,
                DirectionStrength = 0.2f,
                SpeedStrength = 0,
                IsActive = true,
#if STEERING_DEBUG
                Debug = false,
                Color = Color.cyan,
                DebugScale = 100
#endif
            }
        };
    }

    [ComponentAuthoring(typeof(AlignUpComponent))]
    public class AlignUpAuthoring : MonoBehaviour
    {
        public AlignUpComponent AlignUpComponent = AlignUpComponent.Preset;
    }
    
    public class AlignUpBaker : Baker<AlignUpAuthoring>
    {
        public override void Bake(AlignUpAuthoring authoring)
        {
            var e = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(e, authoring.AlignUpComponent);
        }
    }
}