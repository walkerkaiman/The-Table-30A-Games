using SteeringAI.Core;
using Unity.Entities;


namespace SteeringAI.Samples.Cutscenes
{
    [UpdateInGroup(typeof(SteeringSystemsGroup))]
    public partial class CutscenesBirdsSteeringSystem : BaseSteeringSystem
    {
        protected override string getAssetReferenceName()
        {
            return Samples.SamplesRootPath + "10. Cutscene Samples/Cutscenes/Shared/Systems/CutscenesBirdSteeringSystemAsset.asset";
        }
    }
}