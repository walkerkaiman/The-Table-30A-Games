using SteeringAI.Core;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;

namespace SteeringAI.Defaults
{
    [BurstCompile]
    struct FollowPathSplineJob : ISimpleBehaviorJob<FollowPathSplineComponent, VelocityResult>
    {
        public VelocityResult Execute(EntityInformation<FollowPathSplineComponent> entity)
        {
            if (float.IsNaN(entity.Component.Dir.x))
            {
                return default;
            }
            
            return new VelocityResult(
                entity.Component.Dir,
                entity.Component.BaseData.DirectionStrength,
                entity.Component.Speed, 
                entity.Component.BaseData.SpeedStrength,
                entity.Component.BaseData.Priority);
        }
    }
    
    [JobWrapper(typeof(FollowPathSplineComponent))]
    [OutData(typeof(VelocityResults))]
    public class FollowPathSplineJobWrapper : ISimpleBehaviorJobWrapper
    {
        public JobHandle Schedule(
            SystemBase systemBase,
            in BaseBehaviorParams mainBaseParams,
            out IDelayedDisposable results,
            in JobHandle dependency)
        {
            results = new VelocityResults(mainBaseParams.EntityCount);
            
            if (FollowPathSplineManager.Instance == null) return new JobHandle();
            
            var outDependency = new FollowPathSplineJob
            {
            }.Schedule<FollowPathSplineJob, FollowPathSplineComponent, VelocityResult>(
                systemBase,
                mainBaseParams,
                (VelocityResults)results,
                1,
                dependency);

            return outDependency;
        }
    }
}
