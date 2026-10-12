using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ui
{
    /// <summary>
    /// 道具按钮UI组件，显示冷却时间和激活状态
    /// </summary>
    public class PowerUpButton : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Image cooldownOverlay;
        [SerializeField] private TMP_Text cooldownText;
        [SerializeField] private GameObject activeIndicator;
        [SerializeField] private Button button;

        private float _cooldownTime;
        private float _remainingTime;
        private bool _isActive;
        private bool _isCoolingDown;

        private void Update()
        {
            if (_isCoolingDown)
            {
                _remainingTime -= Time.deltaTime;
                if (_remainingTime <= 0)
                {
                    _remainingTime = 0;
                    _isCoolingDown = false;
                    OnCooldownComplete();
                }

                UpdateCooldownDisplay();
            }
        }

        public void SetCooldown(float cooldownTime)
        {
            _cooldownTime = cooldownTime;
            _remainingTime = cooldownTime;
            _isCoolingDown = true;

            if (button != null)
            {
                button.interactable = false;
            }

            if (cooldownOverlay != null)
            {
                cooldownOverlay.gameObject.SetActive(true);
                cooldownOverlay.fillAmount = 1f;
            }
        }

        public void SetActive(bool active)
        {
            _isActive = active;

            if (activeIndicator != null)
            {
                activeIndicator.SetActive(active);

                if (active)
                {
                    // 激活时的闪烁动画
                    var seq = DOTween.Sequence();
                    seq.Append(activeIndicator.transform.DOScale(1.2f, 0.3f).SetEase(Ease.OutQuad));
                    seq.Append(activeIndicator.transform.DOScale(1f, 0.3f).SetEase(Ease.InQuad));
                    seq.SetLoops(-1);
                }
            }

            if (iconImage != null && active)
            {
                // 图标发光效果
                iconImage.DOColor(Color.yellow, 0.5f).SetLoops(-1, LoopType.Yoyo);
            }
        }

        private void UpdateCooldownDisplay()
        {
            if (cooldownOverlay != null)
            {
                cooldownOverlay.fillAmount = _remainingTime / _cooldownTime;
            }

            if (cooldownText != null)
            {
                cooldownText.text = Mathf.CeilToInt(_remainingTime).ToString();
            }
        }

        private void OnCooldownComplete()
        {
            if (cooldownOverlay != null)
            {
                cooldownOverlay.gameObject.SetActive(false);
            }

            if (cooldownText != null)
            {
                cooldownText.gameObject.SetActive(false);
            }

            if (button != null)
            {
                button.interactable = true;

                // 可用时的弹跳动画
                transform.DOPunchScale(Vector3.one * 0.2f, 0.5f, 5, 0.5f);
            }
        }

        public void OnButtonClick()
        {
            // 点击时的缩放反馈
            transform.DOPunchScale(Vector3.one * 0.1f, 0.2f, 3, 0.5f);
        }
    }
}
