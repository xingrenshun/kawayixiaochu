using System;
using System.Collections.Generic;
using FTools.Config;
using FUnityCommon;
using UnityEngine;
#if PT_dy && !UNITY_EDITOR
using TTSDK;
#endif

namespace FTools.Platforms
{
    /// <summary>
    /// 广告统一入口（抖音端）。
    /// 激励视频 / 插屏广告直接复用项目内的 AdSDK（AdManager + TikTokPlatform），不做二次封装；
    /// Banner 广告 AdSDK 没有封装，这里直接用 TTSDK（TT.CreateBannerAd）实现。
    /// 原微信实现（WeChatWASM / WXCustomAd / WXInterstitialAd / WXRewardedVideoAd）已整体替换。
    /// </summary>
    public class FAds : FSingletonCSharp<FAds>
    {
        private const float BANNER_ROTATE_INTERVAL = 30f;
        private const int BANNER_WIDTH = 350;
        private const int BANNER_BOTTOM_OFFSET = 120;

        private int _currentBannerIndex;
        private bool _isBannerVisible;
        private BannerRefreshDriver _bannerRefreshDriver;

#if PT_dy && !UNITY_EDITOR
        private static AdManager _runtimeAdManager;
        private readonly List<TTBannerAd> _bannerAds = new List<TTBannerAd>();
#endif

        protected override void OnInitialize()
        {
            EnsureBannerRefreshDriver();
            EnsureAdManager();
            InitBannerAds();
        }

        #region 激励视频

        public void Video(Action<bool> callback, string remark = "")
        {
#if PT_dy && !UNITY_EDITOR
            var manager = EnsureAdManager();
            if (manager == null)
            {
                Debug.LogError("[FAds] AdManager 不可用，无法播放激励视频");
                Time.timeScale = 1f;
                callback?.Invoke(false);
                return;
            }

            Debug.Log("[FAds] 播放激励视频" + (string.IsNullOrEmpty(remark) ? string.Empty : $"（{remark}）"));
            // 注意：AdSDK 内部会在播放时把 Time.timeScale 置 0，并在关闭/出错时恢复为 1
            manager.ShowRewardedVideoAd(
                onSuccess: () => callback?.Invoke(true),
                onFail: err =>
                {
                    Debug.LogError("[FAds] 激励视频失败: " + err);
                    callback?.Invoke(false);
                });
#else
            // 编辑器 / 非抖音环境：直接发奖，便于调试
            Time.timeScale = 1f;
            callback?.Invoke(true);
#endif
        }

        #endregion

        #region 插屏广告

        public void ShowInterstitialAd()
        {
#if PT_dy && !UNITY_EDITOR
            var manager = EnsureAdManager();
            if (manager == null)
            {
                Debug.LogWarning("[FAds] AdManager 不可用，无法展示插屏广告");
                return;
            }

            manager.ShowInterstitialAd();
#endif
        }

        #endregion

        #region Banner 广告

        public void ShowBannerAd()
        {
            EnsureBannerRefreshDriver();
            _isBannerVisible = true;
#if PT_dy && !UNITY_EDITOR
            if (_bannerAds.Count <= 0) return;
            HideAllBannerAds();
            _bannerAds[_currentBannerIndex].Show();
            _currentBannerIndex = (_currentBannerIndex + 1) % _bannerAds.Count;
#endif
            _bannerRefreshDriver?.Restart(BANNER_ROTATE_INTERVAL);
        }

        public void HideBannerAd()
        {
            _isBannerVisible = false;
            _bannerRefreshDriver?.Stop();
            HideAllBannerAds();
        }

        public void RefreshBannerAd()
        {
            if (!_isBannerVisible) return;
            ShowBannerAd();
        }

        private void HideAllBannerAds()
        {
#if PT_dy && !UNITY_EDITOR
            try
            {
                _bannerAds.ForEach(i => i.Hide());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[FAds] 隐藏 Banner 异常: " + e.Message);
            }
#endif
        }

        private void InitBannerAds()
        {
#if PT_dy && !UNITY_EDITOR
            var config = FToolsConfigAsset.Instance;
            if (config == null || config.bannerIds == null || config.bannerIds.Count == 0) return;

            var systemInfo = TT.GetSystemInfo();
            var screenWidth = systemInfo?.screenWidth ?? Screen.width;
            var screenHeight = systemInfo?.screenHeight ?? Screen.height;

            foreach (var bannerId in config.bannerIds)
            {
                if (string.IsNullOrWhiteSpace(bannerId)) continue;
                try
                {
                    _bannerAds.Add(TT.CreateBannerAd(new CreateBannerAdParam
                    {
                        BannerAdId = bannerId.Trim(),
                        AdIntervals = (int)BANNER_ROTATE_INTERVAL,
                        Style = new TTBannerStyle
                        {
                            width = BANNER_WIDTH,
                            left = (screenWidth - BANNER_WIDTH) / 2,
                            top = screenHeight - BANNER_BOTTOM_OFFSET
                        }
                    }));
                }
                catch (Exception e)
                {
                    Debug.LogError("[FAds] 创建 Banner 失败: " + e.Message);
                }
            }

            Debug.Log($"[FAds] 已创建 Banner 数量: {_bannerAds.Count}");
#endif
        }

        private void EnsureBannerRefreshDriver()
        {
            if (_bannerRefreshDriver != null) return;
            var driverObject = new GameObject("[FAds]-BannerRefreshDriver");
            UnityEngine.Object.DontDestroyOnLoad(driverObject);
            _bannerRefreshDriver = driverObject.AddComponent<BannerRefreshDriver>();
            _bannerRefreshDriver.Setup(this);
        }

        #endregion

        #region AdSDK 桥接

        /// <summary>
        /// 拿到项目内 AdSDK 的 AdManager。
        /// 场景里已经挂了 AdManager 就用它；没挂则自动创建一份，并用 FTools 配置里的广告位兜底，
        /// 保证游戏上传抖音后广告能力可用（不修改 AdSDK 源码）。
        /// </summary>
        private static AdManager EnsureAdManager()
        {
#if PT_dy && !UNITY_EDITOR
            if (AdManager.Instance != null) return AdManager.Instance;
            if (_runtimeAdManager != null) return _runtimeAdManager;

            try
            {
                var driverObject = new GameObject("[FAds]-AdManager");
                UnityEngine.Object.DontDestroyOnLoad(driverObject);
                // AddComponent 会立刻执行 AdManager.Awake，所以这里的赋值会先于 AdManager.Start 生效
                var manager = driverObject.AddComponent<AdManager>();

                var config = FToolsConfigAsset.Instance;
                if (config != null)
                {
                    if (!string.IsNullOrWhiteSpace(config.douyinAppId)) manager.AppId = config.douyinAppId.Trim();
                    if (!string.IsNullOrWhiteSpace(config.videoId))
                        manager.videoAdIdList = new List<string> { config.videoId.Trim() };
                    if (!string.IsNullOrWhiteSpace(config.interstitials))
                        manager.interAdIdList = new List<string> { config.interstitials.Trim() };
                }

                _runtimeAdManager = manager;
                Debug.Log("[FAds] 场景中未找到 AdManager，已自动创建并注入 FTools 广告位配置");
                return manager;
            }
            catch (Exception e)
            {
                Debug.LogError("[FAds] 自动创建 AdManager 失败: " + e.Message);
                return null;
            }
#else
            return null;
#endif
        }

        #endregion

        private class BannerRefreshDriver : MonoBehaviour
        {
            private FAds _owner;
            private float _interval;
            private float _elapsed;
            private bool _isRunning;

            public void Setup(FAds owner)
            {
                _owner = owner;
            }

            public void Restart(float interval)
            {
                _interval = Mathf.Max(1f, interval);
                _elapsed = 0f;
                _isRunning = true;
            }

            public void Stop()
            {
                _isRunning = false;
                _elapsed = 0f;
            }

            private void Update()
            {
                if (!_isRunning || _owner == null) return;
                _elapsed += Time.unscaledDeltaTime;
                if (_elapsed < _interval) return;

                _elapsed = 0f;
                _owner.RefreshBannerAd();
            }
        }
    }
}
