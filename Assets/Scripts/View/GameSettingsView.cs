using System;
using System.Collections;
using Effect;
using FTools.Cache;
using FTools.Helpers;
using FUnityAttribute.RuntimeAttributes;
using FUnityCommon;
using FUnityUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace View
{
    [FViewLoader("View/GameSettingsView")]
    public class GameSettingsView : FUiBase<GameSettingsView>
    {
        private Animator _rootAnimator;


        [FGameObject] private GameObject _panel;

        [FuiAutowire] private Slider _audioSlider;
        [FuiAutowire] private Slider _musicSlider;

        [FuiAutowire] private Toggle _openVibrate;
        [FuiAutowire] private Toggle _closeVibrate;

        [FuiAutowire] private TextMeshProUGUI _staminaTimeText;
        [FuiAutowire] private TextMeshProUGUI _staminaValueText;
        [FuiAutowire] private Image _staminaIcon;
        [FuiAutowire] private RectTransform _restartGame;
        private bool _isTapedStart;

        private Action<int> _onStaminaChanged;
        private Action<TimeSpan> _onRecoveryChanged;

        private CanvasGroup _group;

        public override void OnSetup()
        {
            _group = GetComponent<CanvasGroup>();
            _rootAnimator = _panel.GetComponent<Animator>();

            var isVibrateOpen = FCacheManager.GetCache(ECacheType.Local).Load("IsOpenVibrate", true);
            if (isVibrateOpen) _openVibrate.SetIsOnWithoutNotify(true);
            else _closeVibrate.SetIsOnWithoutNotify(true);

            var audioAmount = FCacheManager.GetCache(ECacheType.Local).Load("AudioAmount", 1f);
            var musicAmount = FCacheManager.GetCache(ECacheType.Local).Load("MusicAmount", 1f);

            _audioSlider.SetValueWithoutNotify(audioAmount);
            _musicSlider.SetValueWithoutNotify(musicAmount);

            _audioSlider.onValueChanged.AddListener(OnAudioAmountChange);
            _musicSlider.onValueChanged.AddListener(OnMusicAmountChange);
            _openVibrate.onValueChanged.AddListener(OnVibrateToggle);

            var data = PlayerLocalData.Instance;
            _staminaValueText.text = data.PlayerStamina.ToString();
            _onStaminaChanged = stamina => { _staminaValueText.text = stamina.ToString(); };

            _onRecoveryChanged = time =>
            {
                _staminaTimeText.text = time == TimeSpan.Zero
                    ? "已满"
                    : $"{time.Minutes:D2}:{time.Seconds:D2}";
            };

            data.OnStaminaChanged += _onStaminaChanged;
            data.OnNextRecoveryTimeChanged += _onRecoveryChanged;
            data.ImmediateTriggerCallback();
        }

        private void OnDisable()
        {
            var data = PlayerLocalData.Instance;
            data.OnStaminaChanged -= _onStaminaChanged;
            data.OnNextRecoveryTimeChanged -= _onRecoveryChanged;
        }

        public static Action callback;

        [FOnClick("restart-game")]
        private void RestartGame()
        {
            FAudioCore.Instance.PlaySfx("tap");
            // AutoLayoutGameView.Instance.RestartGame();
            // Close();
            CheckStamina(_restartGame.transform, StaminaCheck, () =>
            {
                // AutoLayoutGameView.Instance.RestartGame();
                callback?.Invoke();
                callback = null;
                PlayerLocalData.Instance.PlayerStamina--;
                Close();
            });
        }
        
        [FOnClick("PopStamina")]
        private void OpenStamina()
        {
            VideoStaminaView.ShowView();
        }
        private bool StaminaCheck()
        {
            var staminaIsEnough = PlayerLocalData.Instance.PlayerStamina > 0;
            if (!staminaIsEnough)
            {
                AlertTipView.Instance.FadeIn("没有体力啦~");
            }
            return staminaIsEnough;
        }
        private void OnAudioAmountChange(float value)
        {
            FCacheManager.GetCache(ECacheType.Local).Save("AudioAmount", value);
            FAudioCore.Instance.SetSfxVolume(value);
        }

        private void OnMusicAmountChange(float value)
        {
            FCacheManager.GetCache(ECacheType.Local).Save("MusicAmount", value);
            FAudioCore.Instance.SetBGMVolume(value);
        }

        private void OnVibrateToggle(bool isOn)
        {
            FAudioCore.Instance.PlaySfx("tap");
            FCacheManager.GetCache(ECacheType.Local).Save("IsOpenVibrate", isOn);
        }


        [FOnClick("close-settings")]
        private void Close()
        {
            FAudioCore.Instance.PlaySfx("tap");
            if (_rootAnimator.GetCurrentAnimatorStateInfo(0).IsName("Open"))
            {
                _rootAnimator.Play("Close");
            }

            StartCoroutine(RunPopupDestroy());
        }

        [FOnClick("Button-Home")]
        private void Home()
        {
            FAudioCore.Instance.PlaySfx("tap");
            if (_rootAnimator.GetCurrentAnimatorStateInfo(0).IsName("Open"))
            {
                _rootAnimator.Play("Close");
            }

            StartCoroutine(RunPopupDestroy());
            FViewManager.Instance.DestroyView<AutoLayoutGameView>();
            FViewManager.Instance.DestroyView<MemoryGameView>();
            FViewManager.Instance.Show<HomeView>();
        }

        private IEnumerator RunPopupDestroy(Action callback = null)
        {
            var alpha = _group.alpha;
            var elpased = 0f;
            const float duration = 0.35f;
            while (alpha > 0)
            {
                var p = elpased / duration;
                alpha = Mathf.Lerp(1, 0, p);
                _group.alpha = alpha;
                elpased += Time.deltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(duration);
            callback?.Invoke();
            FViewManager.Instance.DestroyInstance(this);
        }
        
        private void CheckStamina(Transform target, Func<bool> preCheck, Action callback)
        {
            if (!preCheck() || _isTapedStart) return;
            _isTapedStart = true;
            FAsync.Instance.StartRoutine(IconMoveVfx.StartIconCircleMove(_staminaIcon.gameObject, target,
                10, null, callback));
        }
    }
}
