using SteeringAI.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

namespace SteeringAI.Defaults
{
    [BurstCompile]
    struct RandomSpawnerJob : IJobParallelFor
    {
        [NativeDisableContainerSafetyRestriction]
        [NativeDisableParallelForRestriction]
        public ComponentLookup<LocalTransform> LocalToWorldFromEntity;
        
        [ReadOnly] public NativeArray<Entity> Entities;

        public float Radius;
        public float3 Position;
        public float3 PositionMask;

        public void Execute(int index)
        {
            var entity = Entities[index];
            
            Random random = new Random(((uint)(entity.Index + index + 1) * 0x9F6ABC1));

            var dir = math.normalizesafe(random.NextFloat3() - new float3(0.5f, 0.5f, 0.5f)) * PositionMask;
            var pos = Position + (dir * Radius * random.NextFloat());

            LocalToWorldFromEntity[entity] = new LocalTransform
            {
                Position = pos,
                Rotation = quaternion.LookRotation(-random.NextFloat3() * PositionMask, math.up()),
                Scale = 1
            };
        }
    }
    
    [RequireMatchingQueriesForUpdate]
    public partial class SpawnerSystem : SystemBase
    {
        protected override void OnDestroy()
        {
            
        }
    
        protected override void OnUpdate()
        {
            Entities.WithStructuralChanges().ForEach((Entity entity, in SpawnerComponent boidSchool) =>
            {
                var world = World.Unmanaged;
                var boidEntities = CollectionHelper.CreateNativeArray<Entity, RewindableAllocator>(boidSchool.Count, ref world.UpdateAllocator);
                EntityManager.Instantiate(boidSchool.Prefab, boidEntities);

                ComponentLookup<LocalTransform> LocalToWorldFromEntity = GetComponentLookup<LocalTransform>(); 
                RandomSpawnerJob randomSpawnerJob = new RandomSpawnerJob 
                {
                    PositionMask = boidSchool.PositionMask,
                    Position = boidSchool.Position,
                    Radius = boidSchool.Radius,
                    Entities = boidEntities,
                    LocalToWorldFromEntity = LocalToWorldFromEntity,
                };
                
                Dependency = randomSpawnerJob.Schedule(boidSchool.Count, 4, Dependency);
                
                EntityManager.DestroyEntity(entity);
            }).Run();
        }
    }
}
