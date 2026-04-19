using System;

namespace SteeringAI.Defaults
{
    [Serializable]
    public struct RotationOptions2D
    {
        public float RotationSpeed;
        public bool Rotate;
        
        public static RotationOptions2D Preset => new()
        {
            RotationSpeed = 100,
            Rotate = true
        };
    }
}