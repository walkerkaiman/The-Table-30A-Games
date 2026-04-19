using System.Runtime.CompilerServices;
using SteeringAI.Core;
using SteeringAI.Defaults.KNN;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace SteeringAI.Defaults
{
    [BurstCompile]
    public struct FillHashMapJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> Positions;
        [ReadOnly] public NativeArray<float> InverseCellRadius;
        public NativeParallelMultiHashMap<long, int>.ParallelWriter HashMap;

        public void Execute(int index)
        {
            float3 position = Positions[index];
            float inverseCellRadius = InverseCellRadius[0];
            var cellPosition = CalculateCellPosition(position, inverseCellRadius);
            long cellHash = CalculateHash(cellPosition);
            HashMap.Add(cellHash, index);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 CalculateCellPosition(float3 position, float inverseBoidCellRadius) =>
            (int3)math.floor(position * inverseBoidCellRadius);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long CalculateHash(int3 position, int3 offset = default) => 
            hash(position + offset);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long hash(int3 position) => 
            position.x +
            (long)position.y * 2_000_000 +
            (long)position.z * 2_000_000 * 2_000_000;
    }
    
    [BurstCompile]
    public struct FindClosestNeighborsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> Forwards;
        [ReadOnly] public NativeArray<float3> Positions;
        [ReadOnly] public NativeArray<float3> OtherPositions;
        [ReadOnly] public int MaxNumNeighbors;
        [ReadOnly] public NativeParallelMultiHashMap<long, int> HashMap;
    
        [ReadOnly] public NativeArray<float> MaxRadius;
        [ReadOnly] public NativeArray<float> InverseCellRadius;
        [ReadOnly] public NativeArray<float> Radii;
        [ReadOnly] public NativeArray<float> OtherRadii;
        [ReadOnly] public float CosOfFOV;

        [NativeDisableParallelForRestriction] public NativeArray<NeighborMatch> Neighbors;

        public void Execute(int entityInQueryIndex)
        {
            float maxDistance = MaxRadius[0];
            float maxDistanceSquared = maxDistance * maxDistance;
        
            float3 boidForward = Forwards[entityInQueryIndex];
            float3 boidPosition = Positions[entityInQueryIndex];
            float boidRadius = Radii[entityInQueryIndex];
        
            var cellPosition = FillHashMapJob.CalculateCellPosition(boidPosition, InverseCellRadius[0]);

            MinMaxHeap<NeighborMatch> maxHeap = new MinMaxHeap<NeighborMatch>(MaxNumNeighbors, Allocator.Temp);
        
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        long cellHash = FillHashMapJob.CalculateHash(cellPosition, new int3(x, y, z));
                        var values = HashMap.GetValuesForKey(cellHash);
                        foreach (int otherEntityInQueryIndex in values)
                        {
                            float3 otherBoidPosition = OtherPositions[otherEntityInQueryIndex];
                            float3 toOther = otherBoidPosition - boidPosition;
                            toOther = new float3(toOther.x, toOther.y, toOther.z);
        
                            float distanceSquared = math.lengthsq(toOther);
                
                            if (distanceSquared < maxDistanceSquared && distanceSquared > math.EPSILON)
                            {
                                float otherRadius = OtherRadii[otherEntityInQueryIndex];
                    
                                float distance = math.sqrt(distanceSquared);
                                float distanceToOther = distance - (boidRadius + otherRadius);

                                if(distanceToOther < maxDistance) {
                                    float3 dir = math.normalize(toOther);
                                    float dot = math.dot(dir, boidForward);
							
                                    if(dot >= CosOfFOV) {
                                        maxHeap.PushObjMax(new NeighborMatch
                                        {
                                            DistanceToOrigin = distance,
                                            DistanceToSurface = distanceToOther,
                                            OtherIndex = otherEntityInQueryIndex
                                        }, distanceToOther);
                            
                                        if (maxHeap.Count == MaxNumNeighbors)
                                        {
                                            maxDistance = maxHeap.HeadValue;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        
            int numFound = maxHeap.Count;
            for (int i = 0; i < numFound; i++) {
            
                Neighbors[entityInQueryIndex * MaxNumNeighbors + i] = maxHeap.PopObjMax();
            }
			
            for (int i = numFound; i < MaxNumNeighbors; i++) {
                Neighbors[entityInQueryIndex * MaxNumNeighbors + i] = new NeighborMatch { OtherIndex = -1 };
            }
			 
            maxHeap.Dispose();
        }
    }
    
    [JobWrapper]
    public class SpacialHashKNNJobWrapper : INeighborQueryJobWrapper
    {
        public JobHandle Schedule(
            SystemBase systemBase, 
            in NeighborQueryParams neighborQueryParams, 
            in NativeArray<NeighborMatch> neighbors,
            in JobHandle dependency)
        {
            var hashMap = new NativeParallelMultiHashMap<long, int>(neighborQueryParams.OtherBaseParams.EntityCount, Allocator.TempJob);
            NativeArray<float3> boidPositions = new NativeArray<float3>(neighborQueryParams.MainBaseParams.EntityCount, Allocator.TempJob);
            NativeArray<float3> boidForwards = new NativeArray<float3>(neighborQueryParams.MainBaseParams.EntityCount, Allocator.TempJob);
            NativeArray<float3> otherPositions = new NativeArray<float3>(neighborQueryParams.OtherBaseParams.EntityCount, Allocator.TempJob);
        
            var loadPositionsForwardsDependency = new LoadPositionsAndForwardsJob
            {
                LocalToWorlds = neighborQueryParams.MainBaseParams.LocalToWorlds, 
                Positions = boidPositions,
                Forwards = boidForwards,
            }.Schedule(neighborQueryParams.MainBaseParams.EntityCount, 8, dependency);
        
            var loadPositionsDependency = new LoadPositionsJob
            {
                LocalToWorlds = neighborQueryParams.OtherBaseParams.LocalToWorlds,
                Positions = otherPositions,
            }.Schedule(neighborQueryParams.OtherBaseParams.EntityCount, 8, dependency);
            
            NativeArray<float> maxRadius1 = new NativeArray<float>(1, Allocator.TempJob);
            var findMaxDependency1 = new FindMaxJob
            {
                RadiusArray = neighborQueryParams.MainBaseParams.Radii,
                Max = maxRadius1
            }.Schedule(dependency);
        
            NativeArray<float> maxRadius2 = new NativeArray<float>(1, Allocator.TempJob);
            var findMaxDependency2 = new FindMaxJob
            {
                RadiusArray = neighborQueryParams.OtherBaseParams.Radii,
                Max = maxRadius2
            }.Schedule(dependency);
        
            var radiusDependency = JobHandle.CombineDependencies(findMaxDependency1, findMaxDependency2);
        
            NativeArray<float> inverseRadius = new NativeArray<float>(1, Allocator.TempJob);
            NativeArray<float> totalRadius = new NativeArray<float>(1, Allocator.TempJob);
            var calculateInverseRadiusDependency = new CalculateInverseRadiusJob
            {
                MaxDistance = neighborQueryParams.NeighborhoodSettings.MaxNeighborDistance,
                MaxRadius1 = maxRadius1,
                MaxRadius2 = maxRadius2,
                InverseRadius = inverseRadius,
                MaxTotalRadius = totalRadius,
            }.Schedule(radiusDependency);

            var initDependency = JobHandle.CombineDependencies(loadPositionsForwardsDependency, loadPositionsDependency);
            initDependency = JobHandle.CombineDependencies(initDependency, calculateInverseRadiusDependency);
            
            var fillHashMapDependency = new FillHashMapJob
            {
                Positions = otherPositions,
                InverseCellRadius = inverseRadius,
                HashMap = hashMap.AsParallelWriter()
            }.Schedule(neighborQueryParams.OtherBaseParams.EntityCount, 8, initDependency);
            
            float cosOfFOV = math.cos(math.radians(neighborQueryParams.NeighborhoodSettings.MaxFOV * 0.5f));
            var findClosestNeighborsJobHandle = new FindClosestNeighborsJob
            {
                Forwards = boidForwards,
                Positions = boidPositions,
                OtherPositions = otherPositions,
                Neighbors = neighbors,
                MaxRadius = totalRadius,
                InverseCellRadius = inverseRadius,
                MaxNumNeighbors = neighborQueryParams.NeighborhoodSettings.MaxNumNeighbors,
                HashMap = hashMap,
                Radii = neighborQueryParams.MainBaseParams.Radii,
                OtherRadii = neighborQueryParams.OtherBaseParams.Radii,
                CosOfFOV = cosOfFOV
            }.Schedule(neighborQueryParams.MainBaseParams.EntityCount, 4, fillHashMapDependency);

            hashMap.Dispose(findClosestNeighborsJobHandle);
            boidPositions.Dispose(findClosestNeighborsJobHandle);
            boidForwards.Dispose(findClosestNeighborsJobHandle);
            otherPositions.Dispose(findClosestNeighborsJobHandle);
            maxRadius1.Dispose(findClosestNeighborsJobHandle);
            maxRadius2.Dispose(findClosestNeighborsJobHandle);
            inverseRadius.Dispose(findClosestNeighborsJobHandle);
            totalRadius.Dispose(findClosestNeighborsJobHandle);
           
            return findClosestNeighborsJobHandle;
        }
    }
}