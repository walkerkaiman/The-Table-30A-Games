using System.Runtime.CompilerServices;
using SteeringAI.Core;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using RaycastHit = Unity.Physics.RaycastHit;

namespace SteeringAI.Defaults
{
	struct AvoidAccumulator : IAccumulator
	{
		public float SumAngle;
		public float3 SumVector;
		public float MaxMagnitude;
		public float SumMagnitude;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(float3 vector, float angle, float magnitude)
		{
			SumVector += vector;
			SumAngle += angle;
			MaxMagnitude = math.max(MaxMagnitude, magnitude);
			SumMagnitude += magnitude;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Init()
		{
			SumVector = new float3();
			MaxMagnitude = 0;
			SumAngle = 0;
		}
	}
	
	[BurstCompile]
	struct AvoidVerticalWallsJob : IRaycastBehaviorJob<AvoidVerticalWallsComponent, AvoidAccumulator, NoneAccumulator, VelocityResult>
	{
		public void OnHit(
			in EntityInformation<AvoidVerticalWallsComponent> entity,
			in RayData rayData,
			in RaycastHit hit,
			ref AvoidAccumulator hitA)
		{
			float dot = math.abs(math.dot(hit.SurfaceNormal, new float3(0, 1, 0)));
			float slopeAngle = math.degrees(math.acos(dot));
			
			if(slopeAngle < entity.Component.MinSlope)
			{
				return;
			}
			
			float angleRatio = math.clamp(math.remap(entity.Component.MinSlope, math.PI * 0.5f, 0, 1, slopeAngle), 0, 1);
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
			weight *= angleRatio;
			
			hitA.Add(hit.SurfaceNormal * weight, slopeAngle * weight,  weight);
		}

		public void OnMiss(in EntityInformation<AvoidVerticalWallsComponent> entity, in RayData rayData, ref NoneAccumulator missA) { }

		public VelocityResult Finalize(
			in EntityInformation<AvoidVerticalWallsComponent> entity,
			in AvoidAccumulator hitA,
			in NoneAccumulator missA)
		{
			if(hitA.MaxMagnitude <= 0) return default;
			
			float averageAngle = hitA.SumAngle / hitA.SumMagnitude;
			float angleRatio = math.remap(entity.Component.MinSlope, 90f, 0, 1, averageAngle);
			angleRatio = math.clamp(angleRatio, 0, 1);
			angleRatio = WeightFunctions.fq4(angleRatio, entity.Component.SlopeActivationP);
			
			float weight = WeightFunctions.InverseSquareToOne(hitA.MaxMagnitude, entity.Component.ActivationP, entity.Component.ActivationK);
			weight *= angleRatio;
			
			float3 direction = Float3Utils.ProjectOnPlane(hitA.SumVector, new float3(0, 1, 0));
			direction = math.normalize(direction);
			
			return new VelocityResult(
				direction,
				weight * entity.Component.BaseData.DirectionStrength,
				0, 
				0,
				entity.Component.BaseData.Priority);
		}
	}

	[JobWrapper(typeof(AvoidVerticalWallsComponent))]
	[OutData(typeof(VelocityResults))]
	public class AvoidVerticalWallsJobWrapper : IRaycastBehaviorJobWrapper
	{
		public JobHandle Schedule(
			SystemBase systemBase,
			in RaycastBehaviorParams rayBehaviorParams,
			out IDelayedDisposable results,
			in JobHandle dependency)
		{
			results = new VelocityResults(rayBehaviorParams.MainBaseParams.EntityCount);
			var a = new AvoidVerticalWallsJob().Schedule<AvoidVerticalWallsJob, AvoidVerticalWallsComponent, AvoidAccumulator, NoneAccumulator, VelocityResult>(
				systemBase,
				rayBehaviorParams,
				(VelocityResults)results,
				1,
				dependency);
			return a;
		}
	}
}