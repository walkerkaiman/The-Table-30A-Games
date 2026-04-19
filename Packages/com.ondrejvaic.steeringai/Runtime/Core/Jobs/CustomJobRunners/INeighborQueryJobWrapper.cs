using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace SteeringAI.Core
{
    public struct NeighborMatch
    {
        public int OtherIndex;
        public float DistanceToSurface;
        public float DistanceToOrigin;
    }
    
    [Serializable]
    public struct NeighborhoodSettings
    {
        public float MaxFOV;
        public float MaxNeighborDistance;
        public int MaxNumNeighbors;
    }
    
    public struct NeighborQueryParams
    {
        [ReadOnly] public BaseBehaviorParams MainBaseParams;
        [ReadOnly] public BaseBehaviorParams OtherBaseParams;
        [ReadOnly] public NeighborhoodSettings NeighborhoodSettings;
    }

    public interface INeighborQueryJobWrapper
    {
        public JobHandle Schedule(
            SystemBase systemBase,
            in NeighborQueryParams neighborQueryParams,
            in NativeArray<NeighborMatch> neighbors,
            in JobHandle dependency);
    }
}