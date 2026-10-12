using System;
using System.Threading.Tasks;
using FTools.Network;
using UnityEngine;

namespace FTools.Cache
{
    public class FNetCache : ICache
    {
        private const string SAVE_ENDPOINT = "cache/save";
        private const string LOAD_ENDPOINT = "cache/load";

        public void SaveAsync<T>(string key, T value, Action onCompleted = null)
        {
            var body = new CacheSaveRequest<T>
            {
                key = key,
                value = value
            };

            // 注意：因为服务端返回的数据通常不重要，这里泛型用 object 或 FResponse 中定义的空结构
            FRequest.Instance.Post<object>(SAVE_ENDPOINT, body, (resp) =>
            {
                if (!resp.success)
                {
                    Debug.LogWarning($"[FNetCache] Save failed: {resp.code} {resp.msg}");
                }
                
                // 无论成功失败，都通知上层完成（或者你可以根据需求只在成功时调用）
                onCompleted?.Invoke();
            });
        }
        public void LoadAsync<T>(string key, Action<T> onCompleted, T defaultValue = default)
        {
            var endpoint = $"{LOAD_ENDPOINT}?key={key}";

            // 调用新的 FRequest.Get (无返回值，直接传回调)
            // Get 的第二个参数是 bodyData，这里传 null
            FRequest.Instance.Get<T>(endpoint, null, (resp) =>
            {
                if (!resp.success)
                {
                    Debug.LogWarning($"[FNetCache] Load failed: {resp.code} {resp.msg}");
                    onCompleted?.Invoke(defaultValue);
                    return;
                }
                onCompleted?.Invoke(resp.data);
            });
        }

        #region ICache 接口的同步方法 (网络层无法实现同步，保持报错或空实现)



        public void Save(string key, object value)
        {
            Debug.LogError("[FNetCache] 网络缓存不支持同步 Save，请使用 Save<T>(key, value, callback)");
            throw new NotImplementedException("网络缓存只能使用异步API");
        }

        public T Load<T>(string key, T defaultValue = default)
        {
            Debug.LogError("[FNetCache] 网络缓存不支持同步 Load，请使用 Load<T>(key, callback)");
            throw new NotImplementedException("网络缓存只能使用异步API");
        }

        #endregion
    }

    [Serializable]
    internal class CacheSaveRequest<T>
    {
        public string key;
        public T value;
    }
}
