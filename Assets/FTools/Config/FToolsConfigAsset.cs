using System;
using System.Collections.Generic;
using System.IO;
using FUnityAttribute.EditorAttributes;
using UnityEngine;
using UnityEngine.Serialization;

namespace FTools.Config
{



    [CreateAssetMenu(fileName = "FLocalCacheConfig", menuName = "FTools/LocalCache Config")]
    public class FToolsConfigAsset : ScriptableObject
    {
        [Header("AES-256 Key (32 chars)")]
        public string aesKey = "FLOCALCACHE_AES_KEY_32BYTES";

        [FormerlySerializedAs("aesIV")] [Header("AES IV (16 chars)")]
        public string aesIv = "FLOCALCACHE_AES_IV_16B";

        [Header("网络配置部分")] 
        public string serverBaseUrl = "127.0.0.1";
        [FLabel("使用 HTTPS ")]
        public bool useSsl = false;
        [FLabel("请求失败重试次数")]
        public int failRetryCount = 5;
        [FLabel("API Key")]
        public string apiKey = "FLOCALCACHE_API_KEY";

        
        private static FToolsConfigAsset _config;
        public static FToolsConfigAsset Instance
        {
            get
            {
                if (_config != null) return _config;
                _config = Resources.Load<FToolsConfigAsset>("FDependence/LocalCacheConfig");
                if (_config == null) Debug.LogError("缺少 FLocalCacheConfig，请在 Resources 创建！");
                return _config;
            }
        }


        // ⚠️ 以下广告位字段刻意「不加 #if 条件编译」。
        // ScriptableObject 的序列化字段一旦被 #if 包裹，打包时与运行时的类型布局就可能不一致，
        // Unity 会报 "has a different serialization layout when loading"，
        // 进而把整个 LocalCacheConfig 对象丢弃 → Resources.Load 返回 null → 平台初始化崩溃。
        // 字段常驻的好处：任何平台 / 任何 Build Target 的布局都恒定；用不到的留空即可。

        [FLabel("抖音小游戏 AppId（以 07 结尾）")]
        public string douyinAppId = "";

        [Header("Banners")]
        public List<string> bannerIds;

        [Header("优选广告")]
        public List<string> preferred;

        [FLabel("插屏广告 ID")]
        public string interstitials;


        [FLabel("激励视频")] public string videoId;
    }
}