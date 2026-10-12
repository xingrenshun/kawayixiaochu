using DG.Tweening;
using FUnityAttribute.EditorAttributes;
using UnityEngine;

namespace FTools.Tween
{
    public class FTweenScale : FTweenBase
    {
        [FLabel("目标缩放值")]
        public Vector3 targetScale = Vector3.one * 1.2f;
     
        [FHideIf("fromCurrentScale"), FLabel("起始缩放")]
        public Vector3 fromScale = Vector3.zero;

        [FLabel("是否从当前缩放播放")]
        public bool fromCurrentScale = true;
   

        protected override DG.Tweening.Tween CreateTween()
        {
            var tween = transform.DOScale(targetScale, duration);
            if (!fromCurrentScale) tween.From(fromScale);
            return tween;
        }
        
        

        
    }
}