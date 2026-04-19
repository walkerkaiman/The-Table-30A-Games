using System;
using SteeringAI.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace SteeringAI.Defaults
{
	[BurstCompile]
	struct DownSingleCreateRayJob : ICreateRaysJob
	{
		public float MaxDistance;
		
		public RayData Execute(int rayIndex, in EntityInformation<SteeringEntityTagComponent> entity)
		{
			return new RayData
			{
				Origin = entity.Position,
				Direction = new float3(0, -1, 0),
				MaxDistance = MaxDistance 
			};
		}
	} 
	
	[JobWrapper]
	[Serializable]
	public class DownSingleCreateRayJobWrapper : ICreateRaysJobWrapper
	{ 
		public JobHandle Schedule(
			SystemBase systemBase,
			in CreateRaysParams createRaysParams,
			in NativeArray<RayData> rayDatas,
			JobHandle dependency)
		{
			return new DownSingleCreateRayJob
			{
				MaxDistance = createRaysParams.RaySettings.MaxDistance 
			}.Schedule(
				createRaysParams,
				rayDatas,
				1,
				dependency);
		}
	}
}