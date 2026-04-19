using SteeringAI.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace SteeringAI.Defaults
{
    [BurstCompile]
    struct WanderingJob : ISimpleBehaviorJob<WanderingComponent, VelocityResult>
    {
        [ReadOnly] public EntityTypeHandle EntityTypeHandle;
        [ReadOnly] public float Time;

        public VelocityResult Execute(EntityInformation<WanderingComponent> entity)
        {
            var entityIndex = entity.Chunk.GetNativeArray(EntityTypeHandle)[entity.Indexes.EntityInChunkIndex].Index;
            
            float noiseValueSpeed = 0.5f + 0.5f * noise.snoise(new float2(entityIndex % 457.897f + 18.671f, Time * entity.Component.SpeedFrequency));
            float noiseSpeed = math.lerp(entity.Component.MinSpeed, entity.Component.MaxSpeed, noiseValueSpeed);
            
            float noiseValueX = normalizedNoise(new float2(entityIndex % 37.1756f, Time * entity.Component.XFrequency));
            float noiseValueY = normalizedNoise(new float2(entityIndex % 37.1756f + 12.423f, Time * entity.Component.YFrequency));
            
            float angleX = noiseValueX * math.PI;
            float angleY = math.PI * 0.5f + noiseValueY * math.radians(entity.Component.MaxUpDownAngle);
            
            float3 direction = new float3(
                math.sin(angleY) * math.cos(angleX),
                math.cos(angleY),
                math.sin(angleY) * math.sin(angleX));
            
            float noiseValueDesire = 0.5f + 0.5f * normalizedNoise(new float2(entityIndex % 31.3856f + 17.746f, Time * entity.Component.DesireFrequency));
            float desireMultiplier = math.lerp(entity.Component.DesireMultiplierRange.x, entity.Component.DesireMultiplierRange.y, noiseValueDesire);
            
            return new VelocityResult(
                direction,
                entity.Component.BaseData.DirectionStrength * desireMultiplier,
                noiseSpeed,
                entity.Component.BaseData.SpeedStrength * desireMultiplier,
                entity.Component.BaseData.Priority);
        }

        private static float normalizedNoise(float2 p)
        {
            float y = math.saturate(noiseNormalizer(0.5f + noise.snoise(p) * 0.5f));
            return y * 2 - 1; 
        }
        
        // makes the distribution of output of noise.snoise uniform
        private static float noiseNormalizer(float p)
        {
            // polynomial regression found with the help of
            // https://stats.blue/Stats_Suite/polynomial_regression_calculator.html
            float x = 
                - 8.7383210f * math.pow(p, 5)
                + 21.8797580f * math.pow(p, 4)
                - 20.5719160f * math.pow(p, 3)
                + 8.9478115f * math.pow(p, 2)
                - 0.5288123f * p
                + 0.0062402f;
            return x; 
        }
    }
    
    [JobWrapper(typeof(WanderingComponent))]
    [OutData(typeof(VelocityResults))]
    public class WanderingJobWrapper : ISimpleBehaviorJobWrapper
    {
        public JobHandle Schedule(
            SystemBase systemBase,
            in BaseBehaviorParams mainBaseParams,
            out IDelayedDisposable results,
            in JobHandle dependency)
        {
            results = new VelocityResults(mainBaseParams.EntityCount);
            return new WanderingJob
            {
                Time = (float)systemBase.World.Time.ElapsedTime,
                EntityTypeHandle = systemBase.GetEntityTypeHandle(),
            }.Schedule<WanderingJob, WanderingComponent, VelocityResult>(
                systemBase,
                mainBaseParams,
                (VelocityResults)results,
                1,
                dependency);
        }
    }
}