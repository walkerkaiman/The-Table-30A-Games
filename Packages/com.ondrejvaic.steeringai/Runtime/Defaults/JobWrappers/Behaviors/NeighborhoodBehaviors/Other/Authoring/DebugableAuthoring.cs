using System;
using SteeringAI.Core;
using Unity.Entities;
using UnityEngine;

namespace SteeringAI.Defaults
{
	[Serializable]
	public struct DebugableComponent : IComponentData, INeighborBaseBehavior
	{
		[field: SerializeField] public NeighborBehaviorData BaseData { get; set; }
		
		public static DebugableComponent Preset = new()
		{
			BaseData = new NeighborBehaviorData
			{
				Priority = 0,
				MaxDistance = 100,
				MaxAngle = 360,
				DirectionStrength = 0f,
				SpeedStrength = 0f,
				IsActive = true,
#if STEERING_DEBUG
				Debug = true,
				DebugScale = 100,
				DebugColor = Color.white,
#endif
			}
		};
	}

	[ComponentAuthoring(typeof(DebugableComponent))]
	public class DebugableAuthoring : MonoBehaviour
	{
		public DebugableComponent DebugableComponent = DebugableComponent.Preset;
	}
    
	public class DebugableBaker : Baker<DebugableAuthoring>
	{
		public override void Bake(DebugableAuthoring authoring)
		{
			var e = GetEntity(authoring, TransformUsageFlags.Dynamic);
			AddComponent(e, authoring.DebugableComponent);
		}
	}
}