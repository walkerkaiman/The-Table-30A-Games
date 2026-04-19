using SteeringAI.Core;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace SteeringAI.Defaults
{
    [BurstCompile]
    struct KeepHeightJob : ISimpleBehaviorJob<KeepHeightComponent, VelocityResult>
    {
        public VelocityResult Execute(EntityInformation<KeepHeightComponent> entity)
        {
            float height = entity.Component.MaxY - entity.Component.MinY;
            float midY = entity.Component.MinY + height * .5f;
            float a = entity.Position.y - midY;
            float overBorder = math.abs(a) - height * .5f;
            float fraction = math.remap(0, entity.Component.CalmZoneHeight, 0, 1, overBorder);
            
            fraction = math.saturate(fraction);
            fraction *= fraction;

            float3 direction = new float3(0, -math.sign(a), 0);
            
            return new VelocityResult(
                direction,
                fraction * entity.Component.BaseData.DirectionStrength,
                0, 
                0,
                entity.Component.BaseData.Priority);
        }
    }
    
    [JobWrapper(typeof(KeepHeightComponent))]
    [OutData(typeof(VelocityResults))]
    public class KeepHeightJobWrapper : ISimpleBehaviorJobWrapper
    {
        public JobHandle Schedule(
            SystemBase systemBase,
            in BaseBehaviorParams mainBaseParams,
            out IDelayedDisposable results,
            in JobHandle dependency)
        {
            results = new VelocityResults(mainBaseParams.EntityCount);
            return new KeepHeightJob
            {
            }.Schedule<KeepHeightJob, KeepHeightComponent, VelocityResult>(
                systemBase,
                mainBaseParams,
                (VelocityResults)results,
                1,
                dependency);
        }
    }
}
