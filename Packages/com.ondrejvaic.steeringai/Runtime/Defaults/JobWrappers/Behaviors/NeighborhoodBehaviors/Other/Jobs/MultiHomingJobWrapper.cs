using System.Runtime.CompilerServices;
using SteeringAI.Core;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace SteeringAI.Defaults
{
	public struct HomingAccumulator : IAccumulator
	{
		public float MinDistance;
		public float MinRadius;
		public float MaxRadius;
		public float StrengthMultiplier;
		public float3 Direction;
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(float3 vector, float magnitude, float minRadius, float maxRadius, float strengthMultiplier)
		{
			if (magnitude < maxRadius && MaxRadius > maxRadius)
			{
				Direction = vector;
				MinDistance = magnitude;
				MinRadius = minRadius;
				MaxRadius = maxRadius;
				StrengthMultiplier = strengthMultiplier;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Init()
		{
			MinDistance = float.PositiveInfinity;
			MaxRadius = float.PositiveInfinity;
			Direction = new float3();
		}
	}
	
	[BurstCompile]
	struct MultiHomingJob : INeighborBehaviorJob<MultiHomingComponent, HomeComponent, HomingAccumulator, VelocityResult>
	{
		public void Execute(in NeighborBehaviorData<MultiHomingComponent, HomeComponent> pair, ref HomingAccumulator accumulator)
		{
			float3 toHomeN = math.normalizesafe(pair.OtherEntity.Position - pair.Entity.Position, float3.zero);

			accumulator.Add(toHomeN, pair.DistanceToOrigin, 
				pair.OtherEntity.Component.MinRadius, 
				pair.OtherEntity.Component.MaxRadius, 
				pair.OtherEntity.Component.StrengthMultiplier);
		}

		public VelocityResult Finalize(in EntityInformation<MultiHomingComponent> entity, in HomingAccumulator accumulator)
		{
			if(float.IsPositiveInfinity(accumulator.MinDistance)) return default;
            
			float distanceFraction = math.remap(accumulator.MinRadius, accumulator.MaxRadius, 0, 1, accumulator.MinDistance);
			distanceFraction = math.saturate(distanceFraction);
            
			float weight = WeightFunctions.fq1(distanceFraction, entity.Component.ActivationP);
			weight *= accumulator.StrengthMultiplier;
			
			return new VelocityResult(
				accumulator.Direction,
				weight * entity.Component.BaseData.DirectionStrength,
				0,
				0,
				entity.Component.BaseData.Priority);
		}
	}
	
	[JobWrapper(typeof(MultiHomingComponent), typeof(HomeComponent))]
	[OutData(typeof(VelocityResults))]
	class MultiHomingJobWrapper : INeighborBehaviorJobWrapper
	{
		public JobHandle Schedule(
			SystemBase systemBase,
			in NeighborBehaviorParams neighborBehaviorParams,
			out IDelayedDisposable results,
			in JobHandle dependency)
		{
			results = new VelocityResults(neighborBehaviorParams.NeighborQueryParams.MainBaseParams.EntityCount);
			return new MultiHomingJob().Schedule<MultiHomingJob, MultiHomingComponent, HomeComponent, HomingAccumulator, VelocityResult>(
				systemBase,
				neighborBehaviorParams,
				(VelocityResults)results,
				1,
				dependency);
		}
	}
}