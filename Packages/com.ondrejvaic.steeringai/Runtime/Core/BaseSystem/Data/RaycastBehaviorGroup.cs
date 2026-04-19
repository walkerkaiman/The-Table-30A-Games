
namespace SteeringAI.Core
{
    public struct RaycastBehaviorGroup
    {
        public IRaycastBehaviorJobWrapper[] BehaviorJobRunners;
        public ICreateRaysJobWrapper CreateRaysJobWrapper;
        public RaycastSettings RaySettings;
    }
}