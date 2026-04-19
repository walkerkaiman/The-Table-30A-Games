using SteeringAI.Core;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace SteeringAI.Defaults
{
	[BurstCompile]
	struct FleeJob : INeighborBehaviorJob<FleeComponent, SteeringEntityTagComponent, SimpleAccumulator, VelocityResult>
	{
		public void Execute(in NeighborBehaviorData<FleeComponent, SteeringEntityTagComponent> pair, ref SimpleAccumulator accumulator)
		{
			if(pair.DistanceToOrigin <= 0) return;
			
			float3 toOther = (pair.OtherEntity.Position - pair.Entity.Position) / pair.DistanceToOrigin;
			float weight = WeightFunctions.InverseSquare(pair.DistanceToOrigin, pair.Entity.Component.BaseData.MaxDistance, 2);
			// weight *= WeightFunctions.DotStrengthToZero(behaviorData.Entity.Forward, toOther, 0, MaxAngle);
			
			accumulator.Add(-toOther * weight, weight);
		}

		public VelocityResult Finalize(in EntityInformation<FleeComponent> entity, in SimpleAccumulator accumulator)
		{
			if(accumulator.SumMagnitude <= 0) return default;
			
			float directionLength = math.length(accumulator.SumVector);
			float3 direction = accumulator.SumVector / directionLength;
			float weight = WeightFunctions.InverseSquareToOne(directionLength, entity.Component.ActivationP, entity.Component.ActivationK);

			float desiredSpeed = math.lerp(entity.Component.MinSpeed, entity.MaxSpeed, weight);
			desiredSpeed = math.max(entity.Speed, desiredSpeed);
			
			var res = new VelocityResult(
				direction,
				weight * entity.Component.BaseData.DirectionStrength,
				desiredSpeed,
				weight * entity.Component.BaseData.SpeedStrength,
				entity.Component.BaseData.Priority);

			return res;
		}
	}
	
	[JobWrapper(typeof(FleeComponent), typeof(SteeringEntityTagComponent))]
	[OutData(typeof(VelocityResults))]
	class FleeJobWrapper : INeighborBehaviorJobWrapper 
	{
		public JobHandle Schedule(
			SystemBase systemBase,
			in NeighborBehaviorParams neighborBehaviorParams,
			out IDelayedDisposable results,
			in JobHandle dependency)
		{
			results = new VelocityResults(neighborBehaviorParams.NeighborQueryParams.MainBaseParams.EntityCount);

			var jobHandle = new FleeJob
			{
			}.Schedule<FleeJob, FleeComponent, SteeringEntityTagComponent, SimpleAccumulator, VelocityResult>(
				systemBase,
				neighborBehaviorParams,
				(VelocityResults)results,
				1,
				dependency);
			
			return jobHandle;
		}
	}
}