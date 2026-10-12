using System;
using System.Threading.Tasks;

namespace FTools.Cache
{
    
    public enum ECacheType
    {
        Local,
        Net
    }
    public interface ICache
    {
        void SaveAsync<T>(string key, T value, Action onCompleted = null);
        void LoadAsync<T>(string key, Action<T> onCompleted, T defaultValue = default);
        void Save(string key, object value);
        T Load<T>(string key, T defaultValue = default);
    }
    public static class FCacheManager
    { 
        private static readonly ICache LocalCache = new FLocalCache();
        private static readonly ICache NetCache = new FNetCache();

        public static ICache GetCache(ECacheType type)
        {
            return type switch
            {
                ECacheType.Local => LocalCache,
                ECacheType.Net => NetCache,
                _ => LocalCache
            };
        }
    }
}