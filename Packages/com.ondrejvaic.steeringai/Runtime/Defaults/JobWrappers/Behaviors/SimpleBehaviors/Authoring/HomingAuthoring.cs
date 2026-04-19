using System;
using SteeringAI.Core;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SteeringAI.Defaults
{
    [Serializable]
    public struct HomingComponent : IComponentData, ISimpleBaseBehavior
    {
        public float MinRadius;
        public float MaxRadius;
        public float3 HomePosition;
        public float ActivationP;
        [field: SerializeField] public SimpleBehaviorData BaseData { get; set; }

        public static HomingComponent Preset => new()
        {
            MinRadius = 15,
            MaxRadius = 30,
            HomePosition = new float3(0, 0, 0),
            ActivationP = 2,
            BaseData = new SimpleBehaviorData
            {
                Priority = 2,
                DirectionStrength = 0.2f,
                SpeedStrength = 0,
                IsActive = true,
#if STEERING_DEBUG
                Debug = false,
                Color = Color.green,
                DebugScale = 100
#endif
            }
        };
    }

    [ComponentAuthoring(typeof(HomingComponent))]
    public class HomingAuthoring : MonoBehaviour
    {
        public HomingComponent HomingComponent = HomingComponent.Preset;
    }
    
    public class HomingBaker : Baker<HomingAuthoring>
    {
        public override void Bake(HomingAuthoring authoring)
        {
            var e = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(e, authoring.HomingComponent);
        }
    }
}