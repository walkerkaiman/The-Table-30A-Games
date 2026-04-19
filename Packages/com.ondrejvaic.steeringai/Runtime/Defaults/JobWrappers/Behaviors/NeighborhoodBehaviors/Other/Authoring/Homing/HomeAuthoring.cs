using System;
using SteeringAI.Core;
using Unity.Entities;
using UnityEngine;

namespace SteeringAI.Defaults
{
    [Serializable]
    public struct HomeComponent : IComponentData
    {
        public float MinRadius;
        public float MaxRadius;
        public float StrengthMultiplier;

        public static HomeComponent Preset => new()
        {
            MinRadius = 10,
            MaxRadius = 25,
            StrengthMultiplier = 1   
        };
    }

    [ComponentAuthoring(typeof(HomeComponent))]
    public class HomeAuthoring : MonoBehaviour
    {
        public HomeComponent HomeComponent = HomeComponent.Preset;
        
#if STEERING_DEBUG && UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (UnityEditor.Selection.Contains(gameObject) ||
                (gameObject.transform.parent != null && UnityEditor.Selection.Contains(gameObject.transform.parent.gameObject)))
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(transform.position, HomeComponent.MinRadius);
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(transform.position, HomeComponent.MaxRadius);   
                
            }
        }
#endif
    }
    
    public class HomeBaker : Baker<HomeAuthoring>
    {
        public override void Bake(HomeAuthoring authoring)
        {
            var e = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(e, authoring.HomeComponent);
        }
    }
}