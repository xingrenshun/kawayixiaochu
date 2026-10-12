using System;
using System.Collections;
using FTools.Cache;
using FTools.Helpers;
using FUnityCommon;
using UnityEngine;

public class PlayerLocalData : FSingletonCSharp<PlayerLocalData>
{
    // ===== 配置 =====
    private const int MAX_STAMINA = 10;
    private const int RECOVERY_SECONDS = 240; // 每 120 秒恢复 1 点

    // ===== 本地缓存 Key =====
    private const string STAMINA_KEY = "Stamina";
    private const string LAST_COST_TIME_KEY = "Stamina_LastCostTime";
    private const string USER_SHARED_KEY = "User_Shared";
    private const string FLIP_ALL_ITEM_AMOUNT_KEY = "FlipAllItemAmount";
    private const string PRECISION_LOCK_ITEM_AMOUNT_KEY = "PrecisionLockItemAmount";
    private const string MEMORY_TUTORIAL_COMPLETED_KEY = "MemoryTutorialCompleted";
    
    

    // ===== 成员变量 =====
    private ICache _localCache;
    private int _stamina;

    // ===== 对外事件 =====
    /// <summary>体力变化</summary>
    public event Action<int> OnStaminaChanged;

    /// <summary>下一次体力恢复剩余时间</summary>
    public event Action<TimeSpan> OnNextRecoveryTimeChanged;

    public void ImmediateTriggerCallback()
    {
        OnStaminaChanged?.Invoke(_stamina);
        OnNextRecoveryTimeChanged?.Invoke(NextRecoveryTime);
    }

    // ===== 初始化 =====
    protected override void OnInitialize()
    {
        _localCache = FCacheManager.GetCache(ECacheType.Local);

        _stamina = LoadStamina();
        RecoverOfflineStamina();

        FAsync.Instance.StartRoutine(StartStaminaRecovery());
    }

    // ===== 体力恢复主协程 =====
    private IEnumerator StartStaminaRecovery()
    {
        while (true)
        {
            if (_stamina < MAX_STAMINA)
            {
                var lastCostTime = LoadLastCostTime();
                var now = GetNowUnixTime();

                var elapsed = now - lastCostTime;
                var recoverCount = elapsed / RECOVERY_SECONDS;

                if (recoverCount > 0)
                {
                    AddStamina((int)recoverCount);
                    SaveLastCostTime(now - (elapsed % RECOVERY_SECONDS));
                }

                var remainSeconds = RECOVERY_SECONDS - (elapsed % RECOVERY_SECONDS);

                OnNextRecoveryTimeChanged?.Invoke(
                    TimeSpan.FromSeconds(remainSeconds)
                );
            }
            else
            {
                OnNextRecoveryTimeChanged?.Invoke(TimeSpan.Zero);
            }

            yield return new WaitForSecondsRealtime(1f);
        }
    }

    // ===== 对外接口 =====
    public int PlayerStamina
    {
        get => _stamina;
        set
        {
            // value = Mathf.Clamp(value, 0, MAX_STAMINA);


            if (_stamina >= MAX_STAMINA && value < MAX_STAMINA)
            {
                SaveLastCostTime(GetNowUnixTime());
            }

            _stamina = value;
            SaveStamina(_stamina);

            OnStaminaChanged?.Invoke(_stamina);
        }
    }

    /// <summary>
    /// 当前下一次恢复剩余时间（可供 UI 主动拉取）
    /// </summary>
    public TimeSpan NextRecoveryTime
    {
        get
        {
            if (_stamina >= MAX_STAMINA)
                return TimeSpan.Zero;

            var elapsed = GetNowUnixTime() - LoadLastCostTime();
            var remain = RECOVERY_SECONDS - (elapsed % RECOVERY_SECONDS);
            return TimeSpan.FromSeconds(remain);
        }
    }

    // ===== 内部逻辑 =====
    private void AddStamina(int count)
    {
        if (count <= 0) return;

        _stamina = Mathf.Min(MAX_STAMINA, _stamina + count);
        SaveStamina(_stamina);

        OnStaminaChanged?.Invoke(_stamina);
    }

    private void RecoverOfflineStamina()
    {
        if (_stamina >= MAX_STAMINA) return;

        var lastCostTime = LoadLastCostTime();
        var now = GetNowUnixTime();

        var elapsed = now - lastCostTime;
        var recoverCount = elapsed / RECOVERY_SECONDS;

        if (recoverCount > 0)
        {
            AddStamina((int)recoverCount);
            SaveLastCostTime(now - (elapsed % RECOVERY_SECONDS));
        }
    }

    // ===== 本地缓存 =====
    private int LoadStamina()
    {
        return _localCache.Load(STAMINA_KEY, MAX_STAMINA);
    }

    private void SaveStamina(int value)
    {
        _localCache.Save(STAMINA_KEY, value);
    }

    private long LoadLastCostTime()
    {
        return _localCache.Load(LAST_COST_TIME_KEY, GetNowUnixTime());
    }

    private void SaveLastCostTime(long time)
    {
        _localCache.Save(LAST_COST_TIME_KEY, time);
    }

    public bool IsUserShared
    {
        get => _localCache.Load(USER_SHARED_KEY, false);
        set => _localCache.Save(USER_SHARED_KEY, value);
    }

    public int RefreshItemAmount
    {
        get => _localCache.Load("RefreshItemAmount", 5);
        set => _localCache.Save("RefreshItemAmount", value);
    }

    public int PathTipItemAmount
    {
        get => _localCache.Load("PathTipItemAmount", 5);
        set => _localCache.Save("PathTipItemAmount", value);
    }

    public int FlipAllItemAmount
    {
        get => Mathf.Max(0, _localCache.Load(FLIP_ALL_ITEM_AMOUNT_KEY, 0));
        set => _localCache.Save(FLIP_ALL_ITEM_AMOUNT_KEY, Mathf.Max(0, value));
    }

    public int PrecisionLockItemAmount
    {
        get => Mathf.Max(0, _localCache.Load(PRECISION_LOCK_ITEM_AMOUNT_KEY, 0));
        set => _localCache.Save(PRECISION_LOCK_ITEM_AMOUNT_KEY, Mathf.Max(0, value));
    }

    public bool MemoryTutorialCompleted
    {
        get => _localCache.Load(MEMORY_TUTORIAL_COMPLETED_KEY, false);
        set => _localCache.Save(MEMORY_TUTORIAL_COMPLETED_KEY, value);
    }
    // ===== 时间工具 =====
    private static long GetNowUnixTime()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
