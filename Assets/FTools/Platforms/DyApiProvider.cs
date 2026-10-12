using System;
using System.Collections.Generic;
using FTools.Cache;
using FTools.Network;
using JetBrains.Annotations;
using UnityEngine;
using View;
#if PT_dy && !UNITY_EDITOR
using TTSDK;
using TTSDK.UNBridgeLib.LitJson;
#endif

namespace FTools.Platforms
{
    /// <summary>
    /// 抖音（字节小游戏）开放能力封装：登录 / 用户信息 / 分享。
    /// 原微信版本（WeChatWASM / WX.*）已整体替换为 TTSDK（TT.*）。
    /// </summary>
    public class DyApiProvider
    {
        public static bool isLogin;
        public static bool canGetUserInfo;

        private const string DEFAULT_SHARE_TITLE = "卡哇伊连连看";
        private const string DEFAULT_SHARE_DESC = "根本停不下来的连连看，来挑战我的分数！";

        #region 分享

        /// <summary>
        /// 调起抖音小游戏分享（channel=video，平台会使用录屏内容生成分享视频）
        /// </summary>
        public static void ShareApp([CanBeNull] string title, Action callback = null)
        {
#if PT_dy && !UNITY_EDITOR
            try
            {
                var shareData = new JsonData
                {
                    ["channel"] = "video",
                    ["title"] = string.IsNullOrEmpty(title) ? DEFAULT_SHARE_TITLE : title,
                    ["desc"] = DEFAULT_SHARE_DESC
                };

                TT.ShareAppMessage(shareData,
                    _ =>
                    {
                        Debug.Log("[DyApi] 分享成功");
                        callback?.Invoke();
                    },
                    errMsg =>
                    {
                        Debug.LogWarning("[DyApi] 分享失败: " + errMsg);
                        callback?.Invoke();
                    },
                    () =>
                    {
                        Debug.Log("[DyApi] 分享被取消");
                        callback?.Invoke();
                    });
                return;
            }
            catch (Exception e)
            {
                Debug.LogError("[DyApi] 分享异常: " + e.Message);
            }
#endif
            // 非抖音环境（编辑器等）直接走回调，保证 UI 流程不卡住
            callback?.Invoke();
        }

        #endregion

        #region 隐私授权 / 用户信息

        /// <summary>
        /// 查询用户是否已授权「获取用户信息」权限
        /// </summary>
        public static void CheckUserIsAssignAppAuthPermission(Action<bool> callback = null)
        {
            var localCache = FCacheManager.GetCache(ECacheType.Local);
#if PT_dy && !UNITY_EDITOR
            try
            {
                TT.GetUserInfoAuth(isAuthed =>
                    {
                        Debug.Log("[DyApi] 用户信息授权状态: " + isAuthed);
                        localCache.Save("IsAuthed", isAuthed);
                        callback?.Invoke(isAuthed);
                        if (isAuthed)
                        {
                            SyncUserInfo();
                        }
                        else
                        {
                            Debug.Log("[DyApi] 用户未授权隐私信息");
                        }
                    },
                    errMsg =>
                    {
                        Debug.LogWarning("[DyApi] 获取授权状态失败: " + errMsg);
                        localCache.Save("IsAuthed", false);
                        callback?.Invoke(false);
                    });
                return;
            }
            catch (Exception e)
            {
                Debug.LogError("[DyApi] 获取授权状态异常: " + e.Message);
            }
#endif
            localCache.Save("IsAuthed", false);
            callback?.Invoke(false);
        }

        private static void SyncUserInfo()
        {
            var localCache = FCacheManager.GetCache(ECacheType.Local);
#if PT_dy && !UNITY_EDITOR
            Debug.Log("[DyApi] 用户已授权隐私信息: 开始获取隐私信息");
            try
            {
                TT.GetUserInfo((ref TTUserInfo userInfo) =>
                    {
                        localCache.Save("avatarUrl", userInfo.avatarUrl);
                        localCache.Save("nickname", userInfo.nickName);

                        FRequest.Instance.Post<object>("update/user", new Dictionary<string, object>()
                        {
                            {"openId", localCache.Load<string>("openId")},
                            {"avatarUrl", userInfo.avatarUrl},
                            {"nickname", userInfo.nickName}
                        });
                    },
                    errMsg =>
                    {
                        Debug.LogWarning("[DyApi] 获取用户信息失败: " + errMsg);
                    });
            }
            catch (Exception e)
            {
                Debug.LogError("[DyApi] 获取用户信息异常: " + e.Message);
            }
#endif
        }

        #endregion

        #region 登录

        /// <summary>
        /// SDK 初始化入口：先处理隐私协议授权，再走登录
        /// </summary>
        public static void InitSDK()
        {
#if PT_dy && !UNITY_EDITOR
            try
            {
                TT.GetPrivacySetting(new TTGetPrivacySettingParam
                {
                    success = res =>
                    {
                        if (res.needAuthorization)
                        {
                            Debug.Log("[DyApi] 需要用户授权隐私协议");
                            TT.RequirePrivacyAuthorize(new TTBaseParam
                            {
                                success = _ =>
                                {
                                    CheckUserIsAssignAppAuthPermission();
                                    StartupLogin();
                                },
                                fail = info =>
                                {
                                    // 拒绝了也无事，后续必要操作会再次拉起
                                    Debug.LogWarning("[DyApi] 用户拒绝了隐私授权: " + info.errMsg);
                                },
                                complete = () => { }
                            });
                        }
                        else
                        {
                            Debug.Log("[DyApi] 用户已授权隐私权限");
                            CheckUserIsAssignAppAuthPermission();
                            StartupLogin();
                        }
                    },
                    fail = info =>
                    {
                        Debug.LogWarning("[DyApi] 获取隐私设置失败: " + info.errMsg);
                        StartupLogin();
                    },
                    complete = () => { }
                });
            }
            catch (Exception e)
            {
                Debug.LogError("[DyApi] 隐私授权流程异常: " + e.Message);
                StartupLogin();
            }
#else
            Debug.Log("[DyApi] 编辑器环境，跳过抖音 SDK 初始化");
#endif
        }

        // 抖音登录流程
        private static void StartupLogin()
        {
#if PT_dy && !UNITY_EDITOR
            try
            {
                TT.Login((code, anonymousCode, isLoginState) =>
                    {
                        Debug.Log("[DyApi] 抖音登录成功, isLogin=" + isLoginState);
                        FRequest.Instance.Post<WxLoginResponse>("wx/login", new Dictionary<string, object>()
                        {
                            {"code", code},
                            {"anonymousCode", anonymousCode},
                            {"platform", FPlatformCore.GetCurrentPlatform()}
                        }, resp =>
                        {
                            isLogin = resp.success;
                            if (!resp.success)
                            {
                                // TODO 发邮件通知我自己
                                AlertTipView.Instance!.FadeIn("在线服务暂不可用");
                                return;
                            }

                            var data = resp.data;
                            FCacheManager.GetCache(ECacheType.Local).Save("openId", data.openId);
                            FCacheManager.GetCache(ECacheType.Local).Save("sessionKey", data.sessionKey);
                        });
                    },
                    errMsg =>
                    {
                        Debug.LogWarning("[DyApi] 抖音登录失败: " + errMsg);
                    },
                    true);
            }
            catch (Exception e)
            {
                Debug.LogError("[DyApi] 抖音登录异常: " + e.Message);
            }
#endif
        }

        #endregion
    }
}
