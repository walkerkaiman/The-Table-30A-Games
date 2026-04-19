using System;
using SteeringAI.Core;
using Unity.Entities;
using UnityEngine;

namespace SteeringAI.Defaults
{
    [Serializable]
    public struct CohesionComponent : IComponentData, INeighborBaseBehavior
    {
        [Header("Observability")]
        public float StartAngle;
        public float DistanceP;
        public float AngleP;

        [Header("Activation")]
        public float ActivationP;
        public float MinActivation;
        public float MinSpeed;
        
        [field: SerializeField] public NeighborBehaviorData BaseData { get; set; }

        public static CohesionComponent Preset => new()
        {
            StartAngle = 72f,
            DistanceP = 2,
            AngleP = 2,
            ActivationP = 2,
            MinActivation = 0.05f,
            MinSpeed = 5,
            BaseData = new NeighborBehaviorData
            {
                Priority = 1,
                MaxDistance = 20,
                MaxAngle = 315,
                DirectionStrength = 0.8f,
                SpeedStrength = 0.3f,
                IsActive = true,
#if STEERING_DEBUG
                Debug = false,
                DebugScale = 100,
                DebugColor = Color.green
#endif
            }   
        };
    }

    [ComponentAuthoring(typeof(CohesionComponent))]
    public class CohesionAuthoring : MonoBehaviour
    {
        public CohesionComponent CohesionComponent = CohesionComponent.Preset;
    }
    
    public class CohesionBaker : Baker<CohesionAuthoring>
    {
        public override void Bake(CohesionAuthoring authoring)
        {
            var e = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(e, authoring.CohesionComponent);
        }
    }
}