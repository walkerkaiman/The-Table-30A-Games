using SteeringAI.Core;
using Unity.Entities;

namespace SteeringAI.Samples
{
    [UpdateInGroup(typeof(SteeringSystemsGroup))]
    public partial class BirdPredatorSteeringSystem : BaseSteeringSystem
    {
        protected override string getAssetReferenceName()
        {
            return Samples.SamplesRootPath + "9. Full Example/SteeringSystems/BirdPredatorSteeringSystemAsset.asset";
        }
    }
}