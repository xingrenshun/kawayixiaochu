using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using FTools.Config;
using FTools.Platforms;
using FUnityCommon;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace FTools.Network
{
    public class FRequest : FSingletonCSharp<FRequest>
    {
        private const string HEADER_API_KEY = "X-API-KEY";
        private const string HEADER_CONTENT_TYPE = "Content-Type";
        private const string HEADER_PLATFORM = "X-Platform";
        private const string CONTENT_TYPE_JSON = "application/json";

        protected override void OnInitialize()
        {
        }

        #region 公共接口 (回调版)


        /// <summary>
        /// 发起 GET 请求
        /// </summary>
        public void Get<T>(string endpoint, [CanBeNull] object bodyData = null, Action<FResponse<T>> callback = null)
        {
            var url = BuildUrl(endpoint);
            var jsonBody = bodyData != null ? JsonConvert.SerializeObject(bodyData) : null;
            Debug.Log("[GET] Json body: " + jsonBody);
            // 启动异步任务，但不等待，完成后触发回调
            _ = RunRequestInternal(url, "GET", jsonBody, callback);
        }

        /// <summary>
        /// 发起 POST 请求
        /// </summary>
        public void Post<T>(string endpoint, object bodyData, Action<FResponse<T>> callback = null)
        {
            var url = BuildUrl(endpoint);
            var jsonBody = bodyData != null ? JsonConvert.SerializeObject(bodyData) : null;
            _ = RunRequestInternal(url, "POST", jsonBody, callback);
        }
        
        /// <summary>
        /// 发起 PUT 请求
        /// </summary>
        public void Put<T>(string endpoint, object bodyData, Action<FResponse<T>> callback = null)
        {
            var url = BuildUrl(endpoint);
            var jsonBody = bodyData != null ? JsonConvert.SerializeObject(bodyData) : null;
            _ = RunRequestInternal(url, "PUT", jsonBody, callback);
        }

        /// <summary>
        /// 发起 DELETE 请求
        /// </summary>
        public void Delete<T>(string endpoint, [CanBeNull] object bodyData = null, Action<FResponse<T>> callback = null)
        {
            var url = BuildUrl(endpoint);
            var jsonBody = bodyData != null ? JsonConvert.SerializeObject(bodyData) : null;
            _ = RunRequestInternal(url, "DELETE", jsonBody, callback);
        }

        #endregion

        #region 内部桥接

        /// <summary>
        /// 这是一个 "Fire-and-Forget" 的异步方法，用来桥接 async 逻辑和 callback 回调
        /// </summary>
        private async Task RunRequestInternal<T>(string url, string method, string jsonBody, Action<FResponse<T>> callback)
        {
            try
            {
                // 等待核心逻辑执行完毕
                var response = await SendRequestCoreAsync<T>(url, method, jsonBody);
                
                // 执行回调（并在主线程，因为 await 恢复后通常在主线程）
                callback?.Invoke(response);
            }
            catch (Exception e)
            {
                Debug.LogError($"[FRequest] Callback Wrapper Error: {e}");
                // 防止回调丢失，发生严重错误时返回一个默认的错误包
                callback?.Invoke(CreateErrorResponse<T>(-999, $"Internal Exception: {e.Message}"));
            }
        }

        #endregion

        #region 核心逻辑

        private async Task<FResponse<T>> SendRequestCoreAsync<T>(string url, string method, string jsonBody)
        {
            var maxRetry = FToolsConfigAsset.Instance.failRetryCount;
            var apiKey = FToolsConfigAsset.Instance.apiKey;
            for (var i = 0; i <= maxRetry; i++)
            {
                var requestBody = FRequestPayload.MakeRequestPayload(!string.IsNullOrEmpty(jsonBody) ? jsonBody : Guid.NewGuid().ToString().Replace("-", ""));
                using var uwr = new UnityWebRequest(url, method);
                uwr.downloadHandler = new DownloadHandlerBuffer();
                var bodyRaw = Encoding.UTF8.GetBytes(requestBody);
                uwr.uploadHandler = new UploadHandlerRaw(bodyRaw);
                uwr.SetRequestHeader(HEADER_CONTENT_TYPE, CONTENT_TYPE_JSON);
                uwr.SetRequestHeader(HEADER_PLATFORM, FPlatformCore.GetCurrentPlatform());
                if (!string.IsNullOrEmpty(apiKey))
                {
                    uwr.SetRequestHeader(HEADER_API_KEY, apiKey);
                }

                try
                {
                    // 使用扩展方法 await
                    await uwr.SendWebRequest();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[FRequest] System Exception: {ex.Message}");
                    // 继续重试循环
                }

                if (uwr.result == UnityWebRequest.Result.Success)
                {
                    var jsonResponse = uwr.downloadHandler.text;
                    try
                    {
                        return JsonConvert.DeserializeObject<FResponse<T>>(jsonResponse);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[FRequest] JSON 解析失败: {e.Message} \nRaw: {jsonResponse}");
                        return CreateErrorResponse<T>(-2, "JSON Parse Error");
                    }
                }

                var isLastTry = (i == maxRetry);
                var errorMsg = $"HTTP Error: {uwr.responseCode} - {uwr.error}";

                Debug.LogWarning($"[FRequest] 请求失败 ({i + 1}/{maxRetry + 1}): {url} \n{errorMsg}");

                if (isLastTry)
                {
                    return CreateErrorResponse<T>((int)uwr.responseCode, uwr.error);
                }
                
                // 异步等待 1 秒后重试
                await Task.Delay(1000);
            }

            return CreateErrorResponse<T>(-1, "Unknown Error");
        }

        #endregion

        #region 辅助方法

        private static string BuildUrl(string endpoint)
        {
            var config = FToolsConfigAsset.Instance;
            string protocol = config.useSsl ? "https" : "http";
            return $"{protocol}://{config.serverBaseUrl}/{endpoint}";
        }

        private static FResponse<T> CreateErrorResponse<T>(int errCode, string errMsg)
        {
            return new FResponse<T>
            {
                code = errCode, 
                msg = errMsg,
                success = false,
                data = default
            };
        }

        #endregion
    }

    public static class UnityWebRequestExtension
    {
        public static System.Runtime.CompilerServices.TaskAwaiter<UnityWebRequest> GetAwaiter(
            this UnityWebRequestAsyncOperation op)
        {
            var tcs = new TaskCompletionSource<UnityWebRequest>();
            op.completed += operation => tcs.SetResult(((UnityWebRequestAsyncOperation)operation).webRequest);
            return tcs.Task.GetAwaiter();
        }
    }
}
