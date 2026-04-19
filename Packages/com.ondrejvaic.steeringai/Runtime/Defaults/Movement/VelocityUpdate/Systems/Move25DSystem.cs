using System.Runtime.CompilerServices;
using SteeringAI.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace SteeringAI.Defaults
{
	[BurstCompile]
	public partial struct Move25DJob : IJobEntity
	{
		[ReadOnly] public CollisionWorld CollisionWorld;
		[ReadOnly] public float DeltaTime;

		public void Execute(
			in DesiredVelocityComponent desiredVelocity,
			in CollideAndSlideComponent collideAndSlide,
			in RadiusComponent radius,
			in Movement25DComponent movement,
			ref Movement25DStateComponent movementState,
			ref VelocityComponent velocity,
			ref LocalTransform transform)
		{
			if(!movement.IsActive) return;
			
			if (movement.MoveOptions.Move) 
			{
				velocity.Value -= movementState.xD;
			}

			movementState.xD = float3.zero;
				
			float maxAcc = movement.UpAlignmentAcceleration * DeltaTime;
			float3 toTargetUp = movementState.TargetUp - movementState.CurrentUp;
			float toTargetUpLength = math.length(toTargetUp);
			if (toTargetUpLength <= maxAcc)
			{
				movementState.CurrentUp = movementState.TargetUp;		
			}
			else
			{
				movementState.CurrentUp += maxAcc * (toTargetUp / toTargetUpLength);		
			}
			
			float3 forward = transform.Forward();
			float3 up = transform.Up();
			float3 position = transform.Position;
			quaternion rotation = transform.Rotation;
			
			float3 currentVelocity = velocity.Value;
			float3 velocityDirection = math.normalizesafe(currentVelocity, forward);
			
			CollisionFilter filter = CollisionFilter.Default;
			filter.CollidesWith = movement.LayerMask; 
			
			MoveState moveState = movementState.MoveState;	
			switch (moveState)
			{
				case MoveState.Walking:
				{
					float3 surfaceUp = movementState.CurrentUp;

					float angle = math.degrees(calculateVelocityAngle(desiredVelocity.Value, surfaceUp));

					float3 targetVelocity = desiredVelocity.Value;
					targetVelocity = new float3(targetVelocity.x, 0, targetVelocity.z);
					targetVelocity = Float3Utils.ProjectOnPlane(targetVelocity, surfaceUp);
					targetVelocity = math.normalizesafe(targetVelocity);

					float velocityFraction;
					if (targetVelocity.y >= 0)
					{
						float angleFraction =
							math.remap(movement.MinAngleToSlide, movement.MaxAngleToSlide, 0, 1, angle);
						angleFraction = math.saturate(angleFraction);
						velocityFraction = 1 - angleFraction * angleFraction;
					}
					else
					{
						float angleFraction = math.remap(movement.MinAngleToSpeedUp, movement.MaxAngleToSpeedUp, 0, 1,
							angle);
						angleFraction = math.saturate(angleFraction);
						velocityFraction = 1 + angleFraction * angleFraction * movement.MaxDownhillSpeedUp;
					}

					float targetVelocityMagnitude = math.min(math.length(desiredVelocity.Value),
						movement.MoveOptions.MaxSpeed * velocityFraction);
					targetVelocity *= targetVelocityMagnitude;

					float3 acceleration = MoveUtils.CalculateNewAcceleration(
						movement.MoveOptions,
						currentVelocity,
						targetVelocity,
						velocityDirection,
						DeltaTime);

					float3 newVelocity = MoveUtils.CalculateNewVelocity(
						movement.MoveOptions,
						currentVelocity,
						acceleration,
						velocityDirection,
						DeltaTime);

					if (movement.MoveOptions.Move)
					{
						velocity.Value = newVelocity;
					}

					float newVelocityMagnitude = math.length(newVelocity);
					float distanceChange = newVelocityMagnitude * DeltaTime;

					if (movement.RotationOptions.Rotate)
					{
						quaternion startRotation = quaternion.LookRotation(forward, up);
						quaternion targetRotation = quaternion.LookRotation(velocityDirection, movementState.CurrentUp);
						if (!float.IsNaN(startRotation.value.x) && !float.IsNaN(targetRotation.value.y))
						{
							var newRotation = MoveUtils.Sterp(
								startRotation,
								targetRotation,
								up,
								movement.RotationOptions.SwingRotationSpeed * DeltaTime,
								movement.RotationOptions.RotationSpeed * DeltaTime);

							transform.Rotation = newRotation;
						}

#if STEERING_DEBUG
						if (movement.Debug)
						{
							DebugDraw.DrawArrow(transform.Position, transform.Position + movementState.CurrentUp,
								Color.green);
							DebugDraw.DrawArrow(transform.Position, transform.Position + velocityDirection,
								Color.green);

							DebugDraw.DrawArrow(transform.Position,
								transform.Position + transform.Forward() + transform.Up() * 0.01f, Color.blue);
							DebugDraw.DrawArrow(transform.Position,
								transform.Position + transform.Up() + transform.Right() * 0.01f, Color.blue);
						}
#endif
					}

#if STEERING_DEBUG
					if (movement.Debug)
					{
						DebugDraw.DrawArrow(transform.Position, transform.Position + desiredVelocity.Value, new Color(0.2f, 0.95f, 0.8f));
						DebugDraw.DrawArrow(transform.Position, transform.Position + velocity.Value, Color.red);

						float fontScale = 0.05f;
						float spacing = 0.5f * fontScale;
						float3 offset = new float3(0, 0, fontScale * 2 + spacing * 2);
						float3 topLeft = transform.Position + new float3(0, 0.2f, 0);

						float3 speedEnd = DebugDraw.DrawNumber(topLeft, math.length(velocity.Value), 10, 2, fontScale,
							spacing, Color.white);
						float3 accelerationEnd = DebugDraw.DrawNumber(topLeft - offset,
							math.length(acceleration) / DeltaTime, 10, 2, fontScale, spacing, Color.white);
						float3 bottomLeft = new float3(speedEnd.x > accelerationEnd.x ? speedEnd.x : accelerationEnd.x,
							topLeft.y, accelerationEnd.z);

						DebugDraw.DrawBox(topLeft + new float3(0, 0, fontScale * 2), bottomLeft, spacing, Color.white);
					}
#endif
					float3 forwardCheckDirection = math.normalizesafe(new float3(desiredVelocity.Value.x, 0, desiredVelocity.Value.z), float3.zero);
					float3 forwardCheckStart = position - forwardCheckDirection * collideAndSlide.SkinWidth; 
					float forwardCheckDistance = distanceChange + collideAndSlide.SkinWidth * 4 + radius.Radius * .5f + 0.01f; 
					
					// if collision in direction of movement
					if (CollisionWorld.SphereCast(
						    forwardCheckStart,
						    radius.Radius - collideAndSlide.SkinWidth,
						    forwardCheckDirection,
						    forwardCheckDistance,
						    out var hitAlongVelocity, filter, QueryInteraction.IgnoreTriggers))
					{
						
#if STEERING_DEBUG
						if (movement.Debug)
						{
							DebugDraw.DrawSphere(position, radius.Radius, Color.red);
						}
#endif

						float3 flatSurfaceNormal = math.normalize(new float3(hitAlongVelocity.SurfaceNormal.x, 0, hitAlongVelocity.SurfaceNormal.z));
						float hitUpDot = math.saturate(math.dot(hitAlongVelocity.SurfaceNormal, new float3(0, 1, 0)));
						float hitUpAngle = math.acos(hitUpDot);
						float collisionDistance = forwardCheckDistance * hitAlongVelocity.Fraction;
						
						// if can move on the surface  
						if (hitUpAngle < math.radians(movement.MaxAngleToSlide))
						{
							if (hitUpAngle > math.radians(movement.MaxAngleToSlide - 3) // almost too steep to walk
							    && math.lengthsq(velocity.Value) < 0.01f)				// moving less than 0.1 m/s
							{
								movementState.TargetUp = new float3(0, 1, 0);
								movementState.MoveState = MoveState.Falling;
								movementState.IsSteppingUp = false;
								movementState.IsSteppingDown = false;
								return;
							}
							
							movementState.TargetUp = hitAlongVelocity.SurfaceNormal;
							
							float3 toSurfaceCheckStart = position - movementState.TargetUp * collideAndSlide.SkinWidth;
							float toSurfaceCheckDistance = collideAndSlide.SkinWidth * 5 + 0.01f;
							
							if (CollisionWorld.SphereCast(
								    toSurfaceCheckStart,
								    radius.Radius - collideAndSlide.SkinWidth,
								    -movementState.TargetUp,
								    toSurfaceCheckDistance,
								    out var hitToSurface, filter, QueryInteraction.IgnoreTriggers))
							{
								float snapDist = hitToSurface.Fraction * toSurfaceCheckDistance - collideAndSlide.SkinWidth * 3;
								snapDist = math.max(0, snapDist);
								float3 toSurface = -hitToSurface.SurfaceNormal * snapDist / DeltaTime;
								
								if (movement.MoveOptions.Move) 
								{
									velocity.Value += toSurface;
									movementState.xD += toSurface;
								}
							}
							
							if (movementState.IsSteppingDown)
							{
								if (movement.MoveOptions.Move) 
								{
									velocity.Value = Float3Utils.ProjectOnPlane(movementState.StartSteppingDownVelocity, movementState.TargetUp);
								}

								movementState.IsSteppingDown = false;
							}
							
							movementState.IsSteppingUp = false;
							return;
						}
						
						float longerDistanceChange = distanceChange + collideAndSlide.SkinWidth * 6 + 0.01f;
						float againstWallAngle = calculateAngle(-flatSurfaceNormal, forwardCheckDirection);
						if (againstWallAngle < 35 && collisionDistance <= longerDistanceChange)
						{
							bool canStep = canStepCheck(
								position, forwardCheckDirection, distanceChange, 
								hitUpAngle, collideAndSlide.SkinWidth, movement.StepHeight, 
								radius.Radius, filter
#if STEERING_DEBUG
								,movement.Debug
#endif
								);
							
							if (canStep)
							{
								// if started step up this frame, save velocity at the beginning of step up
								if (!movementState.IsSteppingUp)
								{
									movementState.StartSteppingVelocity = forwardCheckDirection * movement.MoveOptions.MaxSpeed;
									movementState.IsSteppingUp = true;
								}
								
								if (movement.MoveOptions.Move)
								{
									var steppingDirections = calculateSteppingDirections(movementState.StartSteppingVelocity, movement.MaxAngleToSlide);
									velocity.Value = steppingDirections.forward * movement.MoveOptions.MaxSpeed;
									movementState.TargetUp = steppingDirections.up;
								}
#if STEERING_DEBUG
								if (movement.Debug)
								{
									DebugDraw.DrawSphere(position, radius.Radius, Color.cyan);
								}
#endif								
								movementState.IsSteppingDown = false;
								return;	
							}
						}
						else
						{
							float longDistanceChange = distanceChange + collideAndSlide.SkinWidth * 4 + 0.01f;
							if (collisionDistance <= longDistanceChange)
							{
								// if for example tilted wall, project velocity on the projected normal
								float speed = math.length(velocity.Value);
								float3 projectedVelocity = Float3Utils.ProjectOnPlane(velocity.Value, flatSurfaceNormal);
								
								if (movement.MoveOptions.Move) 
								{
									velocity.Value = Float3Utils.Rescale(projectedVelocity, speed);
								}
#if STEERING_DEBUG
								if (movement.Debug)
								{
									DebugDraw.DrawSphere(transform.Position, radius.Radius - 0.05f, Color.yellow);	
								}
#endif
							}	
						}
					}
					movementState.IsSteppingUp = false;

					float3 downStart = position + new float3(0, 1, 0) * collideAndSlide.SkinWidth;
					float downDistance = collideAndSlide.SkinWidth * 5 + 0.01f;
					// if no collision found in direction of movement, check if grounded
					if (CollisionWorld.SphereCast(
						    downStart,
						    radius.Radius - collideAndSlide.SkinWidth,
						    new float3(0, -1, 0),
						    downDistance,
						    out var hitDown, filter, QueryInteraction.IgnoreTriggers))
					{
						float hitUpAngle = upAngle(hitDown.SurfaceNormal);
						// if can move on the surface
						if (hitUpAngle < movement.MaxAngleToSlide)
						{
#if STEERING_DEBUG
							if (movement.Debug)
							{
								DebugDraw.DrawSphere(position, radius.Radius, Color.green);
							}
#endif
							if (hitUpAngle > math.radians(movement.MaxAngleToSlide - 3) // almost too steep to walk
							    && math.lengthsq(velocity.Value) < 0.01f)				// moving less than 0.1 m/s
							{
								movementState.TargetUp = new float3(0, 1, 0);
								movementState.MoveState = MoveState.Falling;
								movementState.IsSteppingDown = false;
								movementState.IsSteppingUp = false;
								return;
							}
							
							movementState.TargetUp = hitDown.SurfaceNormal;
							if (movementState.IsSteppingDown)
							{
								if (movement.MoveOptions.Move) 
								{
									velocity.Value = Float3Utils.ProjectOnPlane(movementState.StartSteppingDownVelocity, hitDown.SurfaceNormal);
									movementState.IsSteppingDown = false;
								}
							}
							else
							{
								if (movement.MoveOptions.Move) 
								{
									velocity.Value = Float3Utils.ProjectOnPlane(velocity.Value, movementState.CurrentUp);
								}

								if (CollisionWorld.SphereCast(
									    position + hitDown.SurfaceNormal * collideAndSlide.SkinWidth,
									    radius.Radius - collideAndSlide.SkinWidth,
									    -hitDown.SurfaceNormal,
									    downDistance, 
									    out var toSurfaceHit, filter, QueryInteraction.IgnoreTriggers))
								{
									float snapDist = toSurfaceHit.Fraction * downDistance - collideAndSlide.SkinWidth * 3;
									snapDist = math.max(0, snapDist);
									float3 toSurface = -hitDown.SurfaceNormal * snapDist / DeltaTime;

									if (movement.MoveOptions.Move) 
									{
										velocity.Value += toSurface;
										movementState.xD += toSurface;
									}
								}
							}
							return;
						}
					}

					var stepDownCheck = canStepDownCheck(
						position, forwardCheckDirection, movement.StepHeight, 
						radius.Radius, collideAndSlide.SkinWidth, filter);
					
					if (stepDownCheck.canStepDown)
					{
						float hitUpAngle = upAngle(stepDownCheck.surfaceNormal);
						if (hitUpAngle < movement.MaxAngleToSlide)
						{
							movementState.TargetUp = stepDownCheck.surfaceNormal;
							if (!movementState.IsSteppingDown)
							{
								movementState.StartSteppingDownVelocity = velocity.Value;
								movementState.IsSteppingDown = true;
							}
							if (movement.MoveOptions.Move)
							{
								velocity.Value += new float3(0, movement.StepDownGravity * DeltaTime, 0);
								velocity.Value = new float3(velocity.Value.x, math.max(velocity.Value.y, -10), velocity.Value.z);
							}
							
#if STEERING_DEBUG
							if (movement.Debug)
							{
								DebugDraw.DrawSphere(position, radius.Radius, Color.magenta);
							}
#endif
							return;
						}
					}
					
					movementState.IsSteppingUp = false;
					movementState.IsSteppingDown = false;
					movementState.MoveState = MoveState.Falling;
					movementState.TargetUp = new float3(0, 1, 0);
					return;
				}
				case MoveState.Falling:
				{
					float3 gravityAcceleration = new float3(0, movement.AirGravity * DeltaTime, 0);
					float3 newVelocity = velocity.Value + gravityAcceleration;
					float velocityMagnitude = math.length(newVelocity);
					
					float3 dragAcceleration = newVelocity * movement.AirLinearDrag * DeltaTime;
					dragAcceleration = Float3Utils.ClampSize(dragAcceleration, 0, velocityMagnitude - 0.01f);
					newVelocity -= dragAcceleration;

					float3 targetForward = math.normalizesafe(Float3Utils.ProjectOnPlane(forward, new float3(0, 1, 0)), forward);
					
					quaternion targetRotation = quaternion.LookRotation(targetForward, new float3(0, 1, 0));
					transform.Rotation = Quaternion.RotateTowards(rotation, targetRotation, movement.AirAlignmentSpeed * DeltaTime);
					if (movement.MoveOptions.Move)
					{
						velocity.Value = newVelocity;
					}

#if STEERING_DEBUG
					if (movement.Debug)
					{
						DebugDraw.DrawSphere(transform.Position, radius.Radius, Color.black);
					}
#endif
					if (CollisionWorld.SphereCast(
						    position - new float3(0, -1, 0) * collideAndSlide.SkinWidth,
						    radius.Radius - collideAndSlide.SkinWidth,
						    new float3(0, -1, 0),
						    collideAndSlide.SkinWidth * 6,
						    out var hitDown, filter, QueryInteraction.IgnoreTriggers))
					{
						float hitUpAngle = upAngle(hitDown.SurfaceNormal);
						if (hitUpAngle < movement.MaxAngleToSlide - 5)
						{
							movementState.MoveState = MoveState.Walking;
							movementState.TargetUp = hitDown.SurfaceNormal;
							movementState.CurrentUp = hitDown.SurfaceNormal;
							if (movement.MoveOptions.Move) 
							{
								velocity.Value = Float3Utils.ProjectOnPlane(velocity.Value, hitDown.SurfaceNormal);
							}
						}
					}
					return;
				}
			}
		}
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private float calculateVelocityAngle(float3 velocity, float3 up)
		{
			float3 flatVelocity = new float3(velocity.x, 0, velocity.z);
			float3 flatDirectionBeforeProject = math.normalize(flatVelocity);  
			float3 flatDirectionAfterProject = Float3Utils.ProjectOnPlane(flatDirectionBeforeProject, up);
			flatDirectionAfterProject = math.normalize(flatDirectionAfterProject);
			float angle = math.acos(math.saturate(math.dot(flatDirectionBeforeProject, flatDirectionAfterProject)));
			return angle;
		}
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private float upAngle(float3 direction)
		{
			float angle = math.acos(math.saturate(math.dot(direction, new float3(0, 1, 0))));
			return math.degrees(angle);
		}
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private float calculateAngle(float3 direction1, float3 direction2)
		{
			float angle = math.acos(math.saturate(math.dot(direction1, direction2)));
			return math.degrees(angle);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private (float3 forward, float3 up) calculateSteppingDirections(float3 startSteppingVelocity, float maxAngleToSlide)
		{
			float startSteppingSpeed = math.length(startSteppingVelocity);
			float3 startSteppingDirection = startSteppingVelocity / startSteppingSpeed;
			float3 right = math.cross(startSteppingDirection, new float3(0, 1, 0));
			quaternion rot = Quaternion.AngleAxis(maxAngleToSlide, right);
			float3 steppingDirection = math.mul(rot, startSteppingDirection);
			float3 steppingUp = math.cross(right, steppingDirection);
			return (steppingDirection, steppingUp);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private (bool canStepDown, float3 surfaceNormal) canStepDownCheck(
			float3 position,
			float3 flatDirection,
			float stepHeight,
			float radius,
			float skinWidth,
			CollisionFilter filter)
		{
			bool hitAround = CollisionWorld.SphereCast(
				position,
				radius + skinWidth * 5,
				new float3(0, -1, 0), 
				skinWidth * 4, filter, QueryInteraction.IgnoreTriggers);

			if (hitAround)
			{
				return (false, float3.zero);	
			}
			
			float3 stepForwardStart = position + new float3(0, 1, 0) * skinWidth;
			float stepDownDistance = stepHeight + radius + skinWidth * 2;
			
			// if nothing directly in front, check if can step down
			bool canGoForward = !CollisionWorld.SphereCast(
					stepForwardStart,
					radius - skinWidth,
					flatDirection,
					radius, filter, QueryInteraction.IgnoreTriggers);
			
			// if can go forward and then down, step down
			if (canGoForward && CollisionWorld.SphereCast(
				    position + new float3(0, 1, 0) * skinWidth + flatDirection * radius,
				    radius - skinWidth,
				    new float3(0, -1, 0),
				    stepDownDistance, 
				    out var stepDownHit, filter, QueryInteraction.IgnoreTriggers))
			{
				return (true, stepDownHit.SurfaceNormal);
			}
			
			return (false, float3.zero);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool canStepCheck(
			float3 position,
			float3 flatDirection,
			float moveDistance,
			float surfaceAngle,
			float skinWidth,
			float stepHeight,
			float radius,
			CollisionFilter filter
#if STEERING_DEBUG
			,bool debug
#endif
		)
		{
			// would be better to check down first how high we already are
			float3 upStart = position - new float3(0, 1, 0) * skinWidth;
			float upDistance = stepHeight + skinWidth * 2;
			
			float3 forwardStart = position + new float3(0, 1, 0) * stepHeight - flatDirection * skinWidth;
			float forwardDistance = skinWidth * 5 + moveDistance + radius * .5f + 0.01f;
			forwardDistance += stepHeight * math.tan(math.PI * 0.5f - surfaceAngle); // extra distance forward for tileted surfaces
							
#if STEERING_DEBUG
			if (debug)
			{
				DebugDraw.DrawSphere(forwardStart, radius - skinWidth, new Color(0.5f, 0.5f, 0.5f));
				DebugDraw.DrawSphere(forwardStart + flatDirection * forwardDistance, radius - skinWidth, new Color(1f, 0.5f, 0.5f));
			}
#endif
			bool canStep = 
				!CollisionWorld.SphereCast(
					forwardStart,
					radius - skinWidth,
					flatDirection,
					forwardDistance, filter, QueryInteraction.IgnoreTriggers)
				&&
				!CollisionWorld.SphereCast(
					upStart,
					radius - skinWidth,
					new float3(0, 1, 0),
					upDistance, filter, QueryInteraction.IgnoreTriggers);
			
			return canStep;
		}
	}
	
	
	[RequireMatchingQueriesForUpdate]
	[UpdateInGroup(typeof(AfterSteeringSystemsGroup))]
    [BurstCompile]
    public partial struct Move25DSystem : ISystem
    {
	    private EntityQuery entityQuery;
	    
	    public void OnCreate(ref SystemState state)
	    {
		    state.RequireForUpdate<PhysicsWorldSingleton>();
		    entityQuery = state.GetEntityQuery(
			    ComponentType.ReadOnly<SteeringEntityTagComponent>(),
			    ComponentType.ReadOnly<RadiusComponent>(),
			    ComponentType.ReadOnly<DesiredVelocityComponent>(),
			    ComponentType.ReadOnly<CollideAndSlideComponent>(),
			    ComponentType.ReadOnly<Movement25DComponent>(),
			    ComponentType.ReadWrite<Movement25DStateComponent>(),
			    ComponentType.ReadWrite<LocalTransform>(),
			    ComponentType.ReadWrite<VelocityComponent>());
	    }

	    public void OnUpdate(ref SystemState state)
        {
	        float deltaTime = SystemAPI.Time.DeltaTime;
	        var collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
	        
	        state.Dependency = new Move25DJob
			{
				CollisionWorld = collisionWorld,
				DeltaTime = deltaTime,
			}.ScheduleParallel(entityQuery, state.Dependency);
        }
        
        public void OnDestroy(ref SystemState state) { }

    }
}