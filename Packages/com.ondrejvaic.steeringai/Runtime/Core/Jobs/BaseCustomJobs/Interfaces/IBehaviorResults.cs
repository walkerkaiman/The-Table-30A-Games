using Unity.Collections;

namespace SteeringAI.Core
{
    public interface IBehaviorResults<ResultT> : IDelayedDisposable where ResultT : struct
#if STEERING_DEBUG
        ,IDebugDrawable, IValidable, IToStringFixed
#endif
    {
        public NativeArray<ResultT> Results { get; }
    }
}