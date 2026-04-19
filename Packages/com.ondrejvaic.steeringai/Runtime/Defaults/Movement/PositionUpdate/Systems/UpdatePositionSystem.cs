using SteeringAI.Core;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace SteeringAI.Defaults
{
    [BurstCompile]
    public partial struct UpdatePosition : IJobEntity
    {
        public float DeltaTime;
    
        void Execute(ref LocalTransform worldTransform, in VelocityComponent velocity)
        {
            worldTransform.Position += velocity.Value * DeltaTime;
        }
    }

    [RequireMatchingQueriesForUpdate]
    [UpdateAfter(typeof(AfterSteeringSystemsGroup))]
    [BurstCompile]
    public partial struct UpdatePositionSystem : ISystem
    {
        private EntityQuery queryVelocity;
    
        public void OnCreate(ref SystemState state)
        {
            queryVelocity = state.GetEntityQuery(
                new EntityQueryDesc
                {
                    All = new []
                    {
                        ComponentType.ReadWrite<LocalTransform>(),
                        ComponentType.ReadOnly<VelocityComponent>(),
                        ComponentType.ReadOnly<UpdatePositionTag>()
                    },
                    None = new []
                    {
                        ComponentType.ReadWrite<CollideAndSlideComponent>()
                    }
                });
        }

        public void OnDestroy(ref SystemState state) { }

        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            state.Dependency = new UpdatePosition
            {
                DeltaTime = deltaTime
            }.ScheduleParallel(queryVelocity, state.Dependency);
        }
    }
}