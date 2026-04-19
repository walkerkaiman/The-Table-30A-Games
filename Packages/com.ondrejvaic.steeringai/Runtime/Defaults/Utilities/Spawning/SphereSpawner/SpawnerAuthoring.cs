using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SteeringAI.Defaults
{
    public struct SpawnerComponent : IComponentData
    {
        public Entity Prefab;
        public int Count;
        public float Radius;
        public float3 Position;
        public float3 PositionMask;
    }

    public class SpawnerAuthoring : MonoBehaviour
    {
        public GameObject Prefab;
        public int Count = 100;
        public float Radius = 10;
        public float3 PositionMask = new float3(1, 1, 1);

#if STEERING_DEBUG && UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if(!UnityEditor.Selection.Contains(gameObject)) return;
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, Radius);
        }
#endif
    }
    
    public class SpawnerBaker : Baker<SpawnerAuthoring>
    {
        public override void Bake(SpawnerAuthoring authoring)
        {
            var e = GetEntity(authoring, TransformUsageFlags.WorldSpace);

            AddComponent(e, new SpawnerComponent
            {
                Prefab = GetEntity(authoring.Prefab, TransformUsageFlags.Dynamic),
                Count = authoring.Count,
                Radius = authoring.Radius,
                Position = authoring.transform.position,
                PositionMask = authoring.PositionMask
            });
        }
    }
}