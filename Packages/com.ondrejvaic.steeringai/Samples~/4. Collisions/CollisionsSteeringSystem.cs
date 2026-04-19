using SteeringAI.Core;
using Unity.Entities;

namespace SteeringAI.Samples
{
    [UpdateInGroup(typeof(SteeringSystemsGroup))]
    public partial class CollisionsSteeringSystem : BaseSteeringSystem
    {
        protected override string getAssetReferenceName()
        {
            return Samples.SamplesRootPath + "4. Collisions/CollisionsSteeringSystemAsset.asset";
        }
    }
}