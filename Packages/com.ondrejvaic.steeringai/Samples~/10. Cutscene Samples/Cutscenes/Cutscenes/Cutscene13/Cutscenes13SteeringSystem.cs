using SteeringAI.Core;
using Unity.Entities;

namespace SteeringAI.Samples.Cutscenes
{
    [UpdateInGroup(typeof(SteeringSystemsGroup))]
    public partial class Cutscenes13SteeringSystem : BaseSteeringSystem
    {
        protected override string getAssetReferenceName()
        {
            return Samples.SamplesRootPath + "10. Cutscene Samples/Cutscenes/Cutscenes/Cutscene13/Cutscenes13SteeringSystemAsset.asset";
        }
    }
}