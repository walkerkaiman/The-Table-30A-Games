using SteeringAI.Core;
using Unity.Entities;

namespace SteeringAI.Samples.Cutscenes
{
    [UpdateInGroup(typeof(SteeringSystemsGroup))]
    public partial class Cutscene11SteeringSystem : BaseSteeringSystem
    {
        protected override string getAssetReferenceName()
        {
            return Samples.SamplesRootPath + "10. Cutscene Samples/Cutscenes/Cutscenes/Cutscene11/Cutscene11SystemAsset.asset";
        }
    }
}