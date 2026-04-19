using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SteeringAI.Defaults
{
    [Serializable]
    public struct BoxLimiterComponent : IComponentData
    {
        public float3 Size;
    }
    
    public class BoxLimiterAuthoring : MonoBehaviour
    {
        public BoxLimiterComponent BoxLimiterComponent = new()
        {
            Size = new float3(10, 10, 10)
        };
        
#if STEERING_DEBUG && UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if(!UnityEditor.Selection.Contains(gameObject)) return;
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position, BoxLimiterComponent.Size);
        }
#endif
    }
    
    public class BoxLimiterBaker : Baker<BoxLimiterAuthoring>
    {
        public override void Bake(BoxLimiterAuthoring authoring)
        {
            var e = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(e, authoring.BoxLimiterComponent);
        }
    }
}