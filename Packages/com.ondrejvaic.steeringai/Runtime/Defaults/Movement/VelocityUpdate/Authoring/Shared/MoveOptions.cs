using System;

namespace SteeringAI.Defaults
{
    [Serializable]
    public struct MovementOptions
    { 
        public float MaxSpeed;
        public float MaxForwardAcceleration;
        public float MaxRightAcceleration;
        public float Friction; 
        public bool Move;
        
        public static MovementOptions Preset => new()
        {
            Friction = 0.05f,
            MaxSpeed = 2,
            MaxForwardAcceleration = 3,
            MaxRightAcceleration = 3,
            Move = true
        };
    };
}