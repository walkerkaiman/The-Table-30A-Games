using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;

namespace SteeringAI.Defaults
{
    [BurstCompile]
    struct PriorityComparerSpeed : IComparer<VelocityResult>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(VelocityResult x, VelocityResult y)
        {
            int priorityDiff = y.Priority - x.Priority;
            if(priorityDiff != 0) return priorityDiff;
            
            return y.SpeedDesire.CompareTo(x.SpeedDesire);
        }
    }
}