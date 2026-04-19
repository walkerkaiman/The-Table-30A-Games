using UnityEngine;

namespace Cutscenes
{
    public class CutsceneHomeAnimationData : MonoBehaviour
    {
        public float AnimationTime;
        public float MinRadiusScale;
        public float MaxRadiusScale;
        public AnimationCurve MinRadiusCurve;
        public AnimationCurve MaxRadiusCurve;
        public static CutsceneHomeAnimationData Instance;
        
        public void Awake()
        {
            Instance = this;
        }
    }
}