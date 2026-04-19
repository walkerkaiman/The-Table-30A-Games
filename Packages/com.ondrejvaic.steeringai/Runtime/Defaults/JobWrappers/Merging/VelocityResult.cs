using SteeringAI.Core;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace SteeringAI.Defaults
{
    public struct VelocityResult 
#if STEERING_DEBUG
        : IValidable, IToStringFixed, IDebugDrawable
#endif
    {
        public float3 Direction { get; }
        public float Speed { get; }
        public float DirectionDesire { get; }
        public float SpeedDesire { get; }
        public byte Priority { get; }
        
#if STEERING_DEBUG
        public Color Color { get; set; }
#endif

        public VelocityResult(float3 direction, float directionDesire, float speed, float speedDesire, byte priority)
        {
            this.Direction = direction;
            this.Speed  = speed;
            this.SpeedDesire = speedDesire;
            this.DirectionDesire = directionDesire;
            this.Priority = priority;
#if STEERING_DEBUG
            this.Color = default;
#endif
        }
        
#if STEERING_DEBUG
        public void Draw(float3 position, float scale)
        {
            float3 end = position + Direction * math.max(0, DirectionDesire) * scale;
            DebugDraw.DrawArrow(position, end, Color);
            
            float fontScale = 0.05f;
            float spacing = 0.5f * fontScale;
            float3 offset = new float3(0, 0, fontScale * 2 + spacing * 2);
            
            float3 topLeft = end + new float3(0, 0.2f, 0);
            float3 currentPosition = topLeft;
            
            currentPosition = DebugDraw.DrawNumber(currentPosition, DirectionDesire, 1, 4, fontScale, spacing, Color.white * Color);
            currentPosition = DebugDraw.DrawComma(currentPosition, fontScale, spacing, Color.white * Color);
            currentPosition = DebugDraw.DrawVector(currentPosition, Direction, fontScale, spacing, Color.gray * Color);
            float endX = currentPosition.x;
            
            currentPosition = topLeft - offset;
            currentPosition = DebugDraw.DrawNumber(currentPosition, SpeedDesire, 1, 4, fontScale, spacing, Color.white * Color);
            currentPosition = DebugDraw.DrawComma(currentPosition, fontScale, spacing, Color.white * Color);
            currentPosition = DebugDraw.DrawLeftBrace(currentPosition, fontScale, spacing, Color.gray * Color);
            currentPosition = DebugDraw.DrawNumber(currentPosition, Speed, 6, 2, fontScale, spacing, Color.gray * Color);
            currentPosition = DebugDraw.DrawRightBrace(currentPosition, fontScale, spacing, Color.gray * Color);
            float3 bottomLeft = new float3(endX, currentPosition.y, currentPosition.z);
             
            DebugDraw.DrawBox(topLeft + new float3(0, 0, fontScale * 2), bottomLeft, spacing, Color.gray * Color);
        }
        
        public bool IsValid()
        {
            if(float.IsNaN(Direction.x) || float.IsNaN(Direction.y) || float.IsNaN(Direction.z))
            {
                return false;
            }
            
            if(math.abs(1 - math.length(Direction)) > 0.05f && DirectionDesire > 0)
            {
                return false;
            }
            
            return true;
        }
        
        public void ToStringFixed(out FixedString128Bytes string128)
        {
            string128 = $"Direction: {Direction}, DesiredSpeed: {Speed}, Desire: {DirectionDesire}, Priority: {Priority}";
        }
#endif
        
    }
}