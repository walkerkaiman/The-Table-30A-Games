using SteeringAI.Core;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace SteeringAI.Defaults
{
	[BurstCompile]
	public partial struct Move2DJob : IJobEntity
	{
		public float DeltaTime;

		public void Execute(
			in Movement2DComponent movementComponent,
			in DesiredVelocityComponent desiredVelocityComponent,
			ref VelocityComponent velocityComponent,
			ref LocalTransform transform)
		{

			if (!movementComponent.IsActive)
			{
				return;
			}

			float3 currentVelocity = new float3(velocityComponent.Value.x, 0, velocityComponent.Value.z);
			float3 targetVelocity = new float3(desiredVelocityComponent.Value.x, 0, desiredVelocityComponent.Value.z);

			float3 velocityDirection = math.normalizesafe(currentVelocity, transform.Forward());

			float3 acceleration = MoveUtils.CalculateNewAcceleration(
				movementComponent.MoveOptions,
				currentVelocity,
				targetVelocity,
				velocityDirection,
				DeltaTime);

			if (movementComponent.MoveOptions.Move)
			{
				velocityComponent.Value = MoveUtils.CalculateNewVelocity(
					movementComponent.MoveOptions,
					currentVelocity,
					acceleration,
					velocityDirection,
					DeltaTime);
			}

			if(movementComponent.RotationOptions.Rotate)
			{
				quaternion targetRotation = quaternion.LookRotation(velocityDirection, new float3(0, 1, 0));
				float rotationStep = movementComponent.RotationOptions.RotationSpeed * DeltaTime;
				transform.Rotation = Quaternion.RotateTowards(transform.Rotation, targetRotation, rotationStep);
			}

#if STEERING_DEBUG
			if (movementComponent.Debug)
			{
				DebugDraw.DrawArrow(transform.Position, transform.Position + desiredVelocityComponent.Value, new Color(0.2f, 0.95f, 0.8f));
				DebugDraw.DrawArrow(transform.Position, transform.Position + velocityComponent.Value, Color.red);
				
				float fontScale = 0.05f;
				float spacing = 0.5f * fontScale;
				float3 offset = new float3(0, 0, fontScale * 2 + spacing * 2);
				float3 topLeft = transform.Position + new float3(0, 0.2f, 0);
				
				float3 speedEnd = DebugDraw.DrawNumber(topLeft, math.length(velocityComponent.Value), 10, 2, fontScale, spacing, Color.white);
				float3 accelerationEnd = DebugDraw.DrawNumber(topLeft - offset, math.length(acceleration) / DeltaTime, 10, 2, fontScale, spacing, Color.white);
				float3 bottomLeft = new float3(speedEnd.x > accelerationEnd.x ? speedEnd.x : accelerationEnd.x, topLeft.y, accelerationEnd.z);
				
				DebugDraw.DrawBox(topLeft + new float3(0, 0, fontScale * 2), bottomLeft, spacing, Color.white);
			}
#endif
		}
	}

	[RequireMatchingQueriesForUpdate]
	[UpdateInGroup(typeof(AfterSteeringSystemsGroup))]
	[BurstCompile]
	public partial struct Move2DSystem : ISystem
	{
		private EntityQuery entityQuery;

		public void OnCreate(ref SystemState state)
		{
			entityQuery = state.GetEntityQuery(
				ComponentType.ReadWrite<LocalTransform>(),
				ComponentType.ReadWrite<VelocityComponent>(),
				ComponentType.ReadOnly<SteeringEntityTagComponent>(),
				ComponentType.ReadOnly<Movement2DComponent>(),
				ComponentType.ReadOnly<DesiredVelocityComponent>());
		}

		public void OnUpdate(ref SystemState state)
		{
			state.Dependency = new Move2DJob
			{
				DeltaTime = SystemAPI.Time.DeltaTime
			}.ScheduleParallel(entityQuery, state.Dependency);
		}

		public void OnDestroy(ref SystemState state)
		{
		}
	}
}