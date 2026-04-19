using System;
using SteeringAI.Core;
using Unity.Entities;
using UnityEngine;

namespace SteeringAI.Defaults
{
    [Serializable]
    public struct AlignmentComponent : IComponentData, INeighborBaseBehavior
    {
        [Header("Observability")]
        public float StartAngle;
        public float DistanceP;
        public float AngleP;
        
        [Header("Activation")]
        public float StartActivationAngle;
        public float MinAngleWeight;
        public float AngleActivationP;
        
        [Header("Speed activation")]
        public float StartSpeedDiffFraction;
        public float MinSpeedDiffWeight;
        public float SpeedDiffActivationP;
        
        [field: SerializeField] public NeighborBehaviorData BaseData { get; set; }

        public static AlignmentComponent Preset => new()
        {
            StartAngle = 72f,
            DistanceP = 2,
            AngleP = 2,
            StartActivationAngle = 0,
            MinAngleWeight = 0.2f,
            AngleActivationP = 2,
            StartSpeedDiffFraction = 0.1f,
            MinSpeedDiffWeight = 0.2f,
            SpeedDiffActivationP = 2,
            BaseData = new NeighborBehaviorData
            {
                Priority = 1,
                MaxDistance = 5,
                MaxAngle = 270,
                DirectionStrength = 0.6f,
                SpeedStrength = 0.5f,
                IsActive = true,
#if STEERING_DEBUG
                Debug = false,
                DebugScale = 100,
                DebugColor = Color.yellow
#endif
            }
        };
    }

    [ComponentAuthoring(typeof(AlignmentComponent))]
    public class AlignmentAuthoring : MonoBehaviour
    {
        public AlignmentComponent AlignmentComponent = AlignmentComponent.Preset;
    }
    
    public class AlignmentBaker : Baker<AlignmentAuthoring>
    {
        public override void Bake(AlignmentAuthoring authoring)
        {
            var e = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(e, authoring.AlignmentComponent);
        }
    }
}