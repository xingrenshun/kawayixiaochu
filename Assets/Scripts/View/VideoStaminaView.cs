using System.Collections;
using FTools.Platforms;
using FUnityAttribute.RuntimeAttributes;
using FUnityCommon;
using FUnityUI;
using UnityEngine;

namespace View
{
    [FViewLoader("View/VideoStaminaView")]
    public class VideoStaminaView : FUiBase<VideoStaminaView>
    {
          
        private Animator _rootAnimator;
        [FGameObject] private GameObject _panel;
        private CanvasGroup _group;
        public override void OnSetup()
        {
            _group = GetComponent<CanvasGroup>();
            _rootAnimator = _panel.GetComponent<Animator>();
        }
        
        
        
        
        [FOnClick("close-share-modal", Once = true)]
        private void Close()
        {
            if (_rootAnimator.GetCurrentAnimatorStateInfo(0).IsName("Open"))
            {
                _rootAnimator.Play("Close");
            }
            StartCoroutine(RunPopupDestroy());
        }


        [FOnClick("Button-Share", Once = true)]
        private void OnVideoClick()
        {
            FAds.Instance.Video(res =>
            {
                if (!res) return;
                Close();
                if (HomeView.Instance == null)
                {
                    PlayerLocalData.Instance.PlayerStamina += 10;
                }
                else
                {
                    FAsync.Instance.InvokeDelay(() =>
                    {
                        HomeView.Instance.AwareStamina(10);
                        HomeView.Instance.chestGiftOpeningButton.gameObject.SetActive(false);
                    }, 0.35f);
                }
            });
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