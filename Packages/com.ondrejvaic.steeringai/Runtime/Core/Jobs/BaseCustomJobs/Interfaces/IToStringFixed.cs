using Unity.Collections;

namespace SteeringAI.Core
{
    public interface IToStringFixed
    {
        public void ToStringFixed(out FixedString128Bytes string128);
    }
}