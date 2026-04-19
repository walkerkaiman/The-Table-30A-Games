using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace SteeringAI.Core
{
    public struct NeighborBehaviorParams
    {
        [ReadOnly] public int MaxNumNeighbors;
        [ReadOnly] public NativeArray<NeighborMatch> Neighbors;
        [ReadOnly] public NeighborQueryParams NeighborQueryParams;
    }
    
    public interface INeighborBehaviorJobWrapper
    {
        public JobHandle Schedule(
            SystemBase systemBase,
            in NeighborBehaviorParams neighborBehaviorParams,
            out IDelayedDisposable results,
            in JobHandle dependency);
    }
}