using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using FUnityAttribute.EditorAttributes;
using UnityEngine;

namespace FTools.Tween
{
    public class FTweenPosition : FTweenBase
    {

        private bool IsShowWorldSpace => useSlidingIn || isUseAnchor;

        [FLabel("使用世界坐标移动"), FHideIf("IsShowWorldSpace")]
        public bool useWorldSpace = false;

        [FLabel("使用当前位置作为起点"), FHideIf("useSlidingIn")]
        public bool useCurrentPosition = true;

        private bool IsShowFromPosition => !useCurrentPosition && !useSlidingIn;
        [FLabel("起始位置"), FShowIf("IsShowFromPosition")]
        public Vector3 fromPosition = Vector3.zero;

        [FLabel("终点位置"), FHideIf("useSlidingIn")]
        public Vector3 targetPosition = Vector3.zero;

        [FLabel("是否作为移入/移出位移")]
        public bool useSlidingIn = false;

        [FLabel("锚点移动")]
        public bool isUseAnchor = false;

        [FLabel("移入偏移"), FShowIf("useSlidingIn")]
        public Vector3 slidingInOffset = Vector3.zero;

        // [FLabel("移出偏移"), FShowIf("useSlidingIn")]
        // public Vector3 slidingOutOffset = Vector3.zero;

        private Vector3 _initialPosition;

        public void SetAnchorTargetPosition(Vector2 anchoredPosition)
        {
            targetPosition = anchoredPosition;
            if (useSlidingIn)
            {
                _initialPosition = anchoredPosition;
            }

            UpdateTween();
        }

        protected override void BeforeTweenStart()
        {
            _initialPosition = useSlidingIn ? transform.localPosition : transform.position;
        }

        protected override DG.Tweening.Tween CreateTween()
        {
            var (start, target) = CalculateStartAndTargetPosition();

            if (isUseAnchor)
            {
                var tween = (transform as RectTransform)!.DOAnchorPos(target, duration);
                tween.From(start);
                return tween;
            }
            else
            {
                TweenerCore<Vector3, Vector3, VectorOptions> tween;
                if (useWorldSpace) tween = transform.DOMove(target, duration);
                else tween = transform.DOLocalMove(target, duration);
                tween.From(start);
                return tween;
            }

        }

        private (Vector3, Vector3) CalculateStartAndTargetPosition()
        {
            var startPosition = _initialPosition;

            if (useSlidingIn)
            {
                var pos = _initialPosition;
                startPosition = pos + slidingInOffset;
            }
            else if (!useCurrentPosition)
            {
                startPosition = fromPosition;
            }

            var target = useSlidingIn ? _initialPosition : targetPosition;
            return (startPosition, target);
        }

        // public void OnSlidingOut()
        // {
        //     if (!useSlidingIn) return;
        //
        //     var posTween = (TweenerCore<Vector3, Vector3, VectorOptions>)BaseTween;
        //     var currentPos = useWorldSpace ? transform.position : transform.localPosition;
        //
        //     posTween.From(currentPos);
        //     posTween.SetTarget(currentPos + slidingOutOffset);
        //
        //     Play().OnComplete(() =>
        //     {
        //         var (start, target) = CalculateStartAndTargetPosition();
        //         posTween.From(start);
        //         posTween.SetTarget(target);
        //     });
        // }
    }
}
