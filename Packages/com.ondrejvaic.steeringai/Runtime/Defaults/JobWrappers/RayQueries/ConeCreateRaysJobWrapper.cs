// --------------------------------------------------------------------------
// Direction calculation based on:
// https://github.com/SebLague/Boids/blob/master/Assets/Scripts/BoidHelper.cs
// --------------------------------------------------------------------------

// MIT License
//
// Copyright (c) 2019 Sebastian Lague
// Copyright (c) 2024 Ondrej Vaic
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// 	of this software and associated documentation files (the "Software"), to deal
// 	in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// 	furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// 	copies or substantial portions of the Software.
//
// 	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// 	IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// 	FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// 	AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// 	LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using SteeringAI.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace SteeringAI.Defaults
{
	[BurstCompile]
	struct ConeCreateRaysJob : ICreateRaysJob
	{
		public SweepRaysSettings RayCreationSettings;
		public float NormalizedFOV;
		public float MaxDistance;
		
		private const float ANGLE_INCREMENT = math.PI * 2 * 1.618f;

		public RayData Execute(int rayIndex, in EntityInformation<SteeringEntityTagComponent> entity)
		{
			float t = rayIndex * NormalizedFOV;  
			float inclination = math.acos (1 - 2 * t);
			float azimuth = ANGLE_INCREMENT * rayIndex;
			
			float3 direction = new float3(
				math.sin(inclination) * math.cos(azimuth),
				math.sin(inclination) * math.sin(azimuth),
				math.cos(inclination));
			
			direction = math.normalizesafe(direction, new float3(0, -1, 0));
			quaternion rotation = quaternion.Euler(RayCreationSettings.Rotation);
			direction = math.mul(rotation, direction);

			float3 origin = entity.LocalToWorld.Value.TransformPoint(RayCreationSettings.Offset);
			direction = math.normalize(entity.LocalToWorld.Value.TransformDirection(direction));
			
			return new RayData
			{
				Origin = origin,
				Direction = direction,
				MaxDistance = MaxDistance 
			};
		}
	} 
	  
	[JobWrapper]
	[Serializable]
	public class ConeCreateRaysJobWrapper : ICreateRaysJobWrapper
	{ 
		[SerializeField] private SweepRaysSettings rayCreationSettings = SweepRaysSettings.Preset;

		public JobHandle Schedule( 
			SystemBase systemBase,
			in CreateRaysParams createRaysParams,
			in NativeArray<RayData> rayDatas,
			JobHandle dependency)
		{
			return new ConeCreateRaysJob
			{
				RayCreationSettings = rayCreationSettings,
				NormalizedFOV = rayCreationSettings.FOV / 360f / createRaysParams.RaySettings.NumRays,
				MaxDistance = createRaysParams.RaySettings.MaxDistance
			}.Schedule(
				createRaysParams,
				rayDatas,
				1,
				dependency);
		}
	}
}