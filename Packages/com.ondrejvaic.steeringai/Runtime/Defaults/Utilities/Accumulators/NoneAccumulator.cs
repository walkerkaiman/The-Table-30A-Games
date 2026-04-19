using System.Runtime.CompilerServices;
using SteeringAI.Core;

namespace SteeringAI.Defaults
{
    public struct NoneAccumulator : IAccumulator
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Init() { }
    }
}