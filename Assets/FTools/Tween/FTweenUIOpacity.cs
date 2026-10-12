using System;
using DG.Tweening;
using FUnityAttribute.EditorAttributes;
using UnityEngine;

namespace FTools.Tween
{
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public class FTweenUIOpacity : FTweenBase
    {
        [FLabel("目标Alpha")] public float targetAlpha = 1f;

        [FLabel("是否从当前 Alpha 开始渐变")]
        public bool fromCurrentOpacity = true;

        [FHideIf("fromCurrentOpacity"), FLabel("起始Alpha")]
        public float fromAlpha = 0f;


        private CanvasGroup _canvasGroup;

        protected override void BeforeTweenStart()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        protected override DG.Tweening.Tween CreateTween()
        {
            var startAlpha = fromCurrentOpacity ? _canvasGroup.alpha : fromAlpha;
            var endAlpha = targetAlpha;
            return DOTween.To(() => startAlpha, a => _canvasGroup.alpha = a, endAlpha, duration);
        }
    }
}