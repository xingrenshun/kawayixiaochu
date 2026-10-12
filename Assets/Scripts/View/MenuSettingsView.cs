using System.Collections;
using FTools.Cache;
using FTools.Helpers;
using FUnityAttribute.RuntimeAttributes;
using FUnityUI;
using UnityEngine;
using UnityEngine.UI;

namespace View
{
    [FViewLoader("View/MenuSettingsView")]
    public class MenuSettingsView : FUiBase<MenuSettingsView>
    {
        private Animator _rootAnimator;


        [FGameObject] private GameObject _panel;
        private CanvasGroup _group;
        
        [FuiAutowire] private Slider _audioSlider;
        [FuiAutowire] private Slider _musicSlider;

        [FuiAutowire] private Toggle _openVibrate;
        [FuiAutowire] private Toggle _closeVibrate;



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
            
        }
        
        private static void OnAudioAmountChange (float value)
        {
            FCacheManager.GetCache(ECacheType.Local).Save("AudioAmount", value);
            FAudioCore.Instance.SetSfxVolume(value);
        }
        
        private static void OnMusicAmountChange (float value)
        {
            FCacheManager.GetCache(ECacheType.Local).Save("MusicAmount", value);
            FAudioCore.Instance.SetBGMVolume(value);
        }
        private static void OnVibrateToggle(bool isOn)
        {
            FDeviceCore.VibrateEnabled = isOn;
        }


        [FOnClick("close-settings")]
        private void Close()
        {
            if (_rootAnimator.GetCurrentAnimatorStateInfo(0).IsName("Open"))
            {
                _rootAnimator.Play("Close");
            }
            StartCoroutine(RunPopupDestroy());
        }


        private IEnumerator RunPopupDestroy()
        {
            var alpha = _group.alpha;
            var elpased = 0f;
            const float duration = 0.5f;
            while (alpha > 0)
            {
                var p = elpased / duration;
                alpha = Mathf.Lerp(1, 0, p);
                _group.alpha = alpha;
                elpased += Time.deltaTime;
                yield return null;
            }
            yield return new WaitForSeconds(duration);
            FViewManager.Instance.DestroyInstance(this);
        }
    }
}