using System;

namespace SteeringAI.Defaults
{
    [Serializable]
    public struct RotationOptions25D
    {
        public float SwingRotationSpeed;
        public float RotationSpeed;
        public bool Rotate;
        
        public static RotationOptions25D Preset => new()
        {
            SwingRotationSpeed = 3,
            RotationSpeed = 5,
            Rotate = true
        };
    }
}