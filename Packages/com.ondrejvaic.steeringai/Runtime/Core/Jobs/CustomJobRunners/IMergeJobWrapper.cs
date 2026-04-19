using Unity.Entities;
using Unity.Jobs;

namespace SteeringAI.Core
{
    public interface IMergeJobWrapper
    {
        public JobHandle Schedule(
            SystemBase system,
            in BaseBehaviorParams mainBaseParams,
            in IDelayedDisposable[] results,
            JobHandle dependency);
    }
}