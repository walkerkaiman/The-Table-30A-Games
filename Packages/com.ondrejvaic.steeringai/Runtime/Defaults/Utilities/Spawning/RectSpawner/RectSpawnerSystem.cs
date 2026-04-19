using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace SteeringAI.Defaults
{
    [BurstCompile]
    struct RectSpawnerJob : IJobParallelFor
    {
        [NativeDisableContainerSafetyRestriction]
        [NativeDisableParallelForRestriction]
        public ComponentLookup<LocalTransform> LocalToWorldFromEntity;

        [ReadOnly] public NativeArray<Entity> Entities;
        public float3 Position;
        public float2 Size;
        public int Cols;
        public int Rows;

        public void Execute(int index)
        {
            var entity = Entities[index];

            int x = index % Cols;
            int y = index / Cols;

            float colsF = (float)Cols;
            float rowsF = (float)Rows;
            float halfX = Size.x * 0.5f;
            float halfZ = Size.y * 0.5f;
            float spacingX = Size.x / colsF;
            float spacingZ = Size.y / rowsF;

            float offsetX = -halfX + spacingX * (x + 0.5f);
            float offsetZ = -halfZ + spacingZ * (y + 0.5f);

            float3 pos = new float3(Position.x + offsetX, Position.y, Position.z + offsetZ);

            LocalToWorldFromEntity[entity] = new LocalTransform
            {
                Position = pos,
                Rotation = quaternion.identity,
                Scale = 1f
            };
        }
    }

    [RequireMatchingQueriesForUpdate]
    public partial class RectSpawnerSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities.WithStructuralChanges().ForEach((Entity entity, in RectSpawnerComponent spawner) =>
            {
                if (spawner.Count <= 0 || spawner.Prefab == Entity.Null)
                {
                    EntityManager.DestroyEntity(entity);
                    return;
                }

                var world = World.Unmanaged;
                var spawned = CollectionHelper.CreateNativeArray<Entity, RewindableAllocator>(spawner.Count, ref world.UpdateAllocator);
                EntityManager.Instantiate(spawner.Prefab, spawned);

                int cols = (int)math.ceil(math.sqrt((float)spawner.Count));
                if (cols <= 0) cols = 1;
                int rows = (spawner.Count + cols - 1) / cols; // ceil division

                ComponentLookup<LocalTransform> localLookup = GetComponentLookup<LocalTransform>();
                
                var job = new RectSpawnerJob
                {
                    LocalToWorldFromEntity = localLookup,
                    Entities = spawned,
                    Position = spawner.Position,
                    Size = spawner.Size,
                    Cols = cols,
                    Rows = rows
                };

                Dependency = job.Schedule(spawner.Count, 64, Dependency);
                EntityManager.DestroyEntity(entity);
            }).Run();
        }
    }
}