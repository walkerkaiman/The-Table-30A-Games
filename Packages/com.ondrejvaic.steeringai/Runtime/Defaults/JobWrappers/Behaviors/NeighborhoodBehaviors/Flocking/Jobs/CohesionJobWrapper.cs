using SteeringAI.Core;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace SteeringAI.Defaults
{
	[BurstCompile]
	struct CohesionJob : INeighborBehaviorJob<CohesionComponent, SteeringEntityTagComponent, SimpleAccumulator, VelocityResult>
	{
		public void Execute(in NeighborBehaviorData<CohesionComponent, SteeringEntityTagComponent> pair, ref SimpleAccumulator accumulator)
		{
			if(pair.DistanceToOrigin <= 0) return;
			
			float weight = WeightFunctions.Observability(
				pair,
				pair.Entity.Component.StartAngle,
				pair.Entity.Component.BaseData.MaxAngle,
				pair.Entity.Component.DistanceP,
				pair.Entity.Component.AngleP);
			
			accumulator.Add(pair.OtherEntity.Position * weight, weight);
		}

		public VelocityResult Finalize(in EntityInformation<CohesionComponent> entity, in SimpleAccumulator accumulator)
		{
			if(accumulator.SumMagnitude <= 0) return default;
			
			float3 centroid = accumulator.SumVector / accumulator.SumMagnitude;
			float3 toCentroid = centroid - entity.Position;
			
			float distanceToCentroid = math.length(toCentroid);
			float distanceFraction = WeightFunctions.CalculateDistanceFraction(distanceToCentroid - entity.Radius, entity.Component.BaseData.MaxDistance);
			float weight = math.max(entity.Component.MinActivation, WeightFunctions.fq1(distanceFraction, entity.Component.ActivationP));
			
			float3 toCentroidDirectionNormalized = toCentroid / distanceToCentroid;
			float targetSpeed = math.lerp(entity.Component.MinSpeed, entity.MaxSpeed, weight);
			targetSpeed = math.max(entity.Speed, targetSpeed);
			
			return new VelocityResult(
				toCentroidDirectionNormalized,
				weight * entity.Component.BaseData.DirectionStrength,
				targetSpeed,
				weight * entity.Component.BaseData.SpeedStrength,
				entity.Component.BaseData.Priority);
		}
	}
	
	[JobWrapper(typeof(CohesionComponent), typeof(SteeringEntityTagComponent))]
	[OutData(typeof(VelocityResults))]
	class CohesionJobWrapper : INeighborBehaviorJobWrapper 
	{
		public JobHandle Schedule(
			SystemBase systemBase,
			in NeighborBehaviorParams neighborBehaviorParams,
			out IDelayedDisposable results,
			in JobHandle dependency)
		{
			results = new VelocityResults(neighborBehaviorParams.NeighborQueryParams.MainBaseParams.EntityCount);

			var jobHandle = new CohesionJob().Schedule<CohesionJob, CohesionComponent, SteeringEntityTagComponent, SimpleAccumulator, VelocityResult>(
				systemBase,
				neighborBehaviorParams,
				(VelocityResults)results,
				1,
				dependency);
			
			return jobHandle;
		}
	}
}