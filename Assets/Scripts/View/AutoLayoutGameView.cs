using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Algorithm;
using DG.Tweening;
using Effect;
using FCSharpExt;
using FTools.Helpers;
using FTools.Platforms;
using FTools.Tween;
using FUnityAttribute.RuntimeAttributes;
using FUnityCommon;
using FUnityUI;
using GameGrid;
using SObjects;
using TMPro;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UI;
using Grid = GameGrid.Grid;
using Random = UnityEngine.Random;

namespace View
{
    [FViewLoader("View/GameView")]
    public class AutoLayoutGameView : FUiBase<AutoLayoutGameView>
    {
        private const float OFFSET_X_MAX = -25;
        private const float OFFSET_X_MIN = 25;
        private const float OFFSET_Y_MAX = -400;
        private const float OFFSET_Y_MIN = 400;

        private Color _defaultGameAreaColor;


        private bool _isFirstEnterGame = true;

        public bool gameIsEnded;

        // 输入锁定标志（用于旋转时禁止操作）
        private bool _isInputLocked;

        private BoomSpawner _boomSpawner;


        private LevelConfigItem _currentConfig;

        
        // 背景格子对象
        [FGameObject] private GameObject _gridItem;

        // 游戏格子对象
        [FGameObject] private GameObject _gameGrid;

        // 游戏中收集对象渲染父节点
        [FGameObject] public GameObject collectionRoot;

        // 游戏中收集对象
        [FGameObject] public GameObject collectionItem;
        [FGameObject] public GameObject boomItem;
        
        // 设置按钮
        [FGameObject("open-settings-btn")] private GameObject _openSettingsBtn;

        // 计时对象
        [FGameObject("time-hangout")] public GameObject timeHangout;
        [FuiAutowire] public RectTransform gameArea;

        // 游戏总进度条
        [FuiAutowire("Progress-Bar")] public Slider progressBar;
        [FuiAutowire("Progress-Bar-Label")] public TextMeshProUGUI progressText;
        [FuiAutowire("consume-time")] public TextMeshProUGUI timeText;
        [FuiAutowire("score-multiplier")] public TextMeshProUGUI multiplier;
        [FGameObject("no-grid-tip")] public GameObject noGridTipText;

        [FuiAutowire] private TextMeshProUGUI _currentLevel;

        // 图片阴影消散
        public ImageShadowFade imageShadowFade;
        [FGameObject("multi-step-particle")] public GameObject multiStepParticlePrefab;
        [FGameObject("single-kill")] public GameObject singleKillPrefab;

        [FuiAutowire("current-score")] public TextMeshProUGUI scoreText;
        private float _currentScore;
        public float CurrentScore => _currentScore;

        private Dictionary<Sprite, int> _idCache;

        public Stack<Grid> clickedGridStack;

        [FResourcesLoad("UI/Icons/obstacle")] private Sprite _obstacleIcon;
        [FResourcesLoad("VFX/SimpleBurst")] public GameObject iconCombineBurst;

        [FResourcesLoad("UI/Effect/amazing-icon")]
        public Sprite amazingSprite;

        [FResourcesLoad("UI/Effect/excellent-icon")]
        public Sprite excellentSprite;

        [FResourcesLoad("UI/Effect/good-icon")]
        public Sprite goodSprite;

        [FGameObject] public GameObject amazingIcon;


        [FGameObject("Color-Bg")] private GameObject _colorGamePanel;


        public GameObject sampleGameGrid;

        private List<Grid> _tipGrids;


        #region Auto Fit Parameters

        private int _col;
        private int _row;
        private int _total;
        [FuiAutowire("game-padding")] private RectTransform _renderRoot;
        [FuiAutowire] public Image shadowImage;

        private const float MIN_GRID_SIZE = 70f;
        private const float SPACING = 10f;

        #endregion

        private float _gridWidth, _gridHeight;
        private float _yOffset;

        private GridState[,] _gridStates;
        private BackgroundGird[,] _backgroundGrids;
        private List<BoomGrid> _boomGrids;
        public BackgroundGird[,] BackgroundGrids => _backgroundGrids;

        private Queue<GridState> _enableGridStates = new();
        private List<Grid> _gameGirds;
        public List<Grid> GameGrids => _gameGirds;
        public int initTotalGameGridCount;

        public int GameGirdCount => _gameGirds.Count;
        private Runtime _runtime;


        private Sprite _gridOriginSprite;
        private bool _isShuffling = false;
        private bool _isEnsuringValidMove = false;
        private bool _hasPendingTransform;
        private const int MAX_ENSURE_VALID_MOVE_ATTEMPTS = 8;
        private const float CLASSIC_VALID_PAIR_CHECK_DELAY = 0.15f;
        private long _ensureValidPairInvokeId = -1L;

        // 网格变换管理器（替代旧的旋转管理器）
        private GridTransformManager _transformManager;

        // 边界连锁消除管理器
        private BoundaryChainEliminator _boundaryEliminator;


        public override void OnSetup()
        {
            FAds.Instance.ShowBannerAd();
            _tipGrids = new List<Grid>();
            _boomSpawner = new BoomSpawner(boomItem, _renderRoot);
            _boomGrids = new List<BoomGrid>();
            _defaultGameAreaColor = new Color(0.9921569f, 0.8156863f, 0.6784314f, 1);
            _idCache = new Dictionary<Sprite, int>();
            clickedGridStack = new Stack<Grid>();
            _runtime = Runtime.Instance;

            _gameGirds = new List<Grid>();
            _gridOriginSprite = _gridItem.GetComponent<Image>().sprite;

            _gridStates = new GridState[_col, _row];
            _backgroundGrids = new BackgroundGird[_col, _row];

            imageShadowFade = new ImageShadowFade(shadowImage);

            // 初始化变换管理器和边界消除器
            _transformManager = new GridTransformManager(this);
            _boundaryEliminator = new BoundaryChainEliminator(this);

            StartLevel();
            
            
        }


        private void AdapterConfig(int level = 1)
        {
            level = Mathf.Max(1, level);
            _currentLevel.text = $"关卡 {level}";
            if (Runtime.mode == 1)
            {
                _currentLevel.text = "计时模式";
                level = 999;
            }

            _currentConfig = LevelConfigAsset.Instance.GetLevelConfig(level);
            RectTransformStretchUtil.SetStretchSize(gameArea, _currentConfig.panelWidth, _currentConfig.panelHeight);
        }

        public void NextLevel()
        {


            StartLevel();
        }

        private void UpdateTheme()
        {
            var theme = GameThemeAsset.Instance.themes.Random();
            var gameAreaImage = gameArea.GetComponent<Image>();
            gameAreaImage.color = theme.deepColor;
            _colorGamePanel.GetComponent<Image>().color = theme.shallowColor;


            // 更新背景 Grid
            var gridImage = _gridItem.transform.GetChild(0).GetComponent<Image>();
            gridImage.color = theme.deepColor / 1.05f;

            // 更新 Tile 颜色
            var tileImage = _colorGamePanel.transform.GetChild(0).GetComponent<Image>();
            var tileColor = theme.deepColor;
            // tileColor.a = 0.4f;
            tileImage.color = tileColor;
            _runtime.gridOriginalColor = gridImage.color;
        }

        private void StartLevel()
        {
            timeHangout.SetActive(Runtime.mode == 1);
            _boomSpawner.Reset();
            UpdateTheme();


            _prevComboIcon = null;
            gameArea.offsetMin = new Vector2(OFFSET_X_MIN, OFFSET_Y_MIN);
            gameArea.offsetMax = new Vector2(OFFSET_X_MAX, OFFSET_Y_MAX);
            gameIsEnded = false;
            // FIXME 上线前转换到网络存储
            var currentLevel = PlayerNetData.Instance.PlayerLevel;
            AdapterConfig(currentLevel);
            DoClean(cleanBgGird: true);
            var areaWidth = gameArea.rect.width;
            var areaHeight = gameArea.rect.height;

            // padding factor
            const float scaleFactor = 0.05f;
            _renderRoot.offsetMax = -new Vector2(areaWidth * scaleFactor, areaHeight * scaleFactor);
            _renderRoot.offsetMin = new Vector2(areaWidth * scaleFactor, areaHeight * scaleFactor);
            var rect = _renderRoot.rect;


            // 推导行列
            _col = Mathf.FloorToInt((rect.width + SPACING) / (MIN_GRID_SIZE + SPACING));
            _row = Mathf.FloorToInt((rect.height + SPACING) / (MIN_GRID_SIZE + SPACING));

            _col = Mathf.Max(1, _col);
            _row = Mathf.Max(1, _row);
            _total = _col * _row;


            // 计算实际使用的格子大小（保持方形）
            _gridWidth = (rect.width - (_col - 1) * SPACING) / _col;
            _gridHeight = (rect.height - (_row - 1) * SPACING) / _row;

            var min = Mathf.Min(_gridWidth, _gridHeight);
            _gridWidth = _gridHeight = min;

            _yOffset = (rect.height - _row * _gridHeight - (_row - 1) * SPACING) / 2f;

            // resize prefab
            _gridItem.GetComponent<RectTransform>().sizeDelta = new Vector2(_gridWidth, _gridHeight);
            (shadowImage.transform as RectTransform)!.sizeDelta = new Vector2(_gridWidth, _gridHeight);

            _gridStates = new GridState[_col, _row];
            _backgroundGrids = new BackgroundGird[_col, _row];

            RenderGrid();
            InitGridStates();
            InitObstacle();
            SpawnGameGrid();
            if (!_isFirstEnterGame)
            {
                Runtime.Instance.ResetData();
            }

            _isFirstEnterGame = false;

            // 设置变换管理器的关卡
            _transformManager?.SetLevel(currentLevel);

            if (Runtime.mode == 0)
            {
                EnsureAtLeastOneValidPair(() =>
                {
                    if (currentLevel == 1)
                    {
                        StartTutorialGuide();
                    }
                });
            }
            else if (currentLevel == 1)
            {
                StartTutorialGuide();
            }
            
            
        }

        #region Grid State

        private void InitGridStates()
        {
            List<GridState> list = new();

            for (int i = 0; i < _col; i++)
            {
                for (int j = 0; j < _row; j++)
                {
                    var s = new GridState { x = i, y = j, state = EGridState.Empty };
                    _gridStates[i, j] = s;
                    list.Add(s);
                }
            }

            list.Shuffle();
            _enableGridStates = new Queue<GridState>(list);
        }

        private void InitObstacle()
        {
            if (Runtime.mode == 1) return;
            // 重置所有背景格子
            for (var i = 0; i < _col; i++)
            {
                for (var j = 0; j < _row; j++)
                {
                    var bg = _backgroundGrids[i, j];
                    bg.FrontImage.sprite = _gridOriginSprite;
                    bg.FrontImage.color = _runtime.gridOriginalColor;
                }
            }

            var totalGrid = _col * _row;


            var minFactor = _currentConfig.minObstacle;
            var maxFactor = _currentConfig.maxObstacle;

            var minObstacle = Mathf.Max(0, (int)(totalGrid * minFactor));
            var maxObstacle = Mathf.Max(0, (int)(totalGrid * maxFactor));
            var obstacleCount = Random.Range(minObstacle, maxObstacle);

            for (var i = 0; i < obstacleCount; i++)
            {
                if (_enableGridStates.Count == 0) break;

                var grid = _enableGridStates.Dequeue();
                _gridStates[grid.x, grid.y].state = EGridState.Obstacle;

                var bg = _backgroundGrids[grid.x, grid.y];
                bg.FrontImage.color = Color.white;
                bg.FrontImage.sprite = _obstacleIcon;
            }
        }

        #endregion

        #region Background Grid

        private void RenderGrid()
        {
            for (var i = 0; i < _col; i++)
            {
                for (var j = 0; j < _row; j++)
                {
                    var item = Instantiate(_gridItem, _renderRoot);
                    SetupGrid(item, i, j);
                    item.SetActive(true);
                    _backgroundGrids[i, j] = new BackgroundGird(item, i, j);
                }
            }
        }

        private void SetupGrid(GameObject go, int x, int y)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(_gridWidth, _gridHeight);
            rt.anchoredPosition = new Vector2(GetXPosition(x), GetYPosition(y));
        }

        public float GetXPosition(int x)
        {
            return x * (_gridWidth + SPACING) + _gridWidth / 2;
        }

        public void UpdateGridState(int x, int y, EGridState newState)
        {
            _gridStates[x, y].state = newState;
        }

        public void RemoveGameGrid(Grid item, bool updateProgress = false)
        {
            // 检查边界连锁消除
            _boundaryEliminator?.CheckBoundaryChain(item);

            // 生成炸弹
            // _boomSpawner.SpawnBoom(item.x, item.y);
            _gridStates[item.x, item.y].state = EGridState.Empty;
            _enableGridStates.Enqueue(new GridState { x = item.x, y = item.y, state = EGridState.Empty });
            _gameGirds.Remove(item);
            Destroy(item.obj);
            if (updateProgress)
            {
                var percent = 1 - _gameGirds.Count / (float)initTotalGameGridCount;
                progressBar.DOValue(percent, 0.25f);
                progressText.text = $"{(int)(percent * 100)} / 100";
            }

            // 消除后触发变换（延迟0.3秒，等待消除动画完成）
            var willAttemptTransform = false;
            if (_transformManager != null && _transformManager.ShouldTransform())
            {
                willAttemptTransform = true;
                _hasPendingTransform = true;
                FAsync.Instance.InvokeDelay(() =>
                {
                    _hasPendingTransform = false;
                    var didStartTransform = _transformManager.TransformGridsAfterElimination();
                    if (Runtime.mode == 0 && !didStartTransform)
                    {
                        ScheduleEnsureValidPairAfterBoardChange();
                    }
                }, 0.3f);
            }

            if (Runtime.mode == 1)
            {
                // 计时模式
                // 统计一下面积，超过0.75才开始
                // var fullCount = _col * _row;
                if (_gameGirds.Count / (float)_total >= 0.75) return;
                // 开始生成，从已经生成的列表中随机一个，确保它可以被消除
                var collectedItem = Runtime.Instance.levelCollectItems.Random();
                SpawnOneGrid(collectedItem.Id, collectedItem.icon.sprite);
            }
            else
            {
                CheckIsWin();
            }

            if (Runtime.mode == 0 && !willAttemptTransform)
            {
                ScheduleEnsureValidPairAfterBoardChange();
            }
        }

        public float GetYPosition(int y)
        {
            return -y * (_gridHeight + SPACING) - _gridHeight / 2 - _yOffset;
        }

        #endregion

        #region Game Spawn

        private void SpawnGameGrid()
        {
            var src = new List<Sprite>();
            if (Runtime.mode == 0)
            {
                src.AddRange(Runtime.Instance.gameItems);
            }
            else if (Runtime.mode == 1)
            {
                var tmp = new List<SameSpriteItem>(Runtime.Instance.sameSprites);
                tmp.Shuffle();
                src.AddRange(tmp[0].sprites);
            }

            src.Shuffle();

            var count = _enableGridStates.Count;
            var blankAmountGroup = _currentConfig.blankGridGroupAmount;
            var maxLoop = Mathf.Max(Mathf.Min(Mathf.FloorToInt(count / 2f), count) - blankAmountGroup, 5);

            // 计算需要多少种不同的图标（尽可能使用更多种类）
            var totalIcons = Runtime.Instance.gameItems.Count;
            Debug.Log($"可用图标总数: {totalIcons}, 需要生成的对数: {maxLoop}");

            // 使用尽可能多的图标种类，最多使用所有可用图标
            var iconCount = Mathf.Min(maxLoop, totalIcons);
            var icons = src.GetRange(0, iconCount);

            Debug.Log($"实际使用图标数: {iconCount}");

            var id = 0;
            // 使用队列确保每种图标都被使用一次后再重复
            var iconQueue = new Queue<Sprite>(icons);

            while (id < maxLoop && iconQueue.Count > 0)
            {
                var icon = iconQueue.Dequeue();
                SpawnGridGroup(id, icon);
                id++;

                // 如果还需要更多对，将图标重新加入队列
                if (id < maxLoop && iconQueue.Count == 0)
                {
                    icons.Shuffle(); // 重新打乱顺序
                    iconQueue = new Queue<Sprite>(icons);
                }
            }

            initTotalGameGridCount = GameGirdCount;
        }

        public GridState GetGridState(int x, int y)
        {
            return _gridStates[x, y];
        }

        public GridState[,] GridStates => _gridStates;

        // 检查位置是否有效（用于旋转）
        public bool IsValidPosition(int x, int y)
        {
            if (x < 0 || x >= _col || y < 0 || y >= _row) return false;
            var state = _gridStates[x, y].state;
            return state == EGridState.Empty || state == EGridState.Busy;
        }

        // 设置输入锁定状态（用于旋转时禁止操作）
        public void SetInputLocked(bool locked)
        {
            _isInputLocked = locked;
            Debug.Log($"输入锁定状态: {locked}");
        }

        // 检查输入是否被锁定
        public bool IsInputLocked()
        {
            return _isInputLocked || _isShuffling || gameIsEnded;
        }

        /// <summary>
        /// 保障至少存在一对可消除格子（仅经典模式）
        /// </summary>
        public void EnsureAtLeastOneValidPair(Action onComplete = null)
        {
            CancelEnsureValidPairSchedule();
            if (Runtime.mode != 0 || _gameGirds.Count < 2)
            {
                SetInputLocked(false);
                onComplete?.Invoke();
                return;
            }

            if (_isEnsuringValidMove)
            {
                onComplete?.Invoke();
                return;
            }

            StartCoroutine(RoutineEnsureAtLeastOneValidPair(onComplete));
        }

        private IEnumerator RoutineEnsureAtLeastOneValidPair(Action onComplete = null)
        {
            _isEnsuringValidMove = true;
            SetInputLocked(true);

            var attempt = 0;
            while (!DeadlockDetector.HasValidMove(_gameGirds, _gridStates) && attempt < MAX_ENSURE_VALID_MOVE_ATTEMPTS)
            {
                attempt++;
                Debug.LogWarning($"未找到可消除对，执行自动洗牌尝试 {attempt}/{MAX_ENSURE_VALID_MOVE_ATTEMPTS}");
                AutoShuffle();

                while (_isShuffling)
                {
                    yield return null;
                }

                // 等一帧，确保状态刷新完成
                yield return null;
            }

            var hasValidMove = DeadlockDetector.HasValidMove(_gameGirds, _gridStates);
            Debug.Log($"可消除对保障结果: {hasValidMove}, 尝试次数: {attempt}");

            _isEnsuringValidMove = false;
            SetInputLocked(false);
            onComplete?.Invoke();
        }

        private void ScheduleEnsureValidPairAfterBoardChange()
        {
            if (Runtime.mode != 0 || gameIsEnded)
            {
                return;
            }

            CancelEnsureValidPairSchedule();
            _ensureValidPairInvokeId = FAsync.Instance.InvokeDelay(() =>
            {
                _ensureValidPairInvokeId = -1L;
                TryEnsureValidPairAfterBoardSettled();
            }, CLASSIC_VALID_PAIR_CHECK_DELAY);
        }

        private void CancelEnsureValidPairSchedule()
        {
            if (_ensureValidPairInvokeId == -1L)
            {
                return;
            }

            FAsync.Instance.StopRoutine(_ensureValidPairInvokeId);
            _ensureValidPairInvokeId = -1L;
        }

        private void TryEnsureValidPairAfterBoardSettled()
        {
            if (Runtime.mode != 0 || gameIsEnded || _gameGirds.Count < 2)
            {
                return;
            }

            if (!IsBoardSettledForValidPairCheck())
            {
                ScheduleEnsureValidPairAfterBoardChange();
                return;
            }

            EnsureAtLeastOneValidPair();
        }

        private bool IsBoardSettledForValidPairCheck()
        {
            if (_isShuffling || _isEnsuringValidMove)
            {
                return false;
            }

            if (_transformManager != null && _transformManager.IsTransforming())
            {
                return false;
            }

            if (_hasPendingTransform)
            {
                return false;
            }

            if (_boundaryEliminator != null && _boundaryEliminator.IsProcessing())
            {
                return false;
            }

            return !_gameGirds.Any(grid => grid != null && grid.IsBusy);
        }

        // 验证网格状态同步（调试用）
        public void ValidateGridStates()
        {
            var activeGrids = new List<Grid>(_gameGirds);
            var stateMap = new Dictionary<(int x, int y), bool>();

            // 记录所有活动格子的位置
            foreach (var grid in activeGrids)
            {
                stateMap[(grid.x, grid.y)] = true;
            }

            // 检查 GridState 是否匹配
            int mismatchCount = 0;
            for (int x = 0; x < _col; x++)
            {
                for (int y = 0; y < _row; y++)
                {
                    var state = _gridStates[x, y].state;
                    var hasGrid = stateMap.ContainsKey((x, y));

                    if (hasGrid && state != EGridState.Busy)
                    {
                        Debug.LogError($"状态不匹配: ({x},{y}) 有格子但状态是 {state}");
                        mismatchCount++;
                    }
                    else if (!hasGrid && state == EGridState.Busy)
                    {
                        Debug.LogError($"状态不匹配: ({x},{y}) 无格子但状态是 Busy");
                        mismatchCount++;
                    }
                }
            }

            if (mismatchCount == 0)
            {
                Debug.Log("网格状态同步正常");
            }
            else
            {
                Debug.LogError($"发现 {mismatchCount} 个状态不匹配");
            }
        }

        private void SpawnGridGroup(int id, Sprite icon)
        {
            for (int i = 0; i < 2; i++)
            {
                SpawnOneGrid(id, icon);
            }
        }

        private void SpawnOneGrid(int id, Sprite icon)
        {
            if (!_idCache.TryGetValue(icon, out int newId))
                _idCache.Add(icon, id);
            else
                id = newId;

            GetRandomCoord(out var coord);

            var go = Instantiate(_gameGrid, _renderRoot);
            var g = new Grid(icon, go, id, coord.x, coord.y);

            g.SetPosition(new Vector2(GetXPosition(coord.x), GetYPosition(coord.y)));
            g.SetGirdSize(new(_gridWidth, _gridHeight));
            if (sampleGameGrid == null)
            {
                sampleGameGrid = g.obj;
            }

            _gameGirds.Add(g);
        }

        private void GetRandomCoord(out Vector2Int coord)
        {
            var s = _enableGridStates.Dequeue();
            _gridStates[s.x, s.y].state = EGridState.Busy;
            coord = new Vector2Int(s.x, s.y);
        }

        #endregion

        #region Function

        [FOnClick("open-settings-btn")]
        [Preserve]
        public void OpenSettings()
        {
            FAudioCore.Instance.PlaySfx("tap");
            FViewManager.Instance.Show<GameSettingsView>();
            GameSettingsView.callback = Instance.RestartGame;
        }


        public bool IsAllSingle()
        {
            Dictionary<int, int> counts = new();
            foreach (var grid in _gameGirds)
            {
                if (counts.TryGetValue(grid.Id, out var count))
                    counts[grid.Id] = count + 1;
                else
                    counts.Add(grid.Id, 1);
            }

            return counts.Values.All(i => i == 1);
        }


        public void RestartGame()
        {
            
            UpdateTheme();
            DoClean();
            InitGridStates();
            InitObstacle();
            SpawnGameGrid();
            if (Runtime.mode == 0)
            {
                EnsureAtLeastOneValidPair();
            }
            Runtime.Instance.ResetData();
        }

        private void StartTutorialGuide()
        {
            // 教程
            FAsync.Instance.InvokeDelay(() =>
            {
                _openSettingsBtn.SetActive(false);
                var validPairs = DeadlockDetector.GetValidPairs(_gameGirds, _gridStates);
                if (validPairs.Count == 0)
                {
                    Debug.LogWarning("教程启动时未找到可消除对，跳过教程引导");
                    return;
                }

                var first = validPairs[0].Item1;
                var second = validPairs[0].Item2;

                FViewManager.Instance.Show<AlertTipView>(true, 2);
                var ui = FUiHighlighter.HighlightUi(first.obj, () =>
                {
                    first.OnClicked();
                    FAsync.Instance.InvokeDelay(() =>
                    {
                        var nextUi = FUiHighlighter.HighlightUi(second.obj, () =>
                        {
                            second.OnClicked();
                            AlertTipView.Instance.FadeIn("现在试着消除另外的元素吧！");
                        });
                        AlertTipView.Instance.FadeIn("现在选择另一个", 20f);
                        nextUi?.transform.DOShakeScale(1.2f, 0.5f);
                    }, 0.5f);
                });
                // 晃一下
                ui?.transform.DOShakeScale(1.2f, 0.5f);
                AlertTipView.Instance.FadeIn("选中高亮元素", 20f);
            }, 0.5f);
        }

        private void DoClean(bool cleanBgGird = false)
        {
            CancelEnsureValidPairSchedule();
            if (clickedGridStack.Count > 0)
            {
                clickedGridStack.Pop().ResetState();
                clickedGridStack.Clear();
            }

            _gameGirds.ForEach(i => Destroy(i.obj));
            _gameGirds.Clear();

            _idCache.Clear();
            _enableGridStates.Clear();
            _gridStates = new GridState[_col, _row];

            progressBar.value = 0;
            progressText.text = "0 / 100";
            multiplier.text = "x1";
            multiplier.color = Color.clear;
            timeText.text = "00:00";
            _currentScore = 0;
            scoreText.text = $"分数：{Mathf.CeilToInt(_currentScore)}";
            _isShuffling = false;
            _isEnsuringValidMove = false;
            _hasPendingTransform = false;

            if (cleanBgGird)
            {
                for (var i = 0; i < _col; i++)
                {
                    for (var j = 0; j < _row; j++)
                    {
                        if (_backgroundGrids[i, j] == null) continue;
                        Destroy(_backgroundGrids[i, j].UnityObj);
                    }
                }
            }
        }

        // 刷新格子
        [FOnClick("refresh-grid-btn")]
        public void RefreshGrid()
        {
            if (_isShuffling) return;

            FAds.Instance.Video(success =>
            {
                if (!success) return;
                if (clickedGridStack.Count > 0)
                {
                    clickedGridStack.Pop().ResetState();
                    clickedGridStack.Clear();
                }

                var activeGrids = new List<Grid>();
                var allValidSlots = new List<Vector2Int>();

                foreach (var grid in _gameGirds)
                {
                    if (grid != null && grid.IsAlive && grid.obj.activeSelf)
                    {
                        activeGrids.Add(grid);
                    }
                }

                if (activeGrids.Count == 0) return;


                for (int x = 0; x < _col; x++)
                {
                    for (int y = 0; y < _row; y++)
                    {
                        if (!_gridStates[x, y].IsObstacle())
                        {
                            allValidSlots.Add(new Vector2Int(x, y));
                        }
                    }
                }

                if (allValidSlots.Count < activeGrids.Count)
                {
                    Debug.LogError("Error: Not enough slots for grids!");
                    return;
                }

                allValidSlots.Shuffle();

                StartCoroutine(RoutineShuffle(activeGrids, allValidSlots));
            });

        }

        /// <summary>
        /// 自动洗牌（不需要看广告，用于死局自动恢复）
        /// </summary>
        public void AutoShuffle()
        {
            if (_isShuffling) return;

            Debug.Log("执行自动洗牌");

            if (clickedGridStack.Count > 0)
            {
                clickedGridStack.Pop().ResetState();
                clickedGridStack.Clear();
            }

            var activeGrids = new List<Grid>();
            var allValidSlots = new List<Vector2Int>();

            foreach (var grid in _gameGirds)
            {
                if (grid != null && grid.IsAlive && grid.obj.activeSelf)
                {
                    activeGrids.Add(grid);
                }
            }

            if (activeGrids.Count == 0) return;

            for (int x = 0; x < _col; x++)
            {
                for (int y = 0; y < _row; y++)
                {
                    if (!_gridStates[x, y].IsObstacle())
                    {
                        allValidSlots.Add(new Vector2Int(x, y));
                    }
                }
            }

            if (allValidSlots.Count < activeGrids.Count)
            {
                Debug.LogError("Error: Not enough slots for grids!");
                return;
            }

            allValidSlots.Shuffle();

            StartCoroutine(RoutineShuffle(activeGrids, allValidSlots));
        }

        private IEnumerator RoutineShuffle(List<Grid> grids, List<Vector2Int> shuffledSlots)
        {
            _isShuffling = true;
            // FAudioCore.Instance.PlaySfx("shuffle");


            for (int x = 0; x < _col; x++)
            {
                for (int y = 0; y < _row; y++)
                {
                    if (!_gridStates[x, y].IsObstacle())
                    {
                        UpdateGridState(x, y, EGridState.Empty);
                    }
                }
            }

            var runtime = Runtime.Instance;
            var moveDuration = 0.5f;

            // 我们只需要取打乱后的坑位列表的前 N 个（N = 格子数量）
            for (int i = 0; i < grids.Count; i++)
            {
                var grid = grids[i];
                if (grid == null || !grid.IsAlive) continue;
                var targetPosData = shuffledSlots[i]; // 从洗好的牌里拿一个新的坐标

                // 更新 Grid 对象的逻辑坐标
                grid.x = targetPosData.x;
                grid.y = targetPosData.y;


                // 计算物理坐标
                var targetLocalPos = new Vector2(
                    GetXPosition(grid.x),
                    GetYPosition(grid.y)
                );
                var gridRect = grid.Rt;
                var tick = 0f;
                // 播放动画
                gridRect
                    .DOAnchorPos(targetLocalPos, moveDuration)
                    .SetEase(Ease.OutBack)
                    .SetLink(grid.obj)
                    .OnUpdate(() =>
                    {
                        if (grid == null || !grid.IsAlive || grid.obj == null) return;
                        tick -= Time.deltaTime;
                        if (tick < 0f)
                        {
                            imageShadowFade.AddImageToFadeList(grid.obj.transform.parent,
                                gridRect.anchoredPosition, grid.Icon);
                            tick = 0.05f;
                        }
                    });
            }

            yield return new WaitForSeconds(moveDuration);

            // 动画结束，最终确认状态
            foreach (var grid in grids)
            {
                if (grid == null || !grid.IsAlive) continue;
                grid.SetPosition(new Vector2(GetXPosition(grid.x), GetYPosition(grid.y)));

                // 标记占位
                UpdateGridState(grid.x, grid.y, EGridState.Busy);

                grid.ResetState();
            }

            _isShuffling = false;
        }


        // 提示路径

        private long _tipInvokeId = -1L;

        [FOnClick("get-target-tip")]
        [Preserve]
        public void ShowGridPathTip()
        {
            if (_isShuffling) return;
            // 随机找一对可以连起来的
            var res = QuickOneNet.ShowGridPathTip(_gameGirds, _col, _row);
            if (res == null)
            {
                noGridTipText.SetActive(true);
                noGridTipText.GetComponent<FTweenScale>().Replay();
                noGridTipText.GetComponent<FTweenUIOpacity>().Replay();
                if (_tipInvokeId != -1)
                {
                    FAsync.Instance.StopRoutine(_tipInvokeId);
                    _tipInvokeId = -1;
                }

                _tipInvokeId = FAsync.Instance.InvokeDelay(() =>
                {
                    noGridTipText.GetComponent<FTweenScale>().DoSmoothRewind();
                    noGridTipText.GetComponent<FTweenUIOpacity>().DoSmoothRewind();
                }, 3f);
            }
            else
            {
                if (_tipGrids.Count > 0)
                {
                    _tipGrids.ForEach(i => i.ResetState());
                    _tipGrids.Clear();
                }

                res.ForEach(i => i.Highlight());
                _tipGrids.AddRange(res);
            }
        }

        #endregion

        #region Score

        public void AddScore(float score)
        {
            _currentScore += score;
            scoreText.text = $"分数：{Mathf.CeilToInt(_currentScore).ToThousands()}";
        }

        #endregion

        #region Game Helper
        

        private int EmptyCount()
        {
            var cnt = 0;
            for (int i = 0; i < _row; i++)
            {
                for (var j = 0; j < _col; j++)
                {
                    if (_gridStates[i, j].state == EGridState.Empty)
                    {
                        cnt++;
                    }
                }
            }

            return cnt;
        }

        public Vector2 GetCellSize()
        {
            return new Vector2(
                _gridWidth,
                _gridHeight
            );
        }

        public IEnumerator KillAllSingle()
        {
            var items = new List<Grid>(_gameGirds);
            var targetCollectinItems = new List<RuntimeCollectionItem>();
            foreach (var item in Runtime.Instance.levelCollectItems)
            {
                if (_gameGirds.Exists(i => i.IconId == item.Id))
                {
                    targetCollectinItems.Add(item);
                }
            }

            foreach (var p in targetCollectinItems)
            {
                var targetGrids = items.FindAll(i => i.IconId == p.Id);
                items.RemoveAll(i => i.Id == p.Id);
                foreach (var item in targetGrids)
                {
                    var ins = Instantiate(singleKillPrefab, p.transform, false);
                    ins.gameObject.SetActive(true);
                    ins.transform.localPosition = Vector3.zero;
                    ins.transform.localRotation = Quaternion.identity;
                    var killTarget = item.Rt;
                    // 世界坐标
                    var startWorld = ins.transform.position;
                    // var targetWorld = killTarget.position;

                    // 把 target world 转到 ins 的 parent 的 local 坐标
                    var startPos = ins.transform.parent.InverseTransformPoint(startWorld);
                    var targetPos = FMath.TransLocalPositionToTarget(ins.transform, killTarget);

                    // 控制点计算
                    var ab = targetPos - startPos;
                    var mid = (startPos + targetPos) * 0.5f;
                    var dir = Vector3.Cross(ab, Vector3.forward).normalized;
                    var control = mid + dir * 300;

                    DOVirtual.Float(0, 1, 0.5f, t =>
                    {
                        var pos = FMath.Bezier(startPos, control, targetPos, t);
                        ins.transform.localPosition = pos;
                    }).OnComplete(() =>
                    {
                        Destroy(ins);
                        item.DoKill();
                    });
                    yield return new WaitForSeconds(0.5f);
                }
            }
        }

        private long _comboIconInvokeId = -1L;
        private Sprite _prevComboIcon;

        public void ShowComboIcon(int combo)
        {
            var img = amazingIcon.GetComponent<Image>();
            img.sprite = combo switch
            {
                3 or 4 or 5 or 6 => goodSprite,
                7 or 8 or 9 => amazingSprite,
                10 => excellentSprite,
                _ => null
            };
            if (img.sprite == null || _prevComboIcon == img.sprite) return;
            _prevComboIcon = img.sprite;

            var tweenScale = amazingIcon.GetComponent<FTweenScale>();
            var tweenFade = amazingIcon.GetComponent<FTweenUIImageOpacity>();
            amazingIcon.gameObject.SetActive(true);

            if (_comboIconInvokeId != -1L)
            {
                FAsync.Instance.StopRoutine(_comboIconInvokeId);
            }

            if (tweenFade.IsRunning)
            {
                tweenFade.Rewind();
            }

            tweenFade.Replay();
            if (tweenScale.IsRunning)
            {
                tweenScale.Rewind();
            }

            tweenScale.Replay();
            // _comboIconInvokeId = FCoroutineStarter.Instance.InvokeDelay(() =>
            // {
            //     if (tweenFade.IsRunning)
            //     {
            //         tweenFade.Rewind();
            //     }
            //     tweenFade.Replay();
            //     _comboIconInvokeId = -1L;
            // }, tweenScale.duration);
        }

        private void CheckIsWin()
        {
            if (gameIsEnded) return;
            if (_gameGirds.Count > 0) return;
            FAsync.Instance.InvokeDelay(() =>
            {
                gameIsEnded = true;
                FViewManager.Instance.Show<GameWinView>();
                _openSettingsBtn.SetActive(true);
            }, 1f);
        }

        public bool IsValidCoordinate(int x, int y)
        {
            return x >= 0 && x < _col && y >= 0 && y < _row;
        }

        public Grid GetGridInstance(int x, int y)
        {
            if (!IsValidCoordinate(x, y)) return null;
            return _gameGirds.Find(i => i.x == x && i.y == y);
        }

        #endregion


        private void Update()
        {
            imageShadowFade.Update();
        }
    }
}
