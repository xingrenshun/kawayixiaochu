using System.Collections;
using FTools.Tween;
using FUnityAttribute.RuntimeAttributes;
using FUnityCommon;
using FUnityUI;
using TMPro;
using UnityEngine;

namespace View
{
    [FViewLoader("View/AlertTipView")]
    public class AlertTipView : FUiBase<AlertTipView>
    {
        [FuiAutowire]
        private TMP_Text _contentText;

        [FuiAutowire]
        private RectTransform _panel;
        private Vector2 _defaultAnchoredPosition;

        private long _coroutineId = -1;
        
        
        private FTweenPosition _tweenPosition;
        private FTweenUIOpacity _tweenOpacity;

        public override void OnSetup()
        {
            _tweenOpacity = _panel.GetComponent<FTweenUIOpacity>();
            _tweenPosition = _panel.GetComponent<FTweenPosition>();
            _defaultAnchoredPosition = _panel.anchoredPosition;
        }

        public static AlertTipView ShowTopMessage(string content, float delay = 3.5f, Vector2? anchoredPosition = null)
        {
            if (Instance != null)
            {
                FViewManager.Instance.DestroyInstance(Instance);
            }

            var view = FViewManager.Instance.Show<AlertTipView>(true, 2);
            view.FadeIn(content, delay, anchoredPosition);
            return view;
        }

        public void FadeIn(string content, float delay=3.5f, Vector2? anchoredPosition = null)
        {
            ShowView();
            transform.SetAsLastSibling();
            var targetPosition = anchoredPosition ?? _defaultAnchoredPosition;
            if (_tweenPosition != null && _tweenPosition.isUseAnchor)
            {
                _tweenPosition.SetAnchorTargetPosition(targetPosition);
            }
            else
            {
                _panel.anchoredPosition = targetPosition;
            }
            _contentText.text = content;
            if (_coroutineId != -1)
            {
                FAsync.Instance.StopRoutine(_coroutineId);
            }
            if (_tweenPosition.IsRunning) _tweenPosition.Pause();
            if (_tweenOpacity.IsRunning) _tweenOpacity.Pause();
            _tweenOpacity.Replay();
            _tweenPosition.Replay();
            _coroutineId = FAsync.Instance.StartRoutine(StartFadeIn(delay));
        }

        private IEnumerator StartFadeIn(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            _tweenOpacity.DoSmoothRewind();
            _tweenPosition.DoSmoothRewind();
        }
    }
}
