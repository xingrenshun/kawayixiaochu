using System;
using FUnityCommon;
using JetBrains.Annotations;
using UnityEngine;
#if PT_dy && !UNITY_EDITOR
using TTSDK;
using TTSDK.UNBridgeLib.LitJson;
#endif

namespace FTools.Platforms
{
    public class FPlatformCore : FSingletonCSharp<FPlatformCore>
    {
        private DyApiProvider _dyApiProvider;

        protected override void OnInitialize()
        {
            _dyApiProvider = new DyApiProvider();
        }

        public static string GetCurrentPlatform()
        {
#if PT_wx
            return "wx";
#elif PT_dy
            return "dy";
#elif PT_ks
            return "ks";
#elif PT_vivo
            return "vivo";
#elif PT_oppo
            return "oppo";
#elif PT_huawei
            return "huawei";
#elif PT_xiaomi
            return "xiaomi";
#elif PT_bilibili
            return "bilibili";
#elif PT_android
            return "android";
#elif PT_ios
            return "ios";
#elif PT_taptap
            return "taptap";
#elif PT_web
            return "web";
#else 
            return "dy";
#endif
        }


        #region 分享

        public void ShareApp([CanBeNull] string title, Action callback = null)
        {
            DyApiProvider.ShareApp(title, callback);
        }


        /// <summary>
        /// 跳转到小游戏侧边栏（原微信「游戏圈」入口的抖音端等价实现）
        /// </summary>
        public static void GotoGameClub()
        {
#if PT_dy && !UNITY_EDITOR
            try
            {
                var data = new JsonData
                {
                    ["scene"] = "sidebar"
                };
                TT.NavigateToScene(data,
                    () => Debug.Log("[FPlatformCore] 侧边栏打开成功"),
                    () => { },
                    (errCode, errMsg) => Debug.LogWarning($"[FPlatformCore] 侧边栏打开失败: {errCode}, {errMsg}"));
            }
            catch (Exception e)
            {
                Debug.LogError("[FPlatformCore] 打开侧边栏异常: " + e.Message);
            }
#endif
        }

        #endregion

        public void IsAssignGetUserInfoPermission(Action<bool> callback)
        {
            DyApiProvider.CheckUserIsAssignAppAuthPermission(callback);
        }

        public void InitSdk()
        {
            DyApiProvider.InitSDK();
        }
    }
}
