using System;

namespace FTools.Network
{
    [Serializable]
    public struct FResponse<T>
    {
        public int code;
        public bool success;
        public string msg;
        public T data;
        public long time;
    }
    
    [Serializable]
    public class WxLoginResponse
    {
        public string sessionKey;
        public string openId;
    }
}