using SteeringAI.Core;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;

namespace SteeringAI.Defaults
{
    [BurstCompile]
    struct DebugSimpleJob : ISimpleBehaviorJob<DebugSimpleComponent, VelocityResult>
    {
        public VelocityResult Execute(EntityInformation<DebugSimpleComponent> entity)
        {
#if STEERING_DEBUG
            DebugDraw.DrawSphere(entity.Position, 0.5f * entity.Component.BaseData.DebugScale, entity.Component.BaseData.Color);
#endif
            return default;
        }
    }
    
    [JobWrapper(typeof(DebugSimpleComponent))] 
    [OutData(typeof(VelocityResults))] 
    public class DebugSimpleJobWrapper : ISimpleBehaviorJobWrapper
    {
        public JobHandle Schedule( 
            SystemBase systemBase,
            in BaseBehaviorParams mainBaseParams,
            out IDelayedDisposable results,
            in JobHandle dependency)
        {
            results = new VelocityResults(mainBaseParams.EntityCount);
            return new DebugSimpleJob().Schedule<DebugSimpleJob, DebugSimpleComponent, VelocityResult>(
                systemBase,
                mainBaseParams,
                (VelocityResults)results,
                1,
                dependency);
        }
    }
}