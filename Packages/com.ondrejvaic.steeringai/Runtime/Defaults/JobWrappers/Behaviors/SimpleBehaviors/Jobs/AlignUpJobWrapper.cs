using SteeringAI.Core;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace SteeringAI.Defaults
{
    [BurstCompile]
    struct AlignUpJob : ISimpleBehaviorJob<AlignUpComponent, VelocityResult>
    {
        public VelocityResult Execute(EntityInformation<AlignUpComponent> entity)
        {
            float3 projected = Float3Utils.ProjectOnPlane(entity.Forward, new float3(0, 1, 0));
            
            float dissimilarity = 1 - (Float3Utils.DirectionSimilarity(entity.Forward, projected) - 0.5f) * 2;
            dissimilarity *= dissimilarity;
            
            return new VelocityResult(
                math.normalizesafe(projected),
                entity.Component.BaseData.DirectionStrength * dissimilarity,
                0,
                0,
                entity.Component.BaseData.Priority);
        }
    }
    
    [JobWrapper(typeof(AlignUpComponent))] 
    [OutData(typeof(VelocityResults))] 
    public class AlignUpJobWrapper : ISimpleBehaviorJobWrapper
    {
        public JobHandle Schedule( 
            SystemBase systemBase,
            in BaseBehaviorParams mainBaseParams,
            out IDelayedDisposable results,
            in JobHandle dependency)
        {
            results = new VelocityResults(mainBaseParams.EntityCount);
            return new AlignUpJob().Schedule<AlignUpJob, AlignUpComponent, VelocityResult>(
                systemBase,
                mainBaseParams,
                (VelocityResults)results,
                1,
                dependency);
        }
    }
}