using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Splines;

namespace SteeringAI.Defaults
{
    public class FollowPathSplineManager : MonoBehaviour
    {
        public static FollowPathSplineManager Instance;
        
        [SerializeField] private List<SplineContainer> Splines;

        private readonly List<List<float>> curveSegmentLengths = new();
        
        private void Awake()
        {
            Instance = this;
            for (int i = 0; i < Splines.Count; i++)
            {
                var spline = Splines[i][0];
                var nativeSpline = new NativeSpline(spline);
                curveSegmentLengths.Add(new List<float>());
                
                for (int j = 0; j < nativeSpline.Curves.Length; j++)
                {
                    var curveLength = spline.GetCurveLength(j);
                    curveSegmentLengths[i].Add(curveLength);
                }
                
                nativeSpline.Dispose();
            }
        }

        public NativeSpline GetSpline(int index)
        {
            var spline = new NativeSpline(Splines[index][0], Splines[index].transform.localToWorldMatrix, Allocator.TempJob);
            return spline;
        }

        public NativeArray<float> GetSplineLengths(int index)
        {
            List<float> lengths = curveSegmentLengths[index];
            NativeArray<float> nativeLengths = new NativeArray<float>(lengths.Count, Allocator.TempJob);
            
            for (int i = 0; i < lengths.Count; i++)
            {
                nativeLengths[i] = lengths[i];
            }

            return nativeLengths;
        }
        
    }
}