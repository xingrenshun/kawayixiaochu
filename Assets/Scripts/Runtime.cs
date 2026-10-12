using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using DG.Tweening;
using Effect;
using FTools.Helpers;
using FTools.Network;
using FTools.Platforms;
using FTools.Tween;
using FUnityAttribute.EditorAttributes;
using FUnityAttribute.RuntimeAttributes;
using FUnityCommon;
using FUnityUI;
using UnityEngine;
using UnityEngine.Serialization;
using View;

[Serializable]
public class SameSpriteItem
{
    public List<Sprite> sprites;
}
public class Runtime : MonoBehaviour
{
    public static Runtime Instance { get; private set; }
    public static int mode = 0;
    public Color pathColor;
    // 格子本色
    public Color gridOriginalColor;
    // 路径不可达提示色
    public Color errorColor;
    // 计分器
    private RuntimeScoreCalculator _scoreCalculator;
    // 关卡收集
    public List<RuntimeCollectionItem> levelCollectItems;

    public List<GameObject> levelParticles;
    public float perGridItemScore;

    [FLabel("背面卡牌")]
    public Sprite backSprite;

    public int currentLevelTargetScore;

    public float passTime;

    public float currentMultiplier = 1f;
    public int combo;
    private int _comboAmountAccumulate;
    private const int COMBO_AMOUNT = 2;
    private const float COMBO_INTERVAL = 3f;
    public float comboTick;

    public bool isGameOver = true;




    [FLabel("相同物品图标")]
    public List<SameSpriteItem> sameSprites;
    

    
    [FResourcesLoad("UI/GameIcons", true)]
    public List<Sprite> gameItems;
    // public List<Sprite> GameItems => gameItems;
    
    [FLabel("记忆模式按图标ID范围过滤")]
    public bool memoryModeFilterByIconIdRange = true;
    [FLabel("记忆模式图标最小ID")]
    public int memoryModeMinIconId = 0;
    [FLabel("记忆模式图标最大ID")]
    public int memoryModeMaxIconId = 98;
    [FormerlySerializedAs("memoryModeDefaultOpenedMinRingCount")]
    [FLabel("记忆模式开局默认翻开最小数量")]
    public int memoryModeDefaultOpenedMinCount = 5;
    [FormerlySerializedAs("memoryModeDefaultOpenedMaxRingCount")]
    [FLabel("记忆模式开局默认翻开最大数量")]
    public int memoryModeDefaultOpenedMaxCount = 12;

    private AutoLayoutGameView _view;

    private void Awake()
    {
        DOTween.useSafeMode = true;
        levelParticles = new List<GameObject>();
        Instance = this;
        _scoreCalculator = new RuntimeScoreCalculator();
        levelCollectItems = new List<RuntimeCollectionItem>();
        
    }

    public void ResetData()
    {
        combo = 1;
        currentMultiplier = 1;
        comboTick = COMBO_INTERVAL;
        currentLevelTargetScore = _scoreCalculator.GetDynamicTargetScore(5000);
        perGridItemScore = (float)currentLevelTargetScore / _view.initTotalGameGridCount * 2;
        
        levelCollectItems.ForEach(i =>
        {
            if (i == null) return;
            Destroy(i.gameObject);
        });
        levelCollectItems.Clear();
        passTime = 90f;
        levelParticles.ForEach(i =>
        {
            if (i == null) return;
            Destroy(i.gameObject);
        });
        levelParticles.Clear();
        // Debug.Log("目标分数： " + currentLevelTargetScore);
        isGameOver = false;
    }

    private void Start()
    {
        // var userInfoOption = new GetUserInfoOption()
        // {
        //     success = (userInfo) =>
        //     {
        //         // 保存用户信息到本地缓存
        //         Debug.Log("GetUserInfo success: " + userInfo);
        //         // _localCache.Save("UserInfo", userInfo.userInfo);
        //     },
        //     fail = (error) =>
        //     {
        //         Debug.Log("GetUserInfo failed: " + error.errMsg);
        //     },
        //     complete = (_) =>
        //     {
        //         Debug.Log("GetUserInfo complete");
        //     }
        // };
        // WX.GetUserInfo(userInfoOption);
        gameItems.Sort((a, b) =>
        {
            var hasA = TryGetIconId(a, out var aId);
            var hasB = TryGetIconId(b, out var bId);
            if (hasA && hasB) return aId - bId;
            if (hasA) return -1;
            if (hasB) return 1;
            return string.CompareOrdinal(a?.name, b?.name);
        });
        FAudioCore.Instance.PlayBGM("gaming-bg");
        FViewManager.Instance.Show<HomeView>();
        // FViewManager.Instance.Show<AutoLayoutGameView>();
        mode = 0;
        // StartGame();
        // PlayerLocalData.Instance.PlayerStamina--;
    }



    public void StartGame()
    {
        isGameOver = false;
        // if (AutoLayoutGameView.Instance != null) AutoLayoutGameView.DestroyView();
        _view= FViewManager.Instance.Show<AutoLayoutGameView>();
        Debug.Log("Grid count: " + _view.GameGirdCount);
        _scoreCalculator.Setup(_view!.GameGirdCount);
        ResetData();
        // WxProfileAuthView.ShowView();

    }

    public static bool TryGetIconId(Sprite sprite, out int id)
    {
        id = -1;
        if (sprite == null || string.IsNullOrEmpty(sprite.name)) return false;
        var split = sprite.name.Split("-");
        return split.Length > 0 && int.TryParse(split[^1], out id);
    }

    public bool IsMemoryModeIcon(Sprite sprite)
    {
        if (!memoryModeFilterByIconIdRange) return true;
        if (!TryGetIconId(sprite, out var id)) return false;
        return id >= memoryModeMinIconId && id <= memoryModeMaxIconId;
    }

    public void AddOrUpdateCollectionItem(int iconId)
    {
        var target = levelCollectItems.Find(x => x.Id == iconId);
        if (target != null)
        {
            target.AddAmount();
            levelCollectItems.Remove(target);
            FAudioCore.Instance.PlaySfx("on-collect");
        }
        else
        {
            target = Instantiate(_view.collectionItem, _view.collectionRoot.transform).GetComponent<RuntimeCollectionItem>();
            target.Setup(iconId, 0, gameItems[iconId]); 
        }
    
        var insertIndex = levelCollectItems.FindIndex(x => x.amount <= target.amount);
        if (insertIndex == -1)
        {
            levelCollectItems.Add(target);
        }
        else
        {
            levelCollectItems.Insert(insertIndex, target);
        }

        for (var i = 0; i < levelCollectItems.Count; i++)
        {
            var item = levelCollectItems[i];
            item.transform.SetSiblingIndex(i); 
            item.index = i;
            item.StartTween(); 
        }
        var rootRect = _view.collectionRoot.transform as RectTransform;
        var cnt = levelCollectItems.Count;
        // 60 is base offset, 20 is spacing
        rootRect!.sizeDelta = new Vector2(target.Width * cnt + 20f * cnt + 60f, rootRect.sizeDelta.y);

        _comboAmountAccumulate++;
        if (_comboAmountAccumulate < COMBO_AMOUNT) return;
        _comboAmountAccumulate = 0;
        comboTick = COMBO_INTERVAL;
        combo = Mathf.Min(10, combo + 1);
        _view.multiplier.color = Color.black;
        _view.multiplier.text = "x" + combo;
        _view.multiplier.transform.GetComponent<FTweenScale>().Replay();
        _view.ShowComboIcon(combo);
    }

    public void AddCombo()
    {
        combo++;
    }

    private void Update()
    {
        if (isGameOver) return;
        if (mode == 1)
        {
            passTime -= Time.deltaTime;
            if (passTime <= 0f)
            {
                isGameOver = true;
                FAsync.Instance.InvokeDelay(() =>
                {
                    AutoLayoutGameView.Instance.gameIsEnded = true;
                    var view = FViewManager.Instance.Show<GameWinView>();
                    view.UpdateTitle("游戏结束");
                }, 1f);
                return;
            }
        }
        var minus = Mathf.FloorToInt(passTime / 60);
        var seconds = Mathf.FloorToInt(passTime % 60);
        _view.timeText.text = $"{minus:00}:{seconds:00}";

        
        comboTick -= Time.deltaTime;
        if (comboTick < 0 && combo != 1)
        {
            _comboAmountAccumulate = 0;
            combo = 1;
            _view.multiplier.DOFade(0, 0.5f);
        }
        
  
    }
}
