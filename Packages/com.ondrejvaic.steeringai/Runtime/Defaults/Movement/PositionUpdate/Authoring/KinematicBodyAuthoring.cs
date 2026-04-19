using System;
using Unity.Entities;
using UnityEngine;

namespace SteeringAI.Defaults
{
    [Serializable]
    public struct CollideAndSlideComponent : IComponentData
    {
        public float SkinWidth;
        public LayerMask LayerMask;
        
#if STEERING_DEBUG
        public bool Debug;
#endif

        public static CollideAndSlideComponent Preset => new()
        {
            SkinWidth = 0.01f,
            LayerMask = ~0,
            
#if STEERING_DEBUG
            Debug = false
#endif
        };
    }

    public class KinematicBodyAuthoring : MonoBehaviour
    {
        public bool IsActive = true;
        public CollideAndSlideComponent CollideAndSlideComponent = CollideAndSlideComponent.Preset;
    }
    
    public class KinematicBodyBaker : Baker<KinematicBodyAuthoring>
    {
        public override void Bake(KinematicBodyAuthoring authoring)
        {
            var e = GetEntity(authoring, TransformUsageFlags.Dynamic);

            if (authoring.IsActive)
            {
                AddComponent(e, authoring.CollideAndSlideComponent);
            }
        }
    }
}