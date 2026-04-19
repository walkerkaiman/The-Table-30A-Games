using SteeringAI.Core;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;

namespace SteeringAI.Defaults
{
    [BurstCompile]
    struct GoForwardJob : ISimpleBehaviorJob<GoForwardComponent, VelocityResult>
    {
        public VelocityResult Execute(EntityInformation<GoForwardComponent> entity)
        {
            var result = new VelocityResult(
                entity.Forward,
                entity.Component.BaseData.DirectionStrength,
                entity.Component.Speed, 
                entity.Component.BaseData.SpeedStrength,
                entity.Component.BaseData.Priority);
            
            return result;
        }
    }
    
    [JobWrapper(typeof(GoForwardComponent))]
    [OutData(typeof(VelocityResults))]
    public class GoForwardJobWrapper : ISimpleBehaviorJobWrapper
    {
        public JobHandle Schedule(
            SystemBase systemBase,
            in BaseBehaviorParams mainBaseParams,
            out IDelayedDisposable results,
            in JobHandle dependency)
        {
            results = new VelocityResults(mainBaseParams.EntityCount);
            
            var outDependency = new GoForwardJob
            {
            }.Schedule<GoForwardJob, GoForwardComponent, VelocityResult>(
                systemBase,
                mainBaseParams,
                (VelocityResults)results,
                1,
                dependency);
            
            return outDependency;
        }
    }
}