using SteeringAI.Core;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace SteeringAI.Defaults
{
    [BurstCompile]
    struct HomingJob : ISimpleBehaviorJob<HomingComponent, VelocityResult>
    {
        public VelocityResult Execute(EntityInformation<HomingComponent> entity)
        {
            float3 toHome = entity.Component.HomePosition - entity.Position;
            float distance = math.length(toHome);
            float3 toHomeDirection = distance > 0 ? toHome / distance : entity.Direction;
            
            float distanceFraction = math.remap(entity.Component.MinRadius, entity.Component.MaxRadius, 0, 1, distance);
            distanceFraction = math.saturate(distanceFraction);
            
            float weight = WeightFunctions.fq1(distanceFraction, entity.Component.ActivationP);
            
            return new VelocityResult(
                toHomeDirection,
                weight * entity.Component.BaseData.DirectionStrength,
                0,
                0,
                entity.Component.BaseData.Priority);
        }
    }
    
    [JobWrapper(typeof(HomingComponent))]
    [OutData(typeof(VelocityResults))]
    public class HomingJobWrapper : ISimpleBehaviorJobWrapper
    {
        public JobHandle Schedule(
            SystemBase systemBase,
            in BaseBehaviorParams mainBaseParams,
            out IDelayedDisposable results,
            in JobHandle dependency)
        {
            results = new VelocityResults(mainBaseParams.EntityCount);
            return new HomingJob
            {
            }.Schedule<HomingJob, HomingComponent, VelocityResult>(
                systemBase,
                mainBaseParams,
                (VelocityResults)results,
                1,
                dependency);
        }
    }
}