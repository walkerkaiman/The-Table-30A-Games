namespace SteeringAI.Core
{
    public interface IValidable
    {
#if STEERING_DEBUG
        public bool IsValid();
#endif
    }
}