using SteeringAI.Core;
using Unity.Entities;


namespace SteeringAI.Samples
{
    [UpdateInGroup(typeof(SteeringSystemsGroup))]
    public partial class SampleSteeringSystem : BaseSteeringSystem
    {
        protected override string getAssetReferenceName()
        {
            return Samples.SamplesRootPath + "1. Minimal Set Up/SampleSteeringSystemAsset.asset";
        }
    }
}