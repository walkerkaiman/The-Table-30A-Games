using System.Runtime.CompilerServices;
using SteeringAI.Core;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace SteeringAI.Defaults
{
	[BurstCompile]
	public partial struct Move3DJob : IJobEntity
	{
		public float DeltaTime;

		public void Execute(
			in DesiredVelocityComponent desiredVelocityComponent,
			ref Movement3DComponent movement,
			ref VelocityComponent velocity,
			ref LocalTransform transform)
		{
			if (!movement.IsActive)
			{
				return;
			}
			
			float3 forward = transform.Forward();
			float3 up = transform.Up();
			
			float3 currentVelocity = velocity.Value;  
			float3 targetVelocity = desiredVelocityComponent.Value;
			float3 velocityDirection = math.normalizesafe(currentVelocity, forward);
			
			float3 acceleration = MoveUtils.CalculateNewAcceleration(
				movement.MoveOptions,
				currentVelocity,
				targetVelocity,
				velocityDirection,
				DeltaTime);

			if (movement.MoveOptions.Move)
			{
				velocity.Value = MoveUtils.CalculateNewVelocity(
					movement.MoveOptions,
					currentVelocity,
					acceleration,
					velocityDirection,
					DeltaTime);	
			}
			
			if (movement.RotationOptions.Rotate)
			{
				float3 optimalUp = CalculateOptimalUp(acceleration / DeltaTime, velocityDirection, up, movement.BankingGravity);
				float3 upRelativeToVelocity = Float3Utils.ProjectOnPlane(new float3(0, 1, 0), velocityDirection);
				upRelativeToVelocity = math.normalize(upRelativeToVelocity);
				
				float desiredAngle = math.degrees(math.acos(math.saturate(math.dot(upRelativeToVelocity, optimalUp))));
				
				float3 cross = math.cross(upRelativeToVelocity, optimalUp);
				if (math.dot(velocityDirection, cross) < 0)
				{
					desiredAngle = -desiredAngle;
				}
				
				float toDesiredAngle = desiredAngle - movement.ZAxisAngle;

				float multiplier = math.abs(toDesiredAngle) / 90;
				multiplier *= multiplier;
				multiplier = math.remap(0, 1, 0.1f, 1, multiplier);
				
				movement.ZAxisAngle += math.sign(toDesiredAngle) * math.min(math.abs(toDesiredAngle), multiplier * movement.RotationOptions.RollRotationSpeed * DeltaTime);
				
				var upRotation = Quaternion.AngleAxis(movement.ZAxisAngle, velocityDirection);
				var desiredUp = math.mul(upRotation, upRelativeToVelocity);
				
				quaternion targetRotation = quaternion.LookRotation(velocityDirection, desiredUp);
				var newRotation = Quaternion.RotateTowards(transform.Rotation, targetRotation, movement.RotationOptions.RotationSpeed * DeltaTime);
				transform.Rotation = newRotation;
				
#if STEERING_DEBUG
				if (movement.Debug)
				{	
					DebugDraw.DrawArrow(transform.Position, transform.Position + desiredUp, Color.green);
					DebugDraw.DrawArrow(transform.Position, transform.Position + velocityDirection, Color.green);
					
					DebugDraw.DrawArrow(transform.Position, transform.Position + transform.Forward() + transform.Up() * 0.01f, Color.blue);
					DebugDraw.DrawArrow(transform.Position, transform.Position + transform.Up() + transform.Right() * 0.01f, Color.blue);
				}
#endif
			}
			
#if STEERING_DEBUG
			if (movement.Debug)
			{
				DebugDraw.DrawArrow(transform.Position, transform.Position + desiredVelocityComponent.Value, new Color(0.2f, 0.95f, 0.8f));
				DebugDraw.DrawArrow(transform.Position, transform.Position + velocity.Value, Color.red);
				
				float fontScale = 0.05f;
				float spacing = 0.5f * fontScale;
				float3 offset = new float3(0, 0, fontScale * 2 + spacing * 2);
				float3 topLeft = transform.Position + new float3(0, 0.2f, 0);
				
				float3 speedEnd = DebugDraw.DrawNumber(topLeft, math.length(velocity.Value), 10, 2, fontScale, spacing, Color.white);
				float3 accelerationEnd = DebugDraw.DrawNumber(topLeft - offset, math.length(acceleration) / DeltaTime, 10, 2, fontScale, spacing, Color.white);
				float3 bottomLeft = new float3(speedEnd.x > accelerationEnd.x ? speedEnd.x : accelerationEnd.x, topLeft.y, accelerationEnd.z);
				
				DebugDraw.DrawBox(topLeft + new float3(0, 0, fontScale * 2), bottomLeft, spacing, Color.white);	
			}
#endif
		}
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private float3 CalculateOptimalUp(float3 currentAcceleration, float3 velocityDirection, float3 up, float bankingGravity)
		{
			float3 forwardComponentOfAcceleration = math.project(currentAcceleration, velocityDirection); // project on current velocity direction
			float3 upComponentOfAcceleration = math.project(currentAcceleration - forwardComponentOfAcceleration, new float3(0, 1, 0)); // project on global up - it doesn't matter how we are rotated around z
			float3 hori = currentAcceleration - forwardComponentOfAcceleration - upComponentOfAcceleration;
			
			float3 centrifugalForce = -hori;
			centrifugalForce.y += bankingGravity;
			centrifugalForce = -centrifugalForce;
			centrifugalForce = Float3Utils.ProjectOnPlane(centrifugalForce, velocityDirection);
			centrifugalForce = math.normalizesafe(centrifugalForce, up);
			return centrifugalForce;
		}
	}

	[RequireMatchingQueriesForUpdate]
	[UpdateInGroup(typeof(AfterSteeringSystemsGroup))]
    [BurstCompile]
    public partial struct Move3DSystem : ISystem
    {
	    private EntityQuery entityQuery;
    
	    public void OnCreate(ref SystemState state)
	    {
		    entityQuery = state.GetEntityQuery(
			    ComponentType.ReadWrite<LocalTransform>(),
			    ComponentType.ReadWrite<VelocityComponent>(),
			    ComponentType.ReadWrite<Movement3DComponent>(),
			    ComponentType.ReadOnly<SteeringEntityTagComponent>(),
			    ComponentType.ReadOnly<DesiredVelocityComponent>());
	    }

        public void OnDestroy(ref SystemState state)
        {
    
        }

        public void OnUpdate(ref SystemState state)
        {
	        state.Dependency = new Move3DJob
	        {
		        DeltaTime = SystemAPI.Time.DeltaTime
	        }.ScheduleParallel(entityQuery, state.Dependency);
        }
    }
}