using FTools.Cache;
using FUnityCommon;
using UnityEngine;
#if PT_dy && !UNITY_EDITOR
using TTSDK;
#endif

namespace FTools.Helpers
{
    public class FDeviceCore : FSingletonCSharp<FDeviceCore>
    {
        public static bool VibrateEnabled
        {
            get => FCacheManager.GetCache(ECacheType.Local).Load("IsOpenVibrate", true);
            set => FCacheManager.GetCache(ECacheType.Local).Save("IsOpenVibrate", value);
        }

        public static void Vibrate()
        {
#if PT_dy && !UNITY_EDITOR
            try
            {
                TT.VibrateShort(new VibrateShortParam
                {
                    Fail = info => Debug.LogWarning("[FDeviceCore] 短震动失败: " + info.errMsg),
                    Complete = () => { }
                });
            }
            catch (System.Exception e)
            {
                Debug.LogError("[FDeviceCore] 短震动异常: " + e.Message);
            }
#endif
        }

        public static void VibrateLongTime()
        {
#if PT_dy && !UNITY_EDITOR
            try
            {
                TT.VibrateLong(new VibrateLongParam
                {
                    Fail = info => Debug.LogWarning("[FDeviceCore] 长震动失败: " + info.errMsg),
                    Complete = () => { }
                });
            }
            catch (System.Exception e)
            {
                Debug.LogError("[FDeviceCore] 长震动异常: " + e.Message);
            }
#endif
        }
    }
}
