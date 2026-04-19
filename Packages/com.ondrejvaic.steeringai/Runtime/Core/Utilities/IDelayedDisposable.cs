using Unity.Jobs;

namespace SteeringAI.Core
{
    public interface IDelayedDisposable
    {
        public void Dispose(JobHandle dependency);
    }
}