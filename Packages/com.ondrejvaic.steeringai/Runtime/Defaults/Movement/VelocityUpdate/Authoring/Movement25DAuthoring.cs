using System;
using SteeringAI.Core;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace SteeringAI.Defaults
{
    public enum MoveState
    {
        Falling,
        Walking
    }

    public struct Movement25DStateComponent : IComponentData
    {
        [HideInInspector] public MoveState MoveState;
        [HideInInspector] public float3 CurrentUp;
        [HideInInspector] public bool IsSteppingUp;
        [HideInInspector] public bool IsSteppingDown;
        [HideInInspector] public float3 StartSteppingVelocity;
        [HideInInspector] public float3 StartSteppingDownVelocity;
        [HideInInspector] public float3 xD; 
        [HideInInspector] public float3 TargetUp;
    }
    
    [Serializable]
    public struct Movement25DComponent : IComponentData, IActivable
    {
        public MovementOptions MoveOptions;
        public RotationOptions25D RotationOptions;
        [HideInInspector] public uint LayerMask;
        
        [Header("Walking")] 
        public float MinAngleToSlide;
        public float MaxAngleToSlide;
        public float MinAngleToSpeedUp;
        public float MaxAngleToSpeedUp;
        public float MaxDownhillSpeedUp;
        public float UpAlignmentAcceleration;
        public float StepHeight;
        public float StepDownGravity;
        
        [Header("Falling")] 
        public float AirGravity;
        public float AirLinearDrag;
        [FormerlySerializedAs("AirForwardAlignmentSpeed")] public float AirAlignmentSpeed;
        
        [field: SerializeField] public bool IsActive { get; set; }
        
#if STEERING_DEBUG
        public bool Debug;
#endif

        public static Movement25DComponent Preset => new()
        {
            MoveOptions = new MovementOptions
            {
                MaxSpeed = 4f,
                MaxForwardAcceleration = 6,
                MaxRightAcceleration = 6,
                Friction = 0.05f,
                Move = true
            },
            RotationOptions = new RotationOptions25D
            {
                SwingRotationSpeed = 3,
                RotationSpeed = 5,
                Rotate = true
            },
            MinAngleToSlide = 25,
            MaxAngleToSlide = 75,
            MinAngleToSpeedUp = 5,
            MaxAngleToSpeedUp = 45,
            MaxDownhillSpeedUp = 1.5f,
            UpAlignmentAcceleration = 100,
            AirGravity = -9.81f,
            AirLinearDrag = 2,
            AirAlignmentSpeed = 80f,
            StepHeight = 0.5f,
            StepDownGravity = -100f,
            IsActive = true,
#if STEERING_DEBUG
            Debug = false,
#endif
        };
    }

    public class Movement25DAuthoring : MonoBehaviour
    {
        public Movement25DComponent Movement25DComponent = Movement25DComponent.Preset;
        public LayerMask GroundLayerMask = ~0; 
        [SerializeField] public bool UpdatePosition = true;
    }

    public class Movement25DBaker : Baker<Movement25DAuthoring>
    {
        public override void Bake(Movement25DAuthoring authoring)
        {
            var e = GetEntity(authoring, TransformUsageFlags.Dynamic);

            authoring.Movement25DComponent.LayerMask = (uint)authoring.GroundLayerMask.value;
            
            AddComponent(e, new Movement25DStateComponent
            {
                CurrentUp = authoring.transform.up,
                TargetUp = authoring.transform.up,
                MoveState = MoveState.Falling
            });
            AddComponent(e, authoring.Movement25DComponent);
            AddComponent(e, new MaxSpeedComponent(authoring.Movement25DComponent.MoveOptions.MaxSpeed));
            if (authoring.UpdatePosition)
            {
                AddComponent(e, new UpdatePositionTag());
            }
        }
    }
}