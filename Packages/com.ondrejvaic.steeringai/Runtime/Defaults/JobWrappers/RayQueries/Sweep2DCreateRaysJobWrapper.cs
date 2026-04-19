using System;
using SteeringAI.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace SteeringAI.Defaults
{
	[Serializable]
	public struct SweepRaysSettings
	{
		public float FOV;
		public Vector3 Rotation;
		public Vector3 Offset;

		public static SweepRaysSettings Preset => new SweepRaysSettings
		{
			FOV = 270f,
			Rotation = new Vector3(),
			Offset = new Vector3(),
		};
	}
	
	[BurstCompile]
	struct Sweep2DCreateRaysJob : ICreateRaysJob
	{
		public SweepRaysSettings RayCreationSettings;
		public int NumRays;
		public float MaxDistance;
		public float FOV;
		
		public RayData Execute(int rayIndex, in EntityInformation<SteeringEntityTagComponent> entity)
		{
			float t = 2 * math.PI * rayIndex * FOV / NumRays - FOV * math.PI;

			float3 origin = entity.LocalToWorld.Value.TransformPoint(RayCreationSettings.Offset);

			float3 direction = new float3(math.sin(t), 0, math.cos(t));

			float3 projectedForward = math.normalize(new float3(entity.Forward.x, 0, entity.Forward.z));
			var rotation = quaternion.LookRotation(projectedForward, new float3(0, 1, 0));

			direction = math.mul(rotation, direction);

			return new RayData
			{
				Origin = origin,
				Direction = direction,
				MaxDistance = MaxDistance
			};
		}
	}
	
	[JobWrapper]
	[Serializable]
	public class Sweep2DCreateRaysJobWrapper : ICreateRaysJobWrapper
	{
		[SerializeField] private SweepRaysSettings rayCreationSettings = SweepRaysSettings.Preset;
		
		public JobHandle Schedule(
			SystemBase systemBase,
			in CreateRaysParams createRaysParams,
			in NativeArray<RayData> rayDatas,
			JobHandle dependency)
		{
			return new Sweep2DCreateRaysJob
			{
				RayCreationSettings = rayCreationSettings,
				NumRays = createRaysParams.RaySettings.NumRays,
				MaxDistance = createRaysParams.RaySettings.MaxDistance,
				FOV = rayCreationSettings.FOV / 360f,
			}.Schedule(
				createRaysParams,
				rayDatas,
				1,
				dependency);
		}
	}
}