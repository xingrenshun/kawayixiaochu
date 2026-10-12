using DG.Tweening;
using FUnityAttribute.EditorAttributes;
using UnityEngine;

namespace FTools.Tween
{
    public abstract class FTweenBase : MonoBehaviour
    {
        [FHideIf("useCustomCurve"), FLabel("DOTween 内置曲线")]
        public Ease tweenCurve;

        [FShowIf("useCustomCurve"), FLabel("自定义动画曲线")]
        public AnimationCurve customCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [FLabel("是否使用自定义动画曲线")] public bool useCustomCurve;
        [FLabel("持续时间")] public float duration = 0.45f;
        [FLabel("是否循环")] public bool loop;
        [FLabel("循环方式"), FShowIf("loop")] public LoopType loopType = LoopType.Yoyo;
        [FLabel("循环次数(-1为无限循环)"), FShowIf("loop")] public int loopCount = -1;
        [FLabel("加载后立即播放")] public bool playOnAwake = false;
        [FLabel("延迟时间")] public float delay = 0f;
        [FLabel("完成后销毁")] public bool autoDestroy = false;
        


        private DG.Tweening.Tween _tween;
        public bool IsRunning => _tween.IsPlaying();
        protected DG.Tweening.Tween BaseTween => _tween;

        protected abstract DG.Tweening.Tween CreateTween();



        public void UpdateTween()
        {
            if (_tween != null && _tween.IsActive())
            {
                _tween.Kill();
            }

            _tween = CreateTweenCore();
            _tween.SetLink(gameObject);
            if (loop) _tween.SetLoops(loopCount, loopType);
            _tween.SetAutoKill(autoDestroy);
            _tween.Pause();
        }
        private void Awake()
        {
            BeforeTweenStart();
            OnMonoAwake();
        }

        private DG.Tweening.Tween CreateTweenCore()
        {
            var t = CreateTween()
                .SetDelay(delay)
                .SetUpdate(UpdateType.Late);
            if (loop) t.SetLoops(loopCount, loopType);

            return useCustomCurve ? t.SetEase(customCurve) : t.SetEase(tweenCurve);
        }

        protected virtual void OnComplete()
        {
            if (autoDestroy) Destroy(this);
        }

        
        protected virtual void BeforeTweenStart(){}
        protected virtual void OnMonoAwake()
        {
            _tween = CreateTweenCore();
            _tween.SetLink(gameObject);
            if (loop) _tween.SetLoops(loopCount, loopType);
            _tween.SetAutoKill(autoDestroy);
            if (playOnAwake) Play();
            else _tween.Pause();
        }

        // 播放
        public virtual DG.Tweening.Tween Play()
        {
            return _tween?.Play().OnComplete(OnComplete);
        }

        // 重播（从当前点继续播放）
        public virtual void Replay()
        {
            _tween.Complete();
            _tween?.Restart();
        }

        // 暂停
        public virtual void Pause()
        {
            _tween?.Pause();
        }

        // 从头开始播放
        public virtual void Restart()
        {
            _tween?.Restart();
        }

        // 从当前位置往回播放
        public virtual void PlayBackwards()
        {
            _tween?.PlayBackwards();
        }

        // 回到起点（瞬间）
        public virtual void Rewind()
        {
            _tween?.Rewind();
        }

        public virtual void DoSmoothRewind()
        {
            _tween?.SmoothRewind();
        }

        
        public DG.Tweening.Tween GetOrCreateTween()
        {
            if (_tween == null || !_tween.IsActive())
                _tween = CreateTweenCore();
            return _tween;
        }
    }
}
