using System.Runtime.CompilerServices;
using SteeringAI.Core;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace SteeringAI.Defaults
{
	struct SeparationAccumulator : IAccumulator
	{
		public float3 SumVector;
		public float MaxMagnitude;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(float3 vector, float magnitude)
		{
			SumVector += vector;
			MaxMagnitude = math.max(MaxMagnitude, magnitude);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Init()
		{
			SumVector = new float3();
			MaxMagnitude = 0;
		}
	}
	
	[BurstCompile]
	struct SeparationJob : INeighborBehaviorJob<SeparationComponent, SteeringEntityTagComponent, SeparationAccumulator, VelocityResult>
	{
		public void Execute(in NeighborBehaviorData<SeparationComponent, SteeringEntityTagComponent> pair, ref SeparationAccumulator accumulator)
		{
			if(pair.DistanceToOrigin <= 0) return;
			
			float weight = WeightFunctions.Observability(
				pair, 
				pair.Entity.Component.StartAngle, 
				pair.Entity.Component.BaseData.MaxAngle, 
				pair.Entity.Component.DistanceP, 
				pair.Entity.Component.AngleP);
			
			accumulator.Add(-pair.ToOtherNormalized * weight, weight);
		}

		public VelocityResult Finalize(in EntityInformation<SeparationComponent> entity, in SeparationAccumulator accumulator)
		{
			if(accumulator.MaxMagnitude <= 0) return default;
			
			float3 direction = math.normalize(accumulator.SumVector);
			float weight = WeightFunctions.InverseSquareToOne(accumulator.MaxMagnitude, entity.Component.ActivationP, entity.Component.ActivationK);
			
			var res = new VelocityResult(
				direction,
				weight * entity.Component.BaseData.DirectionStrength,
				0,
				0,
				entity.Component.BaseData.Priority);

			return res;
		}
	}
	
	[JobWrapper(typeof(SeparationComponent), typeof(SteeringEntityTagComponent))]
	[OutData(typeof(VelocityResults))]
	class SeparationJobWrapper : INeighborBehaviorJobWrapper 
	{
		public JobHandle Schedule(
			SystemBase systemBase,
			in NeighborBehaviorParams neighborBehaviorParams,
			out IDelayedDisposable results,
			in JobHandle dependency)
		{
			results = new VelocityResults(neighborBehaviorParams.NeighborQueryParams.MainBaseParams.EntityCount);

			var jobHandle = new SeparationJob
			{
			}.Schedule<SeparationJob, SeparationComponent, SteeringEntityTagComponent, SeparationAccumulator, VelocityResult>(
				systemBase,
				neighborBehaviorParams,
				(VelocityResults)results,
				1,
				dependency);
			
			return jobHandle;
		}
	}
}