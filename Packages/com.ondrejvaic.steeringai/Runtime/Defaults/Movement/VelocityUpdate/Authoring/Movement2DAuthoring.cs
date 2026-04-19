using System;
using SteeringAI.Core;
using Unity.Entities;
using UnityEngine;

namespace SteeringAI.Defaults
{
    [Serializable]
    public struct Movement2DComponent : IComponentData
    {
        public MovementOptions MoveOptions;
        public RotationOptions2D RotationOptions;
        public bool IsActive;
        
#if STEERING_DEBUG
        public bool Debug;
#endif
        
        public static Movement2DComponent Preset => new()
        {
            MoveOptions = new MovementOptions
            {
                MaxSpeed = 5,
                MaxForwardAcceleration = 1,
                MaxRightAcceleration = 1,
                Friction = 0.05f,
                Move = true,
            },
            RotationOptions = new RotationOptions2D
            {
                RotationSpeed = 100,
                Rotate = true
            },
            IsActive = true,
#if STEERING_DEBUG
            Debug = false,
#endif
        };
    }

    public class Movement2DAuthoring : MonoBehaviour
    {
        public Movement2DComponent Movement2DComponent = Movement2DComponent.Preset;
        [SerializeField] public bool UpdatePosition = true;
    }
    
    public class Movement2DBaker : Baker<Movement2DAuthoring>
    {
        public override void Bake(Movement2DAuthoring authoring)
        {
            var e = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(e, authoring.Movement2DComponent);
            AddComponent(e, new MaxSpeedComponent(authoring.Movement2DComponent.MoveOptions.MaxSpeed));
            if (authoring.UpdatePosition)
            {
                AddComponent(e, new UpdatePositionTag());
            }
        }
    }
}
