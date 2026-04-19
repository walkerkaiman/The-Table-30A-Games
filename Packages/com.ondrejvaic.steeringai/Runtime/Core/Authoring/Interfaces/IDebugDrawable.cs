using Unity.Mathematics;
using UnityEngine;

namespace SteeringAI.Core
{
    public interface IDebugDrawable
    {
#if STEERING_DEBUG
        public void Draw(float3 position, float scale);
        public Color Color { get; set; }
#endif
    }
}