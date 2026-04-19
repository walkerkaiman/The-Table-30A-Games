using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace SteeringAI.Core
{
    [Serializable]
    public struct RaycastSettings
    {
        public int NumRays;
        public float MaxDistance;
        public LayerMask LayerMask;
    }
    
    public struct CreateRaysParams
    {
        [ReadOnly] public RaycastSettings RaySettings;
        [ReadOnly] public BaseBehaviorParams MainBaseParams;
    }
    
    public struct RayData
    {
        public float3 Origin;
        public float3 Direction;
        public float MaxDistance;
    }
    
    public interface ICreateRaysJobWrapper
    {
        public JobHandle Schedule(
            SystemBase systemBase,
            in CreateRaysParams createRaysParams,
            in NativeArray<RayData> rayDatas,
            JobHandle dependency);
    }
}