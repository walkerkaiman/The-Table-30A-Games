using SteeringAI.Core;
using Unity.Entities;

namespace SteeringAI.Samples.Cutscenes
{
    [UpdateInGroup(typeof(SteeringSystemsGroup))]
    public partial class CutscenesSheepSteeringSystem : BaseSteeringSystem
    {
        protected override string getAssetReferenceName()
        {
            return Samples.SamplesRootPath + "10. Cutscene Samples/Cutscenes/Shared/Systems/CutscenesSheepSteeringSystemAsset.asset";
        }
    }
}