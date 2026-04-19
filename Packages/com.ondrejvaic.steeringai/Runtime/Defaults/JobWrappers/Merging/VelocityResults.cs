using SteeringAI.Core;
using Unity.Collections;
using Unity.Jobs;

namespace SteeringAI.Defaults
{
    public struct VelocityResults : IBehaviorResults<VelocityResult>
    {
        public NativeArray<VelocityResult> Results => results;
        private NativeArray<VelocityResult> results; 
        
        public VelocityResults(int entityCount)
        {
            results = new NativeArray<VelocityResult>(entityCount, Allocator.TempJob);
        }
        
        public void Dispose(JobHandle dependency)
        {
            results.Dispose(dependency); 
        }
    }
}