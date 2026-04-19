using Unity.Entities;
using Unity.Mathematics;

namespace SteeringAI.Core
{
    public struct NeighborBehaviorData<ComponentT, OtherComponentT>
        where ComponentT : unmanaged, INeighborBaseBehavior, IComponentData
        where OtherComponentT : unmanaged, IComponentData
    {
        public EntityInformation<ComponentT> Entity;
        public EntityInformation<OtherComponentT> OtherEntity;
        public float DistanceToSurface;
        public float DistanceToOrigin;
        public float3 ToOther;
        public float3 ToOtherNormalized;
    }
}