using SteeringAI.Core;
using Unity.Entities;

namespace SteeringAI.Samples
{
    [UpdateInGroup(typeof(SteeringSystemsGroup))]
    public partial class Movement25DSteeringSystem : BaseSteeringSystem
    {
        protected override string getAssetReferenceName()
        {
            return Samples.SamplesRootPath + "5. 2.5D Movement/Movement25DSystemAsset.asset";
        }
    }
}