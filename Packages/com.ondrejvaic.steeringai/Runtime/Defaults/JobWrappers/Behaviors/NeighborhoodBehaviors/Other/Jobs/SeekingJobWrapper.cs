using System.Runtime.CompilerServices;
using SteeringAI.Core;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace SteeringAI.Defaults
{
    struct SeekingAccumulator : IAccumulator
    {
        public float DistanceToClosestEntity;
        public float TargetSpeed;
        public float3 DirectionToClosestEntity;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(float3 directionToEntity, float distance, float targetSpeed)
        {
            if (distance < DistanceToClosestEntity)
            {
                DistanceToClosestEntity = distance;
                DirectionToClosestEntity = directionToEntity;
                TargetSpeed = targetSpeed;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Init()
        {
            DistanceToClosestEntity = float.PositiveInfinity;
        }
    }

    [BurstCompile]
    struct SeekingJob : INeighborBehaviorJob<SeekingComponent, SteeringEntityTagComponent, SeekingAccumulator, VelocityResult>
    {

        public void Execute(in NeighborBehaviorData<SeekingComponent, SteeringEntityTagComponent> pair, ref SeekingAccumulator accumulator)
        {
            if (pair.DistanceToOrigin <= 0) return;
            
            accumulator.Add(pair.ToOtherNormalized, pair.DistanceToOrigin, pair.OtherEntity.Speed);
        }

        public VelocityResult Finalize(in EntityInformation<SeekingComponent> entity, in SeekingAccumulator accumulator)
        {
            if (float.IsPositiveInfinity(accumulator.DistanceToClosestEntity)) return default;

            float distanceFraction = accumulator.DistanceToClosestEntity / entity.Component.BaseData.MaxDistance;
            float weight = 1 - distanceFraction * distanceFraction;
            
            float maxSpeed = accumulator.TargetSpeed + entity.Component.MaxSpeedOverPrey;
            maxSpeed = math.max(maxSpeed, entity.Component.MinChaseSpeed);
            float targetSpeed = math.lerp(entity.Component.MinChaseSpeed, maxSpeed, weight);
            targetSpeed = math.max(targetSpeed, entity.Speed);

            float speedStrength = weight * entity.Component.BaseData.SpeedStrength; 
            float directionStrength = weight * entity.Component.BaseData.DirectionStrength;
            
            return new VelocityResult(
                accumulator.DirectionToClosestEntity,
                directionStrength,
                targetSpeed,
                speedStrength,
                entity.Component.BaseData.Priority);
        }
    }

    [JobWrapper(typeof(SeekingComponent), typeof(SteeringEntityTagComponent))]
    [OutData(typeof(VelocityResults))]
    public class SeekingJobWrapper : INeighborBehaviorJobWrapper
    {
        public JobHandle Schedule(
            SystemBase systemBase,
            in NeighborBehaviorParams neighborBehaviorParams,
            out IDelayedDisposable results,
            in JobHandle dependency)
        {
            results = new VelocityResults(neighborBehaviorParams.NeighborQueryParams.MainBaseParams.EntityCount);

            var jobHandle = new SeekingJob
            {
            }.Schedule<SeekingJob, SeekingComponent, SteeringEntityTagComponent, SeekingAccumulator, VelocityResult>(
                systemBase,
                neighborBehaviorParams,
                (VelocityResults)results,
                1,
                dependency);

            return jobHandle;
        }
    }
}