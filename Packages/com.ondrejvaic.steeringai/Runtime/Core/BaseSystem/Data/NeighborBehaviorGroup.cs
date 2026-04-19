using Unity.Entities;

namespace SteeringAI.Core
{
    public struct NeighborBehaviorGroup
    {
        public INeighborBehaviorJobWrapper[] BehaviorJobRunners;
        public INeighborQueryJobWrapper NeighborQueryJobWrapper;
        public NeighborhoodSettings NeighborhoodSettings;
        public EntityQueryDesc TargetQueryDesc;
    }
}