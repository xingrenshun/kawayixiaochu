namespace Response
{
    [System.Serializable]
    public class UserInfoDto
    {
        public string nickname;
        public string avatar;
    }

    public class WxLoginResponse
    {
        public string sessionKey;
        public string openId;
    }
}