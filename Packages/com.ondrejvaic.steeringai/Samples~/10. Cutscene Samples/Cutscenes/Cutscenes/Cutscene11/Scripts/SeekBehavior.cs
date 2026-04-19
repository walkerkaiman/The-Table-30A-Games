using SteeringAI.Core;
using SteeringAI.Defaults;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;

namespace SteeringAI.Samples.Cutscenes
{
    [BurstCompile]
    struct SeekJob : INeighborBehaviorJob<SeekComponent, SteeringEntityTagComponent, MinAccumulator, VelocityResult>
    {
        public void Execute(in NeighborBehaviorData<SeekComponent, SteeringEntityTagComponent> pair,
            ref MinAccumulator accumulator)
        {
            if (pair.DistanceToOrigin <= 0) return;

            accumulator.Add(pair.ToOtherNormalized, pair.DistanceToOrigin);
        }

        public VelocityResult Finalize(in EntityInformation<SeekComponent> entity, in MinAccumulator accumulator)
        {
            if (float.IsPositiveInfinity(accumulator.MinMagnitude)) return default;

            float weight = accumulator.MinMagnitude / entity.Component.BaseData.MaxDistance;
            weight *= weight;

            float directionDesire = weight * entity.Component.BaseData.DirectionStrength;
            float speedDesire = weight * entity.Component.BaseData.SpeedStrength;

            return new VelocityResult(
                accumulator.Direction,
                directionDesire,
                entity.Component.Speed,
                speedDesire,
                entity.Component.BaseData.Priority
            );
        }

        [JobWrapper(typeof(SeekComponent), typeof(SteeringEntityTagComponent))]
        [OutData(typeof(VelocityResults))]
        public class SeekJobWrapper : INeighborBehaviorJobWrapper
        {
            public JobHandle Schedule(
                SystemBase systemBase,
                in NeighborBehaviorParams neighborBehaviorParams,
                out IDelayedDisposable results,
                in JobHandle dependency)
            {
                results = new VelocityResults(neighborBehaviorParams.NeighborQueryParams.MainBaseParams.EntityCount);

                return new SeekJob()
                    .Schedule<SeekJob, SeekComponent, SteeringEntityTagComponent, MinAccumulator, VelocityResult>(
                        systemBase,
                        neighborBehaviorParams,
                        (VelocityResults)results,
                        1,
                        dependency);
            }
        }
    }
}




























































































