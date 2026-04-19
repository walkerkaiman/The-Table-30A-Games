using System;
using SteeringAI.Core;
using Unity.Entities;
using UnityEngine;

namespace SteeringAI.Defaults
{
    [Serializable]
    public struct Movement3DComponent : IComponentData, IActivable
    {
        public MovementOptions MoveOptions;
        public RotationOptions3D RotationOptions;
        public float BankingGravity;
        
        [HideInInspector] public float ZAxisAngle;
        
        [field: SerializeField] public bool IsActive { get; set; }
        
#if STEERING_DEBUG
        public bool Debug;
#endif
        public static Movement3DComponent Preset => new()
        {
            MoveOptions = new MovementOptions
            {
                MaxSpeed = 15,
                MaxForwardAcceleration = 20,
                MaxRightAcceleration = 20,
                Friction = 0.05f,
                Move = true,
            },
            RotationOptions = new RotationOptions3D
            {
                RotationSpeed = 150,
                RollRotationSpeed = 150,
                Rotate = true
            },
            BankingGravity = -0.5f,
            IsActive = true,
#if STEERING_DEBUG
            Debug = false,
#endif
        };
    }
    
    public class Movement3DAuthoring : MonoBehaviour
    {
        public Movement3DComponent Movement3DComponent = Movement3DComponent.Preset;
        [SerializeField] public bool UpdatePosition = true;
    }
        
    public class Movement3DBaker : Baker<Movement3DAuthoring>
    {
        public override void Bake(Movement3DAuthoring authoring)
        {
            var e = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(e, authoring.Movement3DComponent);
            AddComponent(e, new MaxSpeedComponent(authoring.Movement3DComponent.MoveOptions.MaxSpeed));
            if (authoring.UpdatePosition)
            {
                AddComponent(e, new UpdatePositionTag());
            }
        }
    }
}