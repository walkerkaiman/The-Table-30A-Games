using System.Runtime.CompilerServices;
using SteeringAI.Core;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using RaycastHit = Unity.Physics.RaycastHit;

namespace SteeringAI.Defaults
{
	struct AvoidanceAccumulator : IAccumulator
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
	struct EnvironmentAvoidanceJob : IRaycastBehaviorJob<EnvironmentAvoidanceComponent, AvoidanceAccumulator, NoneAccumulator, VelocityResult>
	{
		public void OnHit(
			in EntityInformation<EnvironmentAvoidanceComponent> entity,
			in RayData rayData,
			in RaycastHit hit,
			ref AvoidanceAccumulator hitA)
		{
			float distance = hit.Fraction * rayData.MaxDistance;
			distance = math.max(math.EPSILON, distance);
			
			float distanceFraction = distance / rayData.MaxDistance;
			
			float weight = WeightFunctions.InverseSquare(distanceFraction, entity.Component.DistanceP);
			weight *= WeightFunctions.DotStrengthToZeroNew(
				entity.Direction,
				rayData.Direction,
				entity.Component.StartAngle,
				entity.Component.MaxAngle,
				entity.Component.AngleP);
			
			hitA.Add(hit.SurfaceNormal * weight, weight);
		}

		public void OnMiss(in EntityInformation<EnvironmentAvoidanceComponent> entity, in RayData rayData,
			ref NoneAccumulator missA)
		{
			
		}

		public VelocityResult Finalize(
			in EntityInformation<EnvironmentAvoidanceComponent> entity,
			in AvoidanceAccumulator hitA,
			in NoneAccumulator missA)
		{
			if(hitA.MaxMagnitude <= 0) return default;
			
			float3 direction = math.normalize(hitA.SumVector);
			float weight = WeightFunctions.InverseSquareToOne(hitA.MaxMagnitude, entity.Component.ActivationP, entity.Component.ActivationK);
			
			var res = new VelocityResult(
				direction,
				weight * entity.Component.BaseData.DirectionStrength,
				0,
				0,
				entity.Component.BaseData.Priority);

			return res;
		}
	}

	[JobWrapper(typeof(EnvironmentAvoidanceComponent))]
	[OutData(typeof(VelocityResults))] 
	public class EnvironmentAvoidanceJobWrapper : IRaycastBehaviorJobWrapper
	{
		public JobHandle Schedule(
			SystemBase systemBase,
			in RaycastBehaviorParams rayBehaviorParams,
			out IDelayedDisposable results,
			in JobHandle dependency)
		{
			results = new VelocityResults(rayBehaviorParams.MainBaseParams.EntityCount);
			var a = new EnvironmentAvoidanceJob().Schedule<EnvironmentAvoidanceJob, EnvironmentAvoidanceComponent, AvoidanceAccumulator, NoneAccumulator, VelocityResult>(
				systemBase,
				rayBehaviorParams,
				(VelocityResults)results,
				1,
				dependency);
			return a;
		}
	} 
}
	