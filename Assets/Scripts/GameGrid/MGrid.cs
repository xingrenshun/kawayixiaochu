using System;
using DG.Tweening;
using FTools.Helpers;
using FUnityCommon;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using View;
using Object = UnityEngine.Object;

namespace GameGrid
{
    /// <summary>
    /// 记忆模式的格子类
    /// </summary>
    public class MGrid
    {
        public int x, y, id;
        private readonly RectTransform _rt;
        private readonly GameObject _uObj;
        public GameObject UnityObj => _uObj;
        private readonly GameObject _prefab;
        private GameObject _questionSign;
        private Image _icon;
        private Sprite _frontSprite;
        public MGrid(int x, int y, int id, GameObject prefab, Transform renderRoot)
        {
            this.x = x;
            this.y = y;
            this.id = id;
            _prefab = prefab;
            
            _uObj = Object.Instantiate(_prefab, renderRoot);
            _uObj.GetComponent<Button>().onClick.AddListener(OnTap);
            // _uObj.SetActive(true);
            _rt = _uObj.GetComponent<RectTransform>();
            // _rt.anchoredPosition = new Vector2(MemoryGameView.Instance.GetXPosition(x), MemoryGameView.Instance.GetYPosition(y));

    
            _icon = _uObj.transform.GetChild(0).GetComponent<Image>();

            var spawnerTransform = MemoryGameView.Instance.spawnerTransform;
            // 世界坐标
            _rt.localPosition = FMath.TransLocalPositionToTarget(_rt, spawnerTransform);
            
            _questionSign = _uObj.transform.GetChild(2).gameObject;
        }
        private bool _isFlip;
        private bool _isDestroyed;
        private bool _isLockedOpened;

        public bool IsFlipped()
        {
            return _isFlip;
        }

        public bool IsLockedOpened()
        {
            return _isLockedOpened;
        }

        public void SetLockedOpened(bool lockedOpened)
        {
            _isLockedOpened = lockedOpened;
        }

        public bool IsAlive()
        {
            return !_isDestroyed && _uObj != null && _rt != null && _icon != null;
        }

        public RectTransform GetComponent<T>() where T : Component
        {
            if (typeof(T) == typeof(RectTransform))
            {
                return _rt != null ? _rt : null;
            }
            return null;
        }

        public void FlipCard(Sprite back, Action onComplete=null)
        {
            if (!IsAlive())
            {
                onComplete?.Invoke();
                return;
            }

            _isFlip = !_isFlip;
            if (_questionSign != null)
            {
                _questionSign.SetActive(_isFlip);
            }
            var rt = _icon.rectTransform;

            var seq = DOTween.Sequence();
            var callbackInvoked = false;
            void SafeComplete()
            {
                if (callbackInvoked) return;
                callbackInvoked = true;
                onComplete?.Invoke();
            }

            seq.SetLink(_uObj);

            // 第一段：缩到 0，增加弹性效果
            seq.Append(rt.DOScaleX(0f, 0.15f).SetEase(Ease.InBack));

            // 中间切换图片
            seq.AppendCallback(() =>
            {
                if (!IsAlive()) return;
                _icon.sprite = _icon.sprite == _frontSprite ? back : _frontSprite;
            });
            // 第二段：恢复到 1，增加弹性效果
            seq.Append(rt.DOScaleX(1f, 0.15f).SetEase(Ease.OutBack));

            // 添加轻微的Y轴缩放动画，增加立体感
            seq.Join(rt.DOScaleY(1.05f, 0.1f).SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    if (!IsAlive()) return;
                    rt.DOScaleY(1f, 0.05f);
                }));

            seq.OnComplete(() =>
            {
                SafeComplete();
            });
            seq.OnKill(SafeComplete);
        }

        public void Destroy()
        {
            if (_isDestroyed) return;
            if (_uObj == null || _rt == null)
            {
                _isDestroyed = true;
                return;
            }

            _isDestroyed = true;
            DOTween.Kill(_rt);
            DOTween.Kill(_uObj);

            // 添加消失动画
            var seq = DOTween.Sequence();
            seq.SetLink(_uObj);
            seq.Append(_rt.DOScale(1.2f, 0.15f).SetEase(Ease.OutBack));
            seq.Append(_rt.DOScale(0f, 0.2f).SetEase(Ease.InBack));
            seq.Join(_uObj.GetComponent<Image>().DOFade(0f, 0.2f));
            seq.OnComplete(() =>
            {
                if (_uObj != null)
                {
                    Object.Destroy(_uObj);
                }
            });
        }
        public void OnTap()
        {
            var view = MemoryGameView.Instance;
            FAudioCore.Instance.PlaySfx("tap");
            if (view == null || !IsAlive()) return;
            if (_isLockedOpened)
            {
                if (!view.TryBeginTap(this)) return;
                view.DoSelected(this);
                return;
            }
            if (!view.TryBeginTap(this)) return;
            FlipCard(view.cardBack, () =>
            {
                view.DoSelected(this);
            });
        }
        public void ToTargetPos()
        {
            if (!IsAlive()) return;
            var spawnerTransform = MemoryGameView.Instance.spawnerTransform;
            // 世界坐标
            _rt.localPosition = FMath.TransLocalPositionToTarget(_rt, spawnerTransform);
            var targetPos = new Vector2(MemoryGameView.Instance.GetXPosition(x),
                MemoryGameView.Instance.GetYPosition(y));
            _uObj.SetActive(true);
            var startPos = _rt.anchoredPosition;

            // 初始缩放为0，准备弹出动画
            _rt.localScale = Vector3.zero;

            // 控制点计算
            var ab = startPos - targetPos;
            var mid = (startPos + targetPos) * 0.35f;
            var dir = (Vector2)Vector3.Cross(ab, Vector3.forward).normalized;
            var control = mid + dir * 300;

            var seq = DOTween.Sequence();
            const float popDuration = 0.18f;
            const float moveDuration = 0.28f;
            const float bounceDuration = 0.06f;

            // 先弹出缩放动画
            seq.Append(_rt.DOScale(1f, popDuration).SetEase(Ease.OutBack));

            // 然后贝塞尔曲线移动
            seq.Append(DOVirtual.Float(0, 1, moveDuration, t =>
            {
                var pos = FMath.Bezier(startPos, control, targetPos, t);
                _rt.anchoredPosition = pos;
            }));

            // 到达目标位置后轻微弹跳
            seq.Append(_rt.DOScale(1.1f, bounceDuration).SetEase(Ease.OutQuad));
            seq.Append(_rt.DOScale(1f, bounceDuration).SetEase(Ease.InQuad));
        }
        public void Render(Sprite icon)
        {
            if (!IsAlive()) return;
            _icon.sprite = icon;
            _frontSprite = icon;
        }
    }
}
