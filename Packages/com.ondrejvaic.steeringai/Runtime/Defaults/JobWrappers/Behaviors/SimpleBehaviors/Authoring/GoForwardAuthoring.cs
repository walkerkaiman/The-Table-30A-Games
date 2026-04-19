using System;
using SteeringAI.Core;
using Unity.Entities;
using UnityEngine;

namespace SteeringAI.Defaults
{
    [Serializable]
    public struct GoForwardComponent : IComponentData, ISimpleBaseBehavior
    {
        public float Speed;      
        [field: SerializeField] public SimpleBehaviorData BaseData { get; set; }

        public static GoForwardComponent Preset => new()
        {
            Speed = 1,
            BaseData = SimpleBehaviorData.Preset
        };
    }

    [ComponentAuthoring(typeof(GoForwardComponent))]
    public class GoForwardAuthoring : MonoBehaviour
    {
        public GoForwardComponent GoForwardComponent = GoForwardComponent.Preset;
    }
    
    public class GoForwardBaker : Baker<GoForwardAuthoring>
    {
        public override void Bake(GoForwardAuthoring authoring)
        {
            var e = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(e, authoring.GoForwardComponent);
        }
    }
}