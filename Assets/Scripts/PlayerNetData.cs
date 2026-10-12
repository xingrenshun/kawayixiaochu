using FTools.Cache;
using FUnityCommon;
using UnityEngine;

public class PlayerNetData : FSingletonCSharp<PlayerNetData>
{
    private ICache _cache;

    protected override void OnInitialize()
    {
        _cache = FCacheManager.GetCache(ECacheType.Local);
    }

    public int PlayerLevel
    {
        get => _cache.Load("PlayerLevel", 1);
        set => _cache.Save("PlayerLevel", value);
    }

    public Sprite avatar = null;
}