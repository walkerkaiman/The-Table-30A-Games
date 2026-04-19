using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SteeringAI.Core
{
    public class SteeringEntityAuthoring : MonoBehaviour 
    {
        public float Radius = 0.5f;
        public bool Simulate = true;
        public List<TypeDescription> Tags = new() { new TypeDescription(typeof(DebugTagComponent)) };
        
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, Radius);
        }
    }
    
    public class SteeringEntityBaker : Baker<SteeringEntityAuthoring>
    {
        public override void Bake(SteeringEntityAuthoring authoring)
        {
            var e = GetEntity(authoring, TransformUsageFlags.Dynamic);
            
            AddComponent(e, new VelocityComponent
            {
                Value = new float3(0, 0, 0),
            });
            AddComponent(e, new SteeringEntityTagComponent());

            if (authoring.Simulate)
            {
                AddComponent(e, new IsSimulatingTagComponent());   
            }

            AddComponent(e, new RadiusComponent
            {
                Radius = authoring.Radius
            });
            
            for (int i = 0; i < authoring.Tags.Count; i++)
            {
                Type tag = Type.GetType(authoring.Tags[i].AssemblyQualifiedName);
                if (tag == null)
                {
                    
                    continue;
                }
                
                AddComponent(e, new ComponentType(tag, ComponentType.AccessMode.ReadOnly));
            }
        }
    }
}