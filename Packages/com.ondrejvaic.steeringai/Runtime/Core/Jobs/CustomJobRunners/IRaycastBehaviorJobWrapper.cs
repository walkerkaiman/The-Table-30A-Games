using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;

namespace SteeringAI.Core
{
    public struct RaycastBehaviorParams
    {
        [ReadOnly] public NativeArray<RaycastHit> RaycastHits;
        [ReadOnly] public NativeArray<RayData> RaycastDatas;
        [ReadOnly] public BaseBehaviorParams MainBaseParams;
        [ReadOnly] public RaycastSettings RaySettings;
    }
    
    public interface IRaycastBehaviorJobWrapper
    {
        public JobHandle Schedule(
            SystemBase systemBase,
            in RaycastBehaviorParams rayBehaviorParams,
            out IDelayedDisposable results,
            in JobHandle dependency);
    }
}