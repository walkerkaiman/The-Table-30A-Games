using SteeringAI.Core;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using RaycastHit = Unity.Physics.RaycastHit;

namespace SteeringAI.Defaults
{
	struct MinAccumulator : IAccumulator
	{
		public float MinMagnitude;
		public float3 Nearest;
		
		public void Add(float magnitude, float3 nearest)
		{
			if (magnitude < MinMagnitude)
			{
				MinMagnitude = magnitude;
				Nearest = nearest;
			}
		}

		public void Init()
		{
			MinMagnitude = float.PositiveInfinity;
		} 
	}
	
	[BurstCompile]
    struct AvoidGroundJob : IRaycastBehaviorJob<AvoidGroundComponent, MinAccumulator, NoneAccumulator, VelocityResult>
    {
	    public void OnHit(
		    in EntityInformation<AvoidGroundComponent> entity,
		    in RayData rayData,
		    in RaycastHit hit,
		    ref MinAccumulator hitA)
        {
	        float distance = hit.Fraction * rayData.MaxDistance;
	        hitA.Add(distance, hit.Position);
        }

	    public void OnMiss(in EntityInformation<AvoidGroundComponent> entity, in RayData rayData,
		    ref NoneAccumulator missA) { }

        public VelocityResult Finalize(
	        in EntityInformation<AvoidGroundComponent> entity,
	        in MinAccumulator hitA,
	        in NoneAccumulator missA)
        {
	        if (float.IsPositiveInfinity(hitA.MinMagnitude)) return default;
	        
	        float distanceFraction = 1 - hitA.MinMagnitude / entity.Component.BaseData.MaxDistance;
	        distanceFraction *= distanceFraction;
	        
	        return new VelocityResult(
		        new float3(0, 1, 0),
		        distanceFraction * entity.Component.BaseData.DirectionStrength,
		        0,
		        0,
		        entity.Component.BaseData.Priority);
        }
    }
    
    [JobWrapper(typeof(AvoidGroundComponent))]
    [OutData(typeof(VelocityResults))]
	public class AvoidGroundJobWrapper : IRaycastBehaviorJobWrapper
	{
		public JobHandle Schedule(
			SystemBase systemBase,
			in RaycastBehaviorParams rayBehaviorParams,
			out IDelayedDisposable results,
			in JobHandle dependency)
		{
			results = new VelocityResults(rayBehaviorParams.MainBaseParams.EntityCount);
			var a = new AvoidGroundJob
			{
			}.Schedule<AvoidGroundJob, AvoidGroundComponent, MinAccumulator, NoneAccumulator, VelocityResult>(
				systemBase,
				rayBehaviorParams,
				(VelocityResults)results,
				1,
				dependency);
			return a;
		}
	}
}