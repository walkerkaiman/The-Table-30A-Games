using Unity.Entities;
using Unity.Jobs;

namespace SteeringAI.Core
{
    public interface ISimpleBehaviorJobWrapper
    {
        public JobHandle Schedule(
            SystemBase systemBase,
            in BaseBehaviorParams mainBaseParams,
            out IDelayedDisposable results,
            in JobHandle dependency);
    }
}