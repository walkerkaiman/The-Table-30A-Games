using SteeringAI.Core;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;
using RaycastHit = Unity.Physics.RaycastHit;

namespace SteeringAI.Defaults
{
	[BurstCompile]
	struct DebugRaysJob : IRaycastBehaviorJob<DebugRaysComponent, NoneAccumulator, NoneAccumulator, VelocityResult>
	{
		public void OnHit(
			in EntityInformation<DebugRaysComponent> entity,
			in RayData rayData,
			in RaycastHit hit,
			ref NoneAccumulator hitA)
		{
#if STEERING_DEBUG
			DebugDraw.DrawArrow(entity.Position, hit.Position, Color.green);
#endif
		}

		public void OnMiss(in EntityInformation<DebugRaysComponent> entity, in RayData rayData,
			ref NoneAccumulator missA)
		{
#if STEERING_DEBUG
			DebugDraw.DrawArrow(entity.Position, entity.Position + rayData.Direction * rayData.MaxDistance, Color.red);
#endif
		}

		public VelocityResult Finalize(
			in EntityInformation<DebugRaysComponent> entity,
			in NoneAccumulator hitA,
			in NoneAccumulator missA)
		{
			return default;
		}
	}

	[JobWrapper(typeof(DebugRaysComponent))]
	[OutData(typeof(VelocityResults))]
	public class DebugRaysJobWrapper : IRaycastBehaviorJobWrapper
	{
		public JobHandle Schedule(
			SystemBase systemBase,
			in RaycastBehaviorParams rayBehaviorParams,
			out IDelayedDisposable results,
			in JobHandle dependency)
		{
			
			results = new VelocityResults(rayBehaviorParams.MainBaseParams.EntityCount);
			var a = new DebugRaysJob
			{
			}.Schedule<DebugRaysJob, DebugRaysComponent, NoneAccumulator, NoneAccumulator, VelocityResult>(
				systemBase,
				rayBehaviorParams,
				(VelocityResults)results,
				1,
				dependency);
			return a;
		}
	}
}