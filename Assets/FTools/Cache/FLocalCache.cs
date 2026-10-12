using UnityEngine;
using Newtonsoft.Json;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using FTools.Config;
using FUnityCommon;

namespace FTools.Cache
{
    public class FLocalCache: ICache
    {



        // 异步保存 + 回调
        public void SaveAsync<T>(string key, T value, Action onCompleted = null)
        {
            throw new NotImplementedException();
        }

        // 异步加载 + 回调
        public void LoadAsync<T>(string key, Action<T> onCompleted, T defaultValue = default)
        {
            throw new NotImplementedException();
        }

        public void Save(string key, object value)
        {
            if (value == null)
            {
                PlayerPrefs.DeleteKey(key);
                return;
            }

            var json = JsonConvert.SerializeObject(value);
            var encrypted = Encrypt(json);

            PlayerPrefs.SetString(key, encrypted);
            PlayerPrefs.Save();
        }

        public T Load<T>(string key, T defaultValue = default)
        {
            if (!PlayerPrefs.HasKey(key))
                return defaultValue;

            var encrypted = PlayerPrefs.GetString(key);

            try
            {
                var json = Decrypt(encrypted);
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch
            {
                return defaultValue;
            }
        }

        private static string Encrypt(string plainText)
        {
            var key = Convert.FromBase64String(FToolsConfigAsset.Instance.aesKey); // 必须 Base64 解码
            var iv = Convert.FromBase64String(FToolsConfigAsset.Instance.aesIv);

            var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;


            var encryptor = aes.CreateEncryptor();
            var input = Encoding.UTF8.GetBytes(plainText);
            var output = encryptor.TransformFinalBlock(input, 0, input.Length);

            return Convert.ToBase64String(output);
        }

        private static string Decrypt(string cipherText)
        {
            var key = Convert.FromBase64String(FToolsConfigAsset.Instance.aesKey);
            var iv = Convert.FromBase64String(FToolsConfigAsset.Instance.aesIv);

            var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            var decryptor = aes.CreateDecryptor();
            var input = Convert.FromBase64String(cipherText);
            var output = decryptor.TransformFinalBlock(input, 0, input.Length);

            return Encoding.UTF8.GetString(output);
        }

    }
}
