using SteeringAI.Core;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace SteeringAI.Defaults
{
	public struct AlignmentAccumulator : IAccumulator
	{
		public float3 SumDirections;
		public float SumWeight;
		public float SumSpeed;

		public void Add(float3 vector, float speed, float magnitude)
		{
			SumDirections += vector;
			SumWeight += magnitude;
			SumSpeed += speed;
		}

		public void Init()
		{
			SumDirections = new float3();
			SumWeight = 0;
			SumSpeed = 0;
		}
	}
	
	[BurstCompile]
	struct AlignmentJob : INeighborBehaviorJob<AlignmentComponent, SteeringEntityTagComponent, AlignmentAccumulator, VelocityResult>
	{
		public void Execute(in NeighborBehaviorData<AlignmentComponent, SteeringEntityTagComponent> pair, ref AlignmentAccumulator accumulator)
		{
			if(pair.DistanceToOrigin <= 0) return;
			
			float weight = WeightFunctions.Observability(
				pair,
				pair.Entity.Component.StartAngle,
				pair.Entity.Component.BaseData.MaxAngle,
				pair.Entity.Component.DistanceP,
				pair.Entity.Component.AngleP);
			
			accumulator.Add(pair.OtherEntity.Direction * weight, pair.OtherEntity.Speed * weight, weight);
		}

		public VelocityResult Finalize(in EntityInformation<AlignmentComponent> entity, in AlignmentAccumulator accumulator)
		{
			if(accumulator.SumWeight <= 0) return default;
			
			float sumDirectionsLength = math.length(accumulator.SumDirections);
			if(sumDirectionsLength <= 0) return default;
			
			float3 direction = accumulator.SumDirections / sumDirectionsLength;
			float averageSpeed = accumulator.SumSpeed / accumulator.SumWeight;
			
			float weightDir = WeightFunctions.AngleStrengthToOne(direction, entity.Direction, entity.Component.StartActivationAngle, entity.Component.MinAngleWeight, entity.Component.AngleActivationP);
			float weightSpeed = WeightFunctions.SpeedStrengthToOne(entity.Speed, averageSpeed, entity.MaxSpeed, entity.Component.StartSpeedDiffFraction, entity.Component.MinSpeedDiffWeight, entity.Component.SpeedDiffActivationP);
			
			return new VelocityResult(
				direction,
				weightDir * entity.Component.BaseData.DirectionStrength,
				averageSpeed,
				weightSpeed * entity.Component.BaseData.SpeedStrength, 
				entity.Component.BaseData.Priority);
		}
	}
	
	[JobWrapper(typeof(AlignmentComponent), typeof(SteeringEntityTagComponent))]
	[OutData(typeof(VelocityResults))]
	class AlignmentJobWrapper : INeighborBehaviorJobWrapper 
	{
		public JobHandle Schedule(
			SystemBase systemBase,
			in NeighborBehaviorParams neighborBehaviorParams,
			out IDelayedDisposable results,
			in JobHandle dependency)
		{
			results = new VelocityResults(neighborBehaviorParams.NeighborQueryParams.MainBaseParams.EntityCount);

			var jobHandle = new AlignmentJob().Schedule<AlignmentJob, AlignmentComponent, SteeringEntityTagComponent, AlignmentAccumulator, VelocityResult>(
				systemBase,
				neighborBehaviorParams,
				(VelocityResults)results,
				1,
				dependency);
			
			return jobHandle;
		}
	}
}