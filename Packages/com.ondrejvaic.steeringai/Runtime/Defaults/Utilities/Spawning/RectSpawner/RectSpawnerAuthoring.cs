using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SteeringAI.Defaults
{
    public struct RectSpawnerComponent : IComponentData
    {
        public Entity Prefab;
        public int Count;
        public float2 Size;
        public float3 Position;
    }

    public class RectSpawnerAuthoring : MonoBehaviour
    {
        public GameObject Prefab;
        public int Count = 100;
        public Vector2 Size;

#if STEERING_DEBUG && UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if(!UnityEditor.Selection.Contains(gameObject)) return;
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position, new Vector3(Size.x, 0, Size.y));
        }
#endif
    }
    
    public class RectSpawnerBaker : Baker<RectSpawnerAuthoring>
    {
        public override void Bake(RectSpawnerAuthoring authoring)
        {
            var e = GetEntity(authoring, TransformUsageFlags.WorldSpace);

            AddComponent(e, new RectSpawnerComponent
            {
                Prefab = GetEntity(authoring.Prefab, TransformUsageFlags.Dynamic),
                Count = authoring.Count,
                Size = authoring.Size,
                Position = authoring.transform.position,
            });
        }
    }
}