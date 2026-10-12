using DG.Tweening;
using FUnityAttribute.EditorAttributes;
using UnityEngine;

namespace FTools.Tween
{
    public class FTweenRotation : FTweenBase
    {
        [FLabel("使用相对旋转")]
        public bool useRelativeRotation = true;

        [FLabel("使用当前旋转作为起点")]
        public bool useCurrentRotation = true;

        [FLabel("起始旋转"), FHideIf("useCurrentRotation")]
        public Vector3 fromRotation = Vector3.zero;

        [FLabel("目标旋转")]
        public Vector3 targetRotation = Vector3.zero;

        [FLabel("使用世界坐标系旋转")]
        public bool useWorldRotation = false;



        private Quaternion _initWorldRotation;
        private Quaternion _initLocalRotation;

        protected override DG.Tweening.Tween CreateTween()
        {
            
            _initLocalRotation = transform.localRotation;
            _initWorldRotation = transform.rotation;
            var startRot = useCurrentRotation 
                ? (useWorldRotation ? transform.eulerAngles : transform.localEulerAngles) 
                : fromRotation;

            // if (useWorldRotation)
            //     transform.eulerAngles = startRot;
            // else
            //     transform.localEulerAngles = startRot;

            var endRot = useRelativeRotation 
                ? startRot + targetRotation 
                : targetRotation;


            var t = useWorldRotation
                ? transform.DORotate(endRot, duration, RotateMode.FastBeyond360)
                : transform.DOLocalRotate(endRot, duration, RotateMode.FastBeyond360);
            t.From(startRot);
            
            if (useWorldRotation) transform.rotation = _initWorldRotation;
            else transform.localRotation = _initLocalRotation;
            // t.OnStart(() =>
            // {
            //     if (useWorldRotation)
            //         transform.eulerAngles = startRot;
            //     else
            //         transform.localEulerAngles = startRot;
            // });


            return t;
        }

 
    }
}