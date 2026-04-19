using System.Runtime.CompilerServices;
using SteeringAI.Core;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace SteeringAI.Defaults
{
    [BurstCompile]
    public static class MoveUtils
    {
	    [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 CalculateNewAcceleration(MovementOptions movementOptions, float3 currentVelocity, float3 targetVelocity,  float3 velocityDirection, float deltaTime)
        {
	        float targetVelocityLength = math.length(targetVelocity);
	        targetVelocity = (targetVelocity / targetVelocityLength) * math.min(movementOptions.MaxSpeed, targetVelocityLength); 
	        
			float3 velocityDiff = targetVelocity - currentVelocity;
			if (math.length(velocityDiff) < math.EPSILON) { return float3.zero; }
			
			float3 velocityDiffProjected = math.project(velocityDiff, velocityDirection);
			float accelerationDirectionProjectedLength = math.length(velocityDiffProjected);
			float3 accelerationProjected = new float3();
			if(accelerationDirectionProjectedLength > math.EPSILON)
			{
				float3 accelerationDirectionProjected = velocityDiffProjected / accelerationDirectionProjectedLength;
				accelerationProjected = accelerationDirectionProjected * math.min(movementOptions.MaxForwardAcceleration * deltaTime, accelerationDirectionProjectedLength);
			}
			
			float3 velocityDiffRejected = velocityDiff - velocityDiffProjected;
			float accelerationDirectionRejectedLength = math.length(velocityDiffRejected);
			float3 accelerationRejected = new float3();
			if(accelerationDirectionRejectedLength > math.EPSILON)
			{
				float3 accelerationDirectionRejected = velocityDiffRejected / accelerationDirectionRejectedLength;
				accelerationRejected = accelerationDirectionRejected * math.min(movementOptions.MaxRightAcceleration * deltaTime, accelerationDirectionRejectedLength);
			}
			
			// float velocityDiffLength = math.length(velocityDiff);
			// float t = accelerationDirectionProjectedLength / velocityDiffLength;
			// float l = accelerationDirectionRejectedLength / velocityDiffLength;
			//
			// accelerationProjected *= t;
			// accelerationRejected *= l;
			
			float3 desiredAcceleration = accelerationProjected + accelerationRejected;
			return desiredAcceleration;
		}

	    [MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float3 CalculateNewVelocity(MovementOptions movementOptions, float3 currentVelocity, float3 currentAcceleration, float3 velocityDirection, float deltaTime)
		{
			float3 accelerationDrag = -velocityDirection * math.lengthsq(currentVelocity) * movementOptions.Friction * deltaTime; 
			float3 acceleration = currentAcceleration + accelerationDrag;
			float3 velocity = currentVelocity + acceleration;
				
			float velocitySqrMagnitude = math.lengthsq(velocity);
			if (Float3Utils.IsNan(velocity) || velocitySqrMagnitude < math.EPSILON)
			{
				return float3.zero;
			}
				
			var clampedVelocity = Float3Utils.ClampSize(velocity, 0, movementOptions.MaxSpeed);
			return clampedVelocity;
		}

	    [MethodImpl(MethodImplOptions.AggressiveInlining)]
	    public static quaternion CalculateNewRotation(quaternion currentRotation, float3 forward, float3 up, float3 desiredForward, float3 desiredUp, RotationOptions3D rotationOptions, float deltaTime)
	    {
		    quaternion desiredForwardRotation = quaternion.LookRotation(desiredForward, up);
		    quaternion newForwardRotation;
		    if (Quaternion.Angle(currentRotation, desiredForwardRotation) > math.EPSILON)
		    {
			    newForwardRotation = Quaternion.RotateTowards(currentRotation, desiredForwardRotation, rotationOptions.RotationSpeed * deltaTime);	
		    }
		    else
		    {
			    newForwardRotation = currentRotation;
		    }
		    
		    quaternion desiredUpRotation = quaternion.LookRotation(forward, desiredUp);
		    quaternion newUpRotation;
		    if (Quaternion.Angle(newForwardRotation, desiredUpRotation) > math.EPSILON)
		    {
			    newUpRotation = Quaternion.RotateTowards(newForwardRotation, desiredUpRotation, rotationOptions.RollRotationSpeed * deltaTime);
		    }
		    else
		    {
			    newUpRotation = newForwardRotation;
		    }

		    return newUpRotation;
	    }
	    
	    //From https://allenchou.net/2018/05/game-math-swing-twist-interpolation-sterp/
	    [MethodImpl(MethodImplOptions.AggressiveInlining)]
	    public static Quaternion Sterp(Quaternion a, Quaternion b, Vector3 twistAxis, float t, float v)
	    {
		    Quaternion deltaRotation = b * Quaternion.Inverse(a);
		    if (Quaternion.Angle(deltaRotation, quaternion.identity) < 0.01)
		    {
			    return b;
		    }
		    
		    Quaternion swingFull;
		    Quaternion twistFull;
		    DecomposeSwingTwist(deltaRotation, twistAxis, out swingFull, out twistFull);
 
		    Quaternion swing =
			    Quaternion.Slerp(Quaternion.identity, swingFull, t);
		    Quaternion twist =
			    Quaternion.Slerp(Quaternion.identity, twistFull, v);
 
		    return swing * twist * a;
	    }
	    
	    //From https://allenchou.net/2018/05/game-math-swing-twist-interpolation-sterp/
	    [MethodImpl(MethodImplOptions.AggressiveInlining)]
	    public static void DecomposeSwingTwist(Quaternion q, Vector3 twistAxis, out Quaternion swing, out Quaternion twist)
	    {
		    Vector3 r = new Vector3(q.x, q.y, q.z);
 
			// singularity: rotation by 180 degree
		    if (r.sqrMagnitude < math.EPSILON)
		    {
			    Vector3 rotatedTwistAxis = q * twistAxis;
			    Vector3 swingAxis = Vector3.Cross(twistAxis, rotatedTwistAxis);
 
			    if (swingAxis.sqrMagnitude > math.EPSILON)
			    {
				    float swingAngle = Vector3.Angle(twistAxis, rotatedTwistAxis);
				    swing = Quaternion.AngleAxis(swingAngle, swingAxis);
			    }
			    else
			    {
				    // more singularity:
					// rotation axis parallel to twist axis
				    swing = Quaternion.identity; // no swing
			    }
 
				// always twist 180 degree on singularity
			    twist = Quaternion.AngleAxis(180.0f, twistAxis);
			    return;
		    }
 
			// meat of swing-twist decomposition
		    Vector3 p = Vector3.Project(r, twistAxis);
		    twist = new Quaternion(p.x, p.y, p.z, q.w);
		    twist = Normalize(twist);
		    swing = q * Quaternion.Inverse(twist);
	    }
	    
	    
	    [MethodImpl(MethodImplOptions.AggressiveInlining)]
	    public static float Magnitude(Quaternion q)
	    {
		    return math.sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
	    }
	    
	    
	    [MethodImpl(MethodImplOptions.AggressiveInlining)]
	    public static Quaternion Normalize(Quaternion q)
	    {
		    float magInv = 1.0f / Magnitude(q);
		    return new Quaternion(magInv * q.x, magInv * q.y, magInv * q.z, magInv * q.w);
	    }
    }
}