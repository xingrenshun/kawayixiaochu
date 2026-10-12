using System;
using System.Collections;
using System.Collections.Generic;
using FTools.Network;
using FUnityAttribute.RuntimeAttributes;
using FUnityUI;
using UnityEngine;
using FTools.Cache;
using UnityEngine.UI;
#if PT_dy && !UNITY_EDITOR
using TTSDK;
#endif
namespace View
{
    [FViewLoader("View/WxProfileAuthView")]
    public class WxProfileAuthView : FUiBase<WxProfileAuthView>
    {
        /// <summary>
        /// 授权面板总开关。
        /// false = 不弹授权面板（首页头像按钮、排行榜个人条目、普通结算页、记忆结算页四处调用点全部跳过）。
        /// 后期需要恢复授权流程时，把它改回 true 即可，无需改动其它代码。
        /// </summary>
        public static bool EnableAuthPanel = false;

        [FuiAutowire]
        private RectTransform _panel;
        [FuiAutowire("Buttons")]
        private RectTransform _buttonsRect;

        [FuiAutowire]
        private RectTransform _authBtn;



        private Animator _rootAnimator;
        private CanvasGroup _group;
        private Button _authButton;

        public override void OnSetup()
        {
            _rootAnimator = _panel != null ? _panel.GetComponent<Animator>() : GetComponent<Animator>();
            _group = GetComponent<CanvasGroup>();
            if (_buttonsRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_buttonsRect);
            }
            SetupAuthButton();
        }

        /// <summary>
        /// 抖音端没有微信那种原生「用户信息按钮」（WXBase.CreateUserInfoButton），
        /// 这里在原来授权按钮的位置挂一个普通按钮，点击后调用 TT.GetUserInfo 拉起抖音授权。
        /// </summary>
        private void SetupAuthButton()
        {
            if (_authBtn == null) return;

            _authButton = _authBtn.GetComponent<Button>();
            if (_authButton == null)
            {
                _authButton = _authBtn.gameObject.AddComponent<Button>();
            }
            if (_authButton.targetGraphic == null)
            {
                _authButton.targetGraphic = _authBtn.GetComponent<Graphic>();
            }

            _authButton.onClick.RemoveListener(OnAuthButtonClicked);
            _authButton.onClick.AddListener(OnAuthButtonClicked);
        }

        private void OnAuthButtonClicked()
        {
#if PT_dy && !UNITY_EDITOR
            try
            {
                TT.GetUserInfo((ref TTUserInfo userInfo) =>
                    {
                        var localCache = FCacheManager.GetCache(ECacheType.Local);
                        // 更新用户信息到服务器
                        FRequest.Instance.Post<object>("update/user", new Dictionary<string, object>()
                        {
                            {"openId", localCache.Load<string>("openId")},
                            {"avatarUrl", userInfo.avatarUrl},
                            {"nickname", userInfo.nickName}
                        });
                        localCache.Save("IsAuthed", true);
                        localCache.Save("nickname", userInfo.nickName);
                        localCache.Save("avatarUrl", userInfo.avatarUrl);

                        OnClose();
                    },
                    errMsg =>
                    {
                        Debug.LogWarning("[WxProfileAuthView] 获取用户信息失败: " + errMsg);
                    });
                return;
            }
            catch (Exception e)
            {
                Debug.LogError("[WxProfileAuthView] 获取用户信息异常: " + e.Message);
            }
#endif
            // 非抖音环境或异常时直接关掉弹窗，避免卡住
            OnClose();
        }

        [FOnClick(new[] { "close-auth-panel" })]
        private void OnClose()
        {
            if (_rootAnimator != null && _rootAnimator.GetCurrentAnimatorStateInfo(0).IsName("Open"))
            {
                _rootAnimator.Play("Close");
            }
            StartCoroutine(RunPopupDestroy(DestroyView));
        }
        [FOnClick(new[] { "no-tip" })]
        private void OnCloseNoTip()
        {
            if (_rootAnimator != null && _rootAnimator.GetCurrentAnimatorStateInfo(0).IsName("Open"))
            {
                _rootAnimator.Play("Close");
            }
            StartCoroutine(RunPopupDestroy(DestroyView));
            FCacheManager.GetCache(ECacheType.Local).Save("IsNeedTipUserAuth", false);
        }

        private void OnDestroy()
        {
            if (_authButton != null)
            {
                _authButton.onClick.RemoveListener(OnAuthButtonClicked);
            }
        }

        private IEnumerator RunPopupDestroy(Action callback = null)
        {
            var alpha = _group != null ? _group.alpha : 0f;
            var elpased = 0f;
            const float duration = 0.35f;
            while (alpha > 0)
            {
                var p = elpased / duration;
                alpha = Mathf.Lerp(1, 0, p);
                if (_group != null) _group.alpha = alpha;
                elpased += Time.deltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(duration / 2f);
            callback?.Invoke();
            FViewManager.Instance.DestroyInstance(this);
        }



    }
}
