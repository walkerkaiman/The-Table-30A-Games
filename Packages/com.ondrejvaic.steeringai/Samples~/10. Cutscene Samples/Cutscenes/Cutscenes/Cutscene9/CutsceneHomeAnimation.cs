using SteeringAI.Core;
using SteeringAI.Defaults;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace Cutscenes
{
    [UpdateAfter(typeof(AfterSteeringSystemsGroup))]
    public partial class CutsceneHomeAnimation : SystemBase
    {
	    
        protected override void OnCreate() { }

        protected override void OnUpdate()
        {
            float time = (float)SystemAPI.Time.ElapsedTime;

            var data = CutsceneHomeAnimationData.Instance;
            if (data == null)
            {
                return;
            }
            
            float minRadius = data.MinRadiusCurve.Evaluate(time / data.AnimationTime) * data.MinRadiusScale;
            float maxRadius = data.MaxRadiusCurve.Evaluate(time / data.AnimationTime) * data.MaxRadiusScale;
            
            foreach (var (home, transform) in SystemAPI.Query<RefRW<HomeComponent>, RefRO<LocalTransform>>().WithAll<__Cutscenes_HomeTagComponent>())
            {
                home.ValueRW.MinRadius = minRadius;
                home.ValueRW.MaxRadius = maxRadius;
                
#if STEERING_DEBUG
                DebugDraw.DrawSphere(transform.ValueRO.Position, minRadius, Color.green, 200);
                DebugDraw.DrawSphere(transform.ValueRO.Position, maxRadius, Color.blue, 200);
#endif
            }
        }

        protected override void OnDestroy() { }
    }
}