using System;

namespace SteeringAI.Defaults
{
    [Serializable]
    public struct RotationOptions3D
    {
        public float RotationSpeed;
        public float RollRotationSpeed;
        public bool Rotate;

        public static RotationOptions3D Preset => new()
        {
            RotationSpeed = 150,
            RollRotationSpeed = 150,
            Rotate = true
        };
    }
}