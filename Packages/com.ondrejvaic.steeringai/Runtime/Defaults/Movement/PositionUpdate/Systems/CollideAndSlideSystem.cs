using SteeringAI.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using Plane = Unity.Physics.Plane;

namespace SteeringAI.Defaults
{
	[BurstCompile]
	public partial struct SlideJob : IJobEntity
	{
		[ReadOnly] public CollisionWorld CollisionWorld;
		[ReadOnly] public float DeltaTime;
		
		public void Execute(
			in RadiusComponent radius,
			in VelocityComponent velocityComponent,
			in CollideAndSlideComponent collideAndSlide,
			ref LocalTransform localTransform)
		{
			CollisionFilter filter = CollisionFilter.Default;
			filter.CollidesWith = (uint)collideAndSlide.LayerMask.value;
			
			if(math.lengthsq(velocityComponent.Value) < math.EPSILON) return;
			
			float3 position = localTransform.Position;
			if(CollisionWorld.SphereCast(position, radius.Radius, math.normalize(velocityComponent.Value), 0.2f, out var overlapHit, filter, QueryInteraction.IgnoreTriggers))
			{
				if (overlapHit.Fraction == 0)
				{
					float3 separation = position - overlapHit.Position;
					float distanceToHit = math.length(separation);
				
					position += math.max(collideAndSlide.SkinWidth, radius.Radius - distanceToHit + math.EPSILON) * (separation / distanceToHit);

#if STEERING_DEBUG
					if (collideAndSlide.Debug)
					{
						DebugDraw.DrawSphere(position, radius.Radius - .01f, Color.cyan);
					}
#endif
				}
			}

			float long_radius = radius.Radius + collideAndSlide.SkinWidth;

			float3 velocity = velocityComponent.Value * DeltaTime;
			float3 destination = position + velocity;
			
			float distance = math.length(velocity);
			float shortSpeed = math.max(0, distance - collideAndSlide.SkinWidth);
			float3 direction = distance > 0 ? velocity / distance : new float3();

			if(!CollisionWorld.SphereCast(position + direction * collideAndSlide.SkinWidth, radius.Radius, direction, shortSpeed, out var hit, filter, QueryInteraction.IgnoreTriggers))
			{
#if STEERING_DEBUG
				if (collideAndSlide.Debug)
				{
					DebugDraw.DrawArrow(position, destination, Color.red);
					DebugDraw.DrawSphere(destination, radius.Radius, Color.red);	
				}
#endif
				localTransform.Position = destination;
				return;
			}

			float longDistance = shortSpeed * hit.Fraction;
			float shortDistance = math.max(0.0f, longDistance - collideAndSlide.SkinWidth);

			float3 longPosition = position + direction * longDistance;
			float3 normal = math.normalize(longPosition - hit.Position);
			Plane firstPlane = new Plane(normal, -math.dot(normal, hit.Position));
			
			position += direction * shortDistance;
			destination -= normal * (firstPlane.SignedDistanceToPoint(destination) - long_radius);
			velocity = destination - position;
			
#if STEERING_DEBUG
			if (collideAndSlide.Debug)
			{
				DebugDraw.DrawSphere(destination, radius.Radius, Color.green);
				DebugDraw.DrawSphere(position, radius.Radius, Color.green);
				DebugDraw.DrawArrow(position, destination, Color.green);
			}
#endif
			distance = math.length(velocity);
			shortSpeed = math.max(0, distance - collideAndSlide.SkinWidth);
			direction = distance > 0 ? velocity / distance : new float3();

			if(!CollisionWorld.SphereCast(position + direction * collideAndSlide.SkinWidth, radius.Radius, direction, shortSpeed, out hit, filter, QueryInteraction.IgnoreTriggers))
			{
				
#if STEERING_DEBUG
				if (collideAndSlide.Debug)
				{
					DebugDraw.DrawArrow(position, destination, new Color(1, 0.5f, 0));
					DebugDraw.DrawSphere(destination, radius.Radius, new Color(1, 0.5f, 0));
				}
#endif
				localTransform.Position = destination;
				return;
			}
			
			longDistance = shortSpeed * hit.Fraction;
			shortDistance = math.max(0.0f, longDistance - collideAndSlide.SkinWidth);
			
			longPosition = position + direction * longDistance;
			normal = math.normalize(longPosition - hit.Position);

			position += direction * shortDistance;

			var secondPlane = new Plane(normal, -math.dot(normal, hit.Position));
			
			float3 crease = math.select(math.normalize(math.cross(firstPlane.Normal, secondPlane.Normal)), firstPlane.Normal, math.dot(firstPlane.Normal, secondPlane.Normal) > 1 - 0.001f);
			
			float dis = math.dot(destination - position, crease);
			velocity = dis * crease;
			destination = position + velocity;
			
#if STEERING_DEBUG
			if (collideAndSlide.Debug)
			{
				DebugDraw.DrawSphere(destination, radius.Radius, Color.magenta);
				DebugDraw.DrawSphere(position, radius.Radius, Color.magenta);
				DebugDraw.DrawArrow(position, destination, Color.magenta);
			}
#endif
			distance = math.length(velocity);
			shortSpeed = math.max(0, distance - collideAndSlide.SkinWidth);
			direction = distance > 0 ? velocity / distance : new float3();
			
			if(!CollisionWorld.SphereCast(position + direction * collideAndSlide.SkinWidth, radius.Radius, direction, shortSpeed, out hit, filter, QueryInteraction.IgnoreTriggers))
			{
#if STEERING_DEBUG
				if (collideAndSlide.Debug)
				{
					DebugDraw.DrawArrow(position, destination, new Color(1, 0.8f, 0));
					DebugDraw.DrawSphere(destination, radius.Radius, new Color(1, 0.8f, 0));
				}
#endif
				localTransform.Position = destination;
				return;
			}
			
			longDistance = shortSpeed * hit.Fraction;
			shortDistance = math.max(0.0f, longDistance - collideAndSlide.SkinWidth);

#if STEERING_DEBUG
			if (collideAndSlide.Debug)
			{
				DebugDraw.DrawArrow(position, position + direction * shortDistance, Color.blue);
				DebugDraw.DrawSphere(position + direction * shortDistance, radius.Radius, Color.blue);
			}
#endif
			position += direction * shortDistance;
			localTransform.Position = position;
		}
	}
	
	[RequireMatchingQueriesForUpdate]
	[UpdateAfter(typeof(AfterSteeringSystemsGroup))]
    [UpdateBefore(typeof(UpdatePositionSystem))]
    [BurstCompile]
    public partial struct CollideAndSlide : ISystem
    {
	    private EntityQuery entityQuery;
    
	    public void OnCreate(ref SystemState state)
	    {
		    state.RequireForUpdate<PhysicsWorldSingleton>();
		    entityQuery = state.GetEntityQuery(
			    ComponentType.ReadOnly<LocalTransform>(),
			    ComponentType.ReadOnly<VelocityComponent>(),
			    ComponentType.ReadOnly<RadiusComponent>(),
			    ComponentType.ReadOnly<CollideAndSlideComponent>());
	    }

        public void OnDestroy(ref SystemState state) {}
        
        public void OnUpdate(ref SystemState state)
        {
	        float deltaTime = SystemAPI.Time.DeltaTime;
	        
	        var collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
	 
	        state.Dependency = new SlideJob
	        {
		        DeltaTime = deltaTime,
		        CollisionWorld = collisionWorld,
	        }.ScheduleParallel(entityQuery, state.Dependency);
        }
    }
}