using SteeringAI.Core;
using Unity.Entities;

namespace SteeringAI.Samples
{
    [UpdateInGroup(typeof(SteeringSystemsGroup))]
    public partial class Movement3DSteeringSystem : BaseSteeringSystem
    {
        protected override string getAssetReferenceName()
        {
            return Samples.SamplesRootPath + "3. 3D Movement/Movement3DSystemAsset.asset";
        }
    }
}