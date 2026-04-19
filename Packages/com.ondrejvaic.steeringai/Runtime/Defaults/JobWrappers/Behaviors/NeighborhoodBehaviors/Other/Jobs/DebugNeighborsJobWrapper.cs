using SteeringAI.Core;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;

namespace SteeringAI.Defaults
{
	[BurstCompile]
	struct DebugNeighborsJob : INeighborBehaviorJob<DebugableComponent, SteeringEntityTagComponent, NoneAccumulator, VelocityResult>
	{
		public void Execute(
			in NeighborBehaviorData<DebugableComponent, SteeringEntityTagComponent> pair,
			ref NoneAccumulator accumulator)
		{
#if STEERING_DEBUG
			DebugDraw.DrawArrow(pair.Entity.Position, pair.OtherEntity.Position, pair.Entity.Component.BaseData.DebugColor);
#endif
		}

		public VelocityResult Finalize(in EntityInformation<DebugableComponent> entity, in NoneAccumulator accumulator)
		{
			return default;
		}
	}
	
	[JobWrapper(typeof(DebugableComponent), typeof(SteeringEntityTagComponent))]
	[OutData(typeof(VelocityResults))]
	class DebugNeighborsJobWrapper : INeighborBehaviorJobWrapper
	{
		public JobHandle Schedule(
			SystemBase systemBase,
			in NeighborBehaviorParams neighborBehaviorParams,
			out IDelayedDisposable results,
			in JobHandle dependency)
		{
			results = new VelocityResults(neighborBehaviorParams.NeighborQueryParams.MainBaseParams.EntityCount);
			return new DebugNeighborsJob().Schedule<DebugNeighborsJob, DebugableComponent, SteeringEntityTagComponent, NoneAccumulator, VelocityResult>(
				systemBase,
				neighborBehaviorParams,
				(VelocityResults)results,
				1,
				dependency);
		}
	}
}