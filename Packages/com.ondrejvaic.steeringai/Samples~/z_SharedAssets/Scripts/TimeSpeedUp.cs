using UnityEngine;

namespace SteeringAI.Samples.Cutscenes
{
    public class TimeSpeedUp : MonoBehaviour
    {
        public float TimeScale = 1.0f;

        void Update()
        {
            Time.timeScale = TimeScale;
        }
    }
}
