using SteeringAI.Core;
using Unity.Entities;

namespace SteeringAI.Samples
{
    [UpdateInGroup(typeof(SteeringSystemsGroup))]
    public partial class Flocking3DSystem : BaseSteeringSystem
    {
        protected override string getAssetReferenceName()
        {
            return Samples.SamplesRootPath + "6. Flocking/3. 3D/Flocking3DSystemAsset.asset";
        }
    }
}