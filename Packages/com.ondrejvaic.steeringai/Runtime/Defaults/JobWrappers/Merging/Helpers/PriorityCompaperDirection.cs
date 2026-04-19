using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;

namespace SteeringAI.Defaults
{
    [BurstCompile]
    struct PriorityComparerDirection : IComparer<VelocityResult>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(VelocityResult x, VelocityResult y)
        {
            int priorityDiff = y.Priority - x.Priority;
            if(priorityDiff != 0) return priorityDiff;
            
            return y.DirectionDesire.CompareTo(x.DirectionDesire);
        }
    }
}