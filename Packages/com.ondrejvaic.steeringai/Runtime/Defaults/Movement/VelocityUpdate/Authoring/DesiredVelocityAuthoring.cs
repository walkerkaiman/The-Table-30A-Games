using System;
using SteeringAI.Core;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SteeringAI.Defaults
{
    [Serializable]
    public struct DesiredVelocityComponent : IComponentData
    {
        [HideInInspector] public float3 Value;
        
#if STEERING_DEBUG
        public bool Debug;
        public float DebugScale;
        public Color Color;
#endif
    }

    [ComponentAuthoring(typeof(DesiredVelocityComponent))]
    public class DesiredVelocityAuthoring : MonoBehaviour
    {
        public DesiredVelocityComponent DesiredVelocityComponent = new()
        {
#if STEERING_DEBUG
            Debug = false,
            DebugScale = 10,
            Color = Color.white
#endif
        };
    }

    public class DesiredVelocityBaker : Baker<DesiredVelocityAuthoring>
    {
        public override void Bake(DesiredVelocityAuthoring authoring)
        {
            var e = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(e, authoring.DesiredVelocityComponent);
        }
    }
}