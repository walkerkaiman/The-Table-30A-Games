using System;
using SteeringAI.Core;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SteeringAI.Defaults
{
    [Serializable]
    public struct WanderingComponent : IComponentData, ISimpleBaseBehavior
    {
        public float MaxUpDownAngle;
        public float XFrequency;
        public float YFrequency;
        
        public float SpeedFrequency;
        public float MinSpeed;
        public float MaxSpeed;
        
        public float DesireFrequency;
        public float2 DesireMultiplierRange;
        
        [field: SerializeField] public SimpleBehaviorData BaseData { get; set; }

        public static WanderingComponent Preset => new()
        {
            MaxUpDownAngle = 45,
            XFrequency = 0.05f,
            YFrequency = 0.03f,
            SpeedFrequency = 0.01f,
            MinSpeed = 5,
            MaxSpeed = 6.5f,
            DesireFrequency = 0.01f,
            DesireMultiplierRange = new float2(1f, 1f),
            BaseData = new SimpleBehaviorData
            {
                Priority = 0,
                DirectionStrength = 0.03f,
                SpeedStrength = 0.1f,
                IsActive = true,
#if STEERING_DEBUG
                Debug = false,
                Color = Color.yellow,
                DebugScale = 100
#endif
            }
        };
    }

    [ComponentAuthoring(typeof(WanderingComponent))]
    public class WanderingAuthoring : MonoBehaviour
    {
        public WanderingComponent WanderingComponent = WanderingComponent.Preset;
    }
    
    public class WanderingBaker : Baker<WanderingAuthoring>
    {
        public override void Bake(WanderingAuthoring authoring)
        {
            var e = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(e, authoring.WanderingComponent);
        }
    }
}