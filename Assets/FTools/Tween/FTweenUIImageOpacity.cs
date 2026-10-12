using DG.Tweening;
using FUnityAttribute.EditorAttributes;
using UnityEngine;
using UnityEngine.UI;

namespace FTools.Tween
{
    [RequireComponent(typeof(RectTransform), typeof(Image))]
    public class FTweenUIImageOpacity : FTweenBase
    {
        [FLabel("目标Alpha")] public float targetAlpha = 1f;

        [FLabel("是否从当前 Alpha 开始渐变")]
        public bool fromCurrentOpacity = true;

        [FHideIf("fromCurrentOpacity"), FLabel("起始Alpha")]
        public float fromAlpha = 0f;


        private Image _image;

        protected override void BeforeTweenStart()
        {
            _image = GetComponent<Image>();
        }

        protected override DG.Tweening.Tween CreateTween()
        {
            var startAlpha = fromCurrentOpacity ? _image.color.a : fromAlpha;
            var endAlpha = targetAlpha;
            return DOTween.To(() => startAlpha, a =>
            {
                var c = _image.color;
                c.a = a;
                _image.color = c;
            }, endAlpha, duration);
        }
    }
}