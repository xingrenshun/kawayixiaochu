using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using FCSharpExt;
using FTools.Helpers;
using FTools.Platforms;
using FUnityAttribute.RuntimeAttributes;
using FUnityCommon;
using FUnityUI;
using GameGrid;
using SObjects;
using TMPro;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace View
{
    [FViewLoader("View/MemoryGameView")]
    public class MemoryGameView : FUiBase<MemoryGameView>
    {
        [FuiAutowire] private RectTransform _staminaRenderRoot;

        [FuiAutowire] public RectTransform gameArea;

        [FuiAutowire("game-padding")] private RectTransform _renderRoot;

        [FGameObject] private GameObject _gridItem;
        [FGameObject("video-flip")] private GameObject _videoFlip;
        [FGameObject("video-precision")] private GameObject _videoPrecision;
        [FGameObject("flip-all-btn")] private GameObject _flipAllBtn;
        [FGameObject("precision-lock-btn")] private GameObject _precisionLockBtn;
        [FuiAutowire("flip-amount-tip")] private TMP_Text _flipAmountTip;
        [FuiAutowire("precision-amount-tip")] private TMP_Text _precisionAmountTip;

        [FuiAutowire] private TMP_Text _readyText;

        [FGameObject] private GameObject _staminaPrefab;

        [FGameObject("Color-Bg")] private GameObject _colorGamePanel;

        private GameStaminaProcessor _staminaProcessor;

        private int _col;
        private int _row;
        private int _total;
        private int _remainingPairs;
        private bool _isInputLocked;
        private bool _gameEnded;
        
        // 计时器
        [FGameObject("time-hangout")] private GameObject _timeHangout;
        [FuiAutowire("consume-time")] private TMP_Text _consumeTime;
        [FuiAutowire("lock-remaining")] private Slider _lockRemaining;
        private float _roundElapsedTime;
        private bool _isRoundTiming;

        // 道具系统
        private FlipAllPowerUp _flipAllPowerUp;
        private PrecisionLockPowerUp _precisionLockPowerUp;
        private bool _isPrecisionLockActive;

        private const float SPACING = 10f;
        private const float MIN_GRID_SIZE = 70f;
        private const int INIT_STAMINA = 5;
        private const float MISMATCH_REVEAL_DELAY = 0.5f;
        private const float START_ENTRY_DELAY_INCREMENT = 0.012f;
        private const float START_ENTRY_SETTLE_TIME = 0.7f;
        private const int START_COUNTDOWN_SECONDS = 3;
        private const float START_FLIP_RANDOM_MAX_DELAY = 0.12f;
        private const float START_UNLOCK_AFTER_FLIP = 0.2f;
        private const float MEMORY_TUTORIAL_STEP_INTERVAL = 1.25f;
        private const float MEMORY_TUTORIAL_AUTO_ADVANCE_DELAY = 5f;
        private static readonly Vector2 MEMORY_TUTORIAL_TIP_POSITION = new Vector2(0f, -220f);
        private static readonly Vector2 MEMORY_TUTORIAL_STAMINA_TIP_POSITION = new Vector2(0f, -340f);
        private static readonly Vector2 MEMORY_TUTORIAL_TIMER_TIP_POSITION = MEMORY_TUTORIAL_STAMINA_TIP_POSITION;
        private const int ROUND_START_POWER_UP_AMOUNT = 1;
        private const int DEFAULT_OPENED_MIN_COUNT_FALLBACK = 5;
        private const int DEFAULT_OPENED_MAX_COUNT_FALLBACK = 12;

        private float _gridSize;
        private float _xOffset;
        private float _yOffset;
        [FuiAutowire("item-spawner")] public RectTransform spawnerTransform;

        private MGrid[][] _gridItems;
        private readonly HashSet<MGrid> _defaultOpenedGrids = new HashSet<MGrid>();
        private CanvasGroup _staminaCanvasGroup;
        private CanvasGroup _lockRemainingCanvasGroup;
        private Tween _lockRemainingTween;
        private float _staminaOriginY;
        private bool _isTutorialRunning;
        private int _roundFlipAllItemAmount;
        private int _roundPrecisionLockItemAmount;
        private GameObject _currentTutorialHighlight;

        public List<MGrid> selectedGrids = new List<MGrid>();

        public Sprite cardBack;

        public override void OnSetup()
        {
            FAds.Instance.ShowBannerAd();
            _staminaProcessor = new GameStaminaProcessor(_staminaRenderRoot, _staminaPrefab);
            InitPowerUpUi();
            InitPowerUps();
            InitGridLayout();
            StartGame();
        }

        private void InitPowerUpUi()
        {
            _staminaCanvasGroup = GetOrCreateCanvasGroup(_staminaRenderRoot);
            if (_staminaRenderRoot != null)
            {
                _staminaOriginY = _staminaRenderRoot.anchoredPosition.y;
            }
            if (_lockRemaining != null)
            {
                _lockRemainingCanvasGroup = GetOrCreateCanvasGroup(_lockRemaining.transform);
                _lockRemaining.gameObject.SetActive(false);
                _lockRemainingCanvasGroup.alpha = 0f;
                _lockRemaining.value = 1f;
            }
        }

        private void InitPowerUps()
        {
            _flipAllPowerUp = new FlipAllPowerUp();
            _precisionLockPowerUp = new PrecisionLockPowerUp();
            _isPrecisionLockActive = false;
        }

        private void Update()
        {
            _flipAllPowerUp?.Update();
            _precisionLockPowerUp?.Update();
            if (_isRoundTiming && !_gameEnded)
            {
                _roundElapsedTime += Time.deltaTime;
                UpdateConsumeTimeText();
            }
        }

        public void RestartGame()
        {
            DoClean();
            InitGridLayout();
            StartGame();
        }

        private void DoClean()
        {
            ClearTutorialHighlight();
            selectedGrids.Clear();
            foreach (var item in _defaultOpenedGrids)
            {
                if (item != null && item.IsAlive())
                {
                    item.SetLockedOpened(false);
                }
            }
            _defaultOpenedGrids.Clear();
            _remainingPairs = 0;
            _isInputLocked = true;
            _gameEnded = false;
            _isTutorialRunning = false;
            StopPrecisionLockDurationUi(true);
            if (_gridItems == null) return;
            for (int i = _gridItems.Length - 1; i >= 0; i--)
            {
                for (int j = _gridItems[i].Length - 1; j >= 0; j--)
                {
                    _gridItems[i][j] = null;
                }
            }
        }

        public bool TryBeginTap(MGrid grid)
        {
            if (_gameEnded || _isInputLocked) return false;
            if (selectedGrids.Count >= 2 && !selectedGrids.Contains(grid)) return false;
            if (selectedGrids.Count == 1 && !selectedGrids.Contains(grid))
            {
                // Second card selected: lock input until match is resolved.
                _isInputLocked = true;
            }
            return true;
        }

        public void DoSelected(MGrid grid)
        {
            if (_gameEnded) return;
            if (grid == null || !grid.IsAlive())
            {
                return;
            }
            if (selectedGrids.Contains(grid))
            {
                // 移除
                selectedGrids.Remove(grid);
                return;
            }
            // 追加一个新的
            selectedGrids.Add(grid);

            if (selectedGrids.Count == 1 && TryResolveDefaultOpenedAutoMatch(grid))
            {
                return;
            }

            // 如果精准锁定激活，触发自动匹配
            if (_isPrecisionLockActive && selectedGrids.Count == 1)
            {
                _precisionLockPowerUp?.OnPlayerFlipCard(grid);
            }

            if (selectedGrids.Count == 1) return;
            if (selectedGrids.Count > 2)
            {
                selectedGrids.Remove(grid);
                return;
            }

            var first = selectedGrids[0];
            var second = selectedGrids[1];
            if (first.id == second.id)
            {
                ResolveMatchedGrids(new List<MGrid>(selectedGrids));
                return;
            }

            var mismatch = new List<MGrid>(selectedGrids);
            FAsync.Instance.InvokeDelay(() =>
            {
                // 错误匹配时的震动效果
                FAudioCore.Instance.PlaySfx("error");
                foreach (var item in mismatch)
                {
                    if (item == null || !item.IsAlive()) continue;
                    var rt = item.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.DOShakePosition(0.3f, 10f, 20, 90, false, true);
                    }
                }

                var pending = 0;
                void FinishMismatch()
                {
                    selectedGrids.Clear();
                    var staminaLeft = _staminaProcessor.Consume(1);
                    if (staminaLeft <= 0)
                    {
                        EndGame(false);
                        return;
                    }
                    _isInputLocked = false;
                }

                foreach (var item in mismatch)
                {
                    if (item == null || !item.IsAlive()) continue;
                    if (item.IsLockedOpened()) continue;
                    if (item.IsFlipped()) continue;

                    pending++;
                    item.FlipCard(cardBack, () =>
                    {
                        pending--;
                        if (pending > 0) return;
                        FinishMismatch();
                    });
                }

                if (pending <= 0)
                {
                    FinishMismatch();
                }
            }, MISMATCH_REVEAL_DELAY);
        }

        private bool TryResolveDefaultOpenedAutoMatch(MGrid grid)
        {
            if (grid == null || !grid.IsAlive()) return false;
            if (grid.IsLockedOpened()) return false;

            var lockedOpened = GetLockedOpenedById(grid.id, grid);
            if (lockedOpened == null) return false;

            _isInputLocked = true;
            selectedGrids.Clear();
            ResolveMatchedGrids(new List<MGrid> { lockedOpened, grid });
            return true;
        }

        private MGrid GetLockedOpenedById(int id, MGrid exclude)
        {
            var toRemove = new List<MGrid>();
            MGrid result = null;
            foreach (var grid in _defaultOpenedGrids)
            {
                if (grid == null || !grid.IsAlive() || !grid.IsLockedOpened())
                {
                    toRemove.Add(grid);
                    continue;
                }

                if (grid == exclude || grid.id != id) continue;
                result = grid;
                break;
            }

            foreach (var item in toRemove)
            {
                _defaultOpenedGrids.Remove(item);
            }

            return result;
        }

        private void ResolveMatchedGrids(List<MGrid> matched)
        {
            if (matched.IsNullOrEmpty())
            {
                _isInputLocked = false;
                return;
            }

            foreach (var item in matched)
            {
                if (item == null || !item.IsAlive()) continue;
                RemoveDefaultOpenedGrid(item);
                var rt = item.GetComponent<RectTransform>();
                if (rt == null) continue;
                var seq = DOTween.Sequence();
                seq.Append(rt.DOScale(1.15f, 0.1f).SetEase(Ease.OutQuad));
                seq.Append(rt.DOScale(1f, 0.05f).SetEase(Ease.InQuad));
            }

            FAsync.Instance.InvokeDelay(() =>
            {
                if (_gameEnded) return;
                DoDestroyMGrid(matched);
                selectedGrids.Clear();
                _remainingPairs = Mathf.Max(0, _remainingPairs - 1);
                if (_remainingPairs <= 0)
                {
                    EndGame(true);
                    return;
                }
                _isInputLocked = false;
            }, 0.15f);
        }

        private void RemoveDefaultOpenedGrid(MGrid grid)
        {
            if (grid == null) return;
            grid.SetLockedOpened(false);
            _defaultOpenedGrids.Remove(grid);
        }

        private void DoDestroyMGrid(List<MGrid> grids)
        {
            if (grids.IsNullOrEmpty()) return;

            // 播放音效
            FAudioCore.Instance.PlaySfx("on-collect");

            // 消除动画
            foreach (var item in grids)
            {
                RemoveDefaultOpenedGrid(item);
                item.Destroy();
                _gridItems[item.x][item.y] = null;
            }
        }

        private void InitGridLayout()
        {
            var areaWidth = gameArea.rect.width;
            var areaHeight = gameArea.rect.height;

            const float scaleFactor = 0.05f;

            _renderRoot.offsetMax = -new Vector2(areaWidth * scaleFactor, areaHeight * scaleFactor);
            _renderRoot.offsetMin = new Vector2(areaWidth * scaleFactor, areaHeight * scaleFactor);

            var rect = _renderRoot.rect;

            _col = Mathf.FloorToInt((rect.width + SPACING) / (MIN_GRID_SIZE + SPACING));
            _row = Mathf.FloorToInt((rect.height + SPACING) / (MIN_GRID_SIZE + SPACING));

            _col = Mathf.Max(1, _col);
            _row = Mathf.Max(1, _row);

            // 保证总格子为偶数
            _total = _col * _row;
            if (_total % 2 != 0)
            {
                _row = Mathf.Max(1, _row - 1);
                _total = _col * _row;
            }

            _gridItems = new MGrid[_col][];
            for (int i = 0; i < _col; i++)
                _gridItems[i] = new MGrid[_row];

            float gridWidth = (rect.width - (_col - 1) * SPACING) / _col;
            float gridHeight = (rect.height - (_row - 1) * SPACING) / _row;

            _gridSize = Mathf.Min(gridWidth, gridHeight);

            _xOffset = (rect.width - _col * _gridSize - (_col - 1) * SPACING) / 2f;
            _yOffset = (rect.height - _row * _gridSize - (_row - 1) * SPACING) / 2f;

            _gridItem.GetComponent<RectTransform>().sizeDelta = new Vector2(_gridSize, _gridSize);
        }

        private void UpdateTheme()
        {
            var theme = GameThemeAsset.Instance.themes.Random();
            cardBack = theme.cardBack;
            var gameAreaImage = gameArea.GetComponent<Image>();
            gameAreaImage.color = theme.deepColor;
            _colorGamePanel.GetComponent<Image>().color = theme.shallowColor;


            // 更新背景 Grid
            var gridImage = _gridItem.GetComponent<Image>();
            gridImage.color = theme.deepColor / 1.05f;

            // 更新 Tile 颜色
            var tileImage = _colorGamePanel.transform.GetChild(0).GetComponent<Image>();
            var tileColor = theme.deepColor;
            // tileColor.a = 0.4f;
            tileImage.color = tileColor;
        }

        public void StartGame()
        {
            _gameEnded = false;
            _isInputLocked = true;
            selectedGrids.Clear();
            StopPrecisionLockDurationUi(true);
            ShowStaminaUi(true);
            ResetRoundTimer();
            UpdateTheme();
            _staminaProcessor.InitStamina(INIT_STAMINA);
            ResetRoundPowerUpInventory();
            if (_gridItems == null)
            {
                InitGridLayout();
            }
            ClearOldGrid();
            RenderGrid();
        }

        private void ClearOldGrid()
        {
            for (int i = _renderRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(_renderRoot.GetChild(i).gameObject);
            }
        }

        private void RenderGrid()
        {
            var pairCount = _total / 2;
            _remainingPairs = pairCount;

            var allIcons = GetMemoryModeIconPool();
            if (allIcons.Count == 0)
            {
                Debug.LogError("记忆模式没有可用图标，无法生成卡牌");
                return;
            }
            allIcons.Shuffle();

            var finalIcons = new List<Sprite>();

            for (int i = 0; i < pairCount; i++)
            {
                var icon = allIcons[i % allIcons.Count];
                finalIcons.Add(icon);
                finalIcons.Add(icon);
            }

            finalIcons.Shuffle();

            var index = 0;
            for (var x = 0; x < _col; x++)
            {
                for (var y = 0; y < _row; y++)
                {
                    var sprite = finalIcons[index++];
                    var id = Runtime.TryGetIconId(sprite, out var parsedId) ? parsedId : sprite.GetInstanceID();
                    var grid = new MGrid(x, y, id, _gridItem, _renderRoot);
                    grid.Render(sprite);
                    _gridItems[x][y] = grid;
                }
            }

            FAsync.Instance.StartRoutine(StartFadeIn());
        }

        private List<Sprite> GetMemoryModeIconPool()
        {
            var runtime = Runtime.Instance;
            if (runtime == null) return new List<Sprite>();
            var source = runtime.gameItems;
            if (source == null || source.Count == 0) return new List<Sprite>();

            var mainIcons = new List<Sprite>();
            foreach (var icon in source)
            {
                if (runtime.IsMemoryModeIcon(icon))
                {
                    mainIcons.Add(icon);
                }
            }

            // 兜底：若配置变更导致主图标池为空/过小，回退原始图标池
            if (mainIcons.Count >= 2) return mainIcons;
            return new List<Sprite>(source);
        }

        private IEnumerator StartFadeIn()
        {
            _isInputLocked = true;
            // 把所有的item分为多个组，按照组别来归位
            var allItems = new List<MGrid>();
            for (int i = 0; i < _total; i++)
            {
                var x = i % _col;
                var y = i / _col;
                var grid = _gridItems[x][y];
                allItems.Add(grid);
            }

            // 从中心向外扩散的入场效果
            var centerX = _col / 2f;
            var centerY = _row / 2f;

            // 按距离中心的距离排序
            allItems.Sort((a, b) =>
            {
                var distA = Mathf.Sqrt(Mathf.Pow(a.x - centerX, 2) + Mathf.Pow(a.y - centerY, 2));
                var distB = Mathf.Sqrt(Mathf.Pow(b.x - centerX, 2) + Mathf.Pow(b.y - centerY, 2));
                return distA.CompareTo(distB);
            });

            var maxDelay = 0f;
            var delayIncrement = START_ENTRY_DELAY_INCREMENT;

            for (int i = 0; i < allItems.Count; i++)
            {
                var g = allItems[i];
                var delay = i * delayIncrement;
                maxDelay = Mathf.Max(maxDelay, delay);
                FAsync.Instance.InvokeDelay(g.ToTargetPos, delay);
            }

            yield return new WaitForSeconds(maxDelay + START_ENTRY_SETTLE_TIME);
            if (_timeHangout != null)
            {
                _timeHangout.SetActive(true);
            }
            yield return StartCoroutine(PlayCountdown(START_COUNTDOWN_SECONDS, () =>
            {
                Debug.Log("倒计时结束");
                // 开始翻转全部卡片
                var flipDelay = 0f;
                foreach (var g in allItems)
                {
                    var delay = Random.Range(0f, START_FLIP_RANDOM_MAX_DELAY);
                    flipDelay = Mathf.Max(flipDelay, delay);
                    FAsync.Instance.InvokeDelay(() =>
                    {
                        g.FlipCard(cardBack);
                    }, delay);
                }

                FAsync.Instance.InvokeDelay(() =>
                {
                    if (_gameEnded) return;
                    SetupDefaultOpenedGrids(() =>
                    {
                        if (_gameEnded) return;
                        if (ShouldPlayMemoryTutorial())
                        {
                            StartMemoryTutorialGuide();
                            return;
                        }

                        BeginMemoryRound();
                    });
                }, flipDelay + START_UNLOCK_AFTER_FLIP);
            }));
        }

        private bool ShouldPlayMemoryTutorial()
        {
            return !_isTutorialRunning && !PlayerLocalData.Instance.MemoryTutorialCompleted;
        }

        private void BeginMemoryRound()
        {
            if (_gameEnded) return;
            ClearTutorialHighlight();
            _isTutorialRunning = false;
            _isInputLocked = false;
            StartRoundTimer();
        }

        private void StartMemoryTutorialGuide()
        {
            if (_isTutorialRunning) return;
            _isTutorialRunning = true;
            _isInputLocked = true;

            FAsync.Instance.InvokeDelay(() =>
            {
                if (_gameEnded) return;

                var demoPair = GetTutorialDemoPair();
                if (demoPair.opened == null || demoPair.hidden == null)
                {
                    ContinueMemoryTutorialAfterDemo();
                    return;
                }

                var openedStepDone = false;
                void ProceedOpenedStep()
                {
                    if (openedStepDone) return;
                    openedStepDone = true;
                    if (_gameEnded || !demoPair.opened.IsAlive()) return;

                    TriggerTutorialGridTap(demoPair.opened);
                    AlertTipView.ShowTopMessage(FormatTutorialStepMessage("这些默认翻开的牌会保持正面，找到它对应的另一张就会直接配对。"), 20f, MEMORY_TUTORIAL_TIP_POSITION);

                    FAsync.Instance.InvokeDelay(() =>
                    {
                        if (_gameEnded || !demoPair.hidden.IsAlive()) return;

                        var hiddenStepDone = false;
                        void ProceedHiddenStep()
                        {
                            if (hiddenStepDone) return;
                            hiddenStepDone = true;
                            if (_gameEnded || !demoPair.hidden.IsAlive()) return;
                            TriggerTutorialGridTap(demoPair.hidden);
                            FAsync.Instance.InvokeDelay(ContinueMemoryTutorialAfterDemo, 0.8f);
                        }

                        var hiddenUi = CreateTutorialHighlight(demoPair.hidden.UnityObj, ProceedHiddenStep);
                        AlertTipView.ShowTopMessage(FormatTutorialStepMessage("记住它的位置，再翻开另一张相同图案的卡牌。"), 20f, MEMORY_TUTORIAL_TIP_POSITION);
                        hiddenUi?.transform.DOShakeScale(1.2f, 0.5f);
                        FAsync.Instance.InvokeDelay(ProceedHiddenStep, MEMORY_TUTORIAL_AUTO_ADVANCE_DELAY);
                    }, MEMORY_TUTORIAL_STEP_INTERVAL);
                }

                var openedUi = CreateTutorialHighlight(demoPair.opened.UnityObj, ProceedOpenedStep);
                AlertTipView.ShowTopMessage(FormatTutorialStepMessage("记忆牌模式里，翻到两张相同图案的卡牌，就能完成配对消除。"), 20f, MEMORY_TUTORIAL_TIP_POSITION);
                openedUi?.transform.DOShakeScale(1.2f, 0.5f);
                FAsync.Instance.InvokeDelay(ProceedOpenedStep, MEMORY_TUTORIAL_AUTO_ADVANCE_DELAY);
            }, 0.6f);
        }

        private void ContinueMemoryTutorialAfterDemo()
        {
            HighlightTutorialTarget(_staminaRenderRoot != null ? _staminaRenderRoot.gameObject : null,
                "翻错一组会扣 1 点体力，体力耗尽时本局会失败。",
                () =>
                {
                    var timeTarget = _timeHangout != null ? _timeHangout : _consumeTime != null ? _consumeTime.gameObject : null;
                    HighlightTutorialTarget(timeTarget,
                        "这里会记录你的通关用时，用时越短，记忆榜排名越靠前。",
                        () =>
                        {
                            HighlightTutorialTarget(_flipAllBtn,
                                "全部翻转会短暂翻开全场卡牌，帮你重新记位置。",
                                () =>
                                {
                                    HighlightTutorialTarget(_precisionLockBtn,
                                        "精准锁定会帮你直接找出一张可配对的目标牌。",
                                        FinishMemoryTutorialGuide,
                                        false,
                                        true);
                                },
                                false,
                                true);
                        },
                        false,
                        false,
                        MEMORY_TUTORIAL_TIMER_TIP_POSITION);
                },
                false,
                false,
                MEMORY_TUTORIAL_STAMINA_TIP_POSITION);
        }

        private void HighlightTutorialTarget(
            GameObject target,
            string message,
            Action onComplete,
            bool playEmphasisAnimation = true,
            bool hideSourceWhileHighlighted = false,
            Vector2? tipAnchoredPosition = null)
        {
            if (_gameEnded)
            {
                return;
            }

            if (target == null)
            {
                FAsync.Instance.InvokeDelay(() => onComplete?.Invoke(), 0.2f);
                return;
            }

            var sourceCanvasGroup = hideSourceWhileHighlighted ? GetOrCreateCanvasGroup(target.transform) : null;
            var sourceAlpha = sourceCanvasGroup != null ? sourceCanvasGroup.alpha : 1f;
            var sourceInteractable = sourceCanvasGroup != null && sourceCanvasGroup.interactable;
            var sourceBlocksRaycasts = sourceCanvasGroup != null && sourceCanvasGroup.blocksRaycasts;
            var stepDone = false;
            void FinishStep()
            {
                if (stepDone) return;
                stepDone = true;
                if (hideSourceWhileHighlighted && sourceCanvasGroup != null)
                {
                    sourceCanvasGroup.alpha = sourceAlpha;
                    sourceCanvasGroup.interactable = sourceInteractable;
                    sourceCanvasGroup.blocksRaycasts = sourceBlocksRaycasts;
                }

                FAsync.Instance.InvokeDelay(() => onComplete?.Invoke(), 0.3f);
            }

            var ui = CreateTutorialHighlight(target, () =>
            {
                FinishStep();
            });
            if (hideSourceWhileHighlighted)
            {
                AlignHighlightToTarget(ui, target);
            }

            if (hideSourceWhileHighlighted && sourceCanvasGroup != null)
            {
                sourceCanvasGroup.alpha = 0f;
                sourceCanvasGroup.interactable = false;
                sourceCanvasGroup.blocksRaycasts = false;
            }

            AlertTipView.ShowTopMessage(FormatTutorialStepMessage(message), 20f, tipAnchoredPosition ?? MEMORY_TUTORIAL_TIP_POSITION);
            if (playEmphasisAnimation)
            {
                ui?.transform.DOShakeScale(1.2f, 0.5f);
            }

            FAsync.Instance.InvokeDelay(FinishStep, MEMORY_TUTORIAL_AUTO_ADVANCE_DELAY);
        }

        private void TriggerTutorialGridTap(MGrid grid)
        {
            if (grid == null || !grid.IsAlive() || _gameEnded) return;

            var prevLocked = _isInputLocked;
            _isInputLocked = false;
            grid.OnTap();
            _isInputLocked = prevLocked || _isTutorialRunning;
        }

        private void FinishMemoryTutorialGuide()
        {
            if (_gameEnded) return;
            ClearTutorialHighlight();

            PlayerLocalData.Instance.MemoryTutorialCompleted = true;
            AlertTipView.ShowTopMessage("现在开始正式挑战，尽量用最短时间完成全部配对。", 3.5f, MEMORY_TUTORIAL_TIP_POSITION);
            BeginMemoryRound();
        }

        private GameObject CreateTutorialHighlight(GameObject target, Action onClick)
        {
            ClearTutorialHighlight();
            _currentTutorialHighlight = FUiHighlighter.HighlightUi(target, () =>
            {
                onClick?.Invoke();
            });
            return _currentTutorialHighlight;
        }

        private static void AlignHighlightToTarget(GameObject highlight, GameObject target)
        {
            if (highlight == null || target == null) return;

            var targetRt = target.GetComponent<RectTransform>();
            var highlightRt = highlight.GetComponent<RectTransform>();
            if (targetRt == null || highlightRt == null) return;

            var parentRt = highlightRt.parent as RectTransform;
            highlightRt.position = targetRt.position;
            highlightRt.rotation = targetRt.rotation;
            highlightRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetRt.rect.width);
            highlightRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetRt.rect.height);

            if (parentRt == null)
            {
                highlightRt.localScale = targetRt.lossyScale;
                return;
            }

            var parentLossy = parentRt.lossyScale;
            var targetLossy = targetRt.lossyScale;
            highlightRt.localScale = new Vector3(
                SafeDiv(targetLossy.x, parentLossy.x),
                SafeDiv(targetLossy.y, parentLossy.y),
                SafeDiv(targetLossy.z, parentLossy.z));
        }

        private static float SafeDiv(float numerator, float denominator)
        {
            return Mathf.Abs(denominator) < 0.0001f ? 1f : numerator / denominator;
        }

        private void ClearTutorialHighlight()
        {
            TryCloseHighlighterByApi();

            if (_currentTutorialHighlight != null)
            {
                Destroy(_currentTutorialHighlight);
                _currentTutorialHighlight = null;
            }
        }

        private static void TryCloseHighlighterByApi()
        {
            var type = typeof(FUiHighlighter);
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;

            var clearLikeMethodNames = new[]
            {
                "ClearHighlightUi",
                "Clear",
                "Close",
                "CloseCurrent",
                "CloseHighlight",
                "DestroyCurrent"
            };

            foreach (var methodName in clearLikeMethodNames)
            {
                var method = type.GetMethod(methodName, flags, null, Type.EmptyTypes, null);
                if (method == null) continue;
                method.Invoke(null, null);
                return;
            }
        }

        private static string FormatTutorialStepMessage(string message)
        {
            return message;
        }

        private (MGrid opened, MGrid hidden) GetTutorialDemoPair()
        {
            foreach (var opened in _defaultOpenedGrids)
            {
                if (opened == null || !opened.IsAlive() || !opened.IsLockedOpened()) continue;

                var hidden = FindMatchingGrid(opened);
                if (hidden != null && hidden.IsAlive())
                {
                    return (opened, hidden);
                }
            }

            return (null, null);
        }

        private void SetupDefaultOpenedGrids(Action onComplete)
        {
            foreach (var item in _defaultOpenedGrids)
            {
                if (item != null && item.IsAlive())
                {
                    item.SetLockedOpened(false);
                }
            }
            _defaultOpenedGrids.Clear();

            var minCount = DEFAULT_OPENED_MIN_COUNT_FALLBACK;
            var maxCount = DEFAULT_OPENED_MAX_COUNT_FALLBACK;
            var runtime = Runtime.Instance;
            if (runtime != null)
            {
                minCount = runtime.memoryModeDefaultOpenedMinCount;
                maxCount = runtime.memoryModeDefaultOpenedMaxCount;
            }

            // 兼容旧版本从“圈数配置”迁移后遗留的 1/1 序列化值，避免开局固定只翻 1 张。
            if (minCount == 1 && maxCount == 1)
            {
                minCount = DEFAULT_OPENED_MIN_COUNT_FALLBACK;
                maxCount = DEFAULT_OPENED_MAX_COUNT_FALLBACK;
            }

            minCount = Mathf.Max(0, minCount);
            maxCount = Mathf.Max(minCount, maxCount);
            if (maxCount <= 0)
            {
                onComplete?.Invoke();
                return;
            }

            var idBuckets = new Dictionary<int, List<MGrid>>();
            foreach (var grid in GetAllActiveGrids())
            {
                if (grid == null || !grid.IsAlive() || !grid.IsFlipped()) continue;
                if (!idBuckets.TryGetValue(grid.id, out var list))
                {
                    list = new List<MGrid>();
                    idBuckets.Add(grid.id, list);
                }
                list.Add(grid);
            }

            if (idBuckets.Count <= 0)
            {
                onComplete?.Invoke();
                return;
            }

            var expectedCount = Random.Range(minCount, maxCount + 1);
            if (expectedCount <= 0)
            {
                onComplete?.Invoke();
                return;
            }

            var candidateIds = new List<int>(idBuckets.Keys);
            candidateIds.Shuffle();
            var openCount = Mathf.Min(expectedCount, candidateIds.Count);

            var toOpen = new List<MGrid>();
            for (var i = 0; i < openCount; i++)
            {
                var id = candidateIds[i];
                var bucket = idBuckets[id];
                if (bucket.IsNullOrEmpty()) continue;
                var grid = bucket[Random.Range(0, bucket.Count)];
                grid.SetLockedOpened(true);
                _defaultOpenedGrids.Add(grid);
                toOpen.Add(grid);
            }

            var pending = toOpen.Count;
            var completed = false;
            void SafeComplete()
            {
                if (completed) return;
                completed = true;
                onComplete?.Invoke();
            }

            foreach (var grid in toOpen)
            {
                if (grid == null || !grid.IsAlive() || !grid.IsFlipped())
                {
                    pending--;
                    continue;
                }

                grid.FlipCard(cardBack, () =>
                {
                    pending--;
                    if (pending <= 0)
                    {
                        SafeComplete();
                    }
                });
            }

            if (pending <= 0)
            {
                SafeComplete();
            }
        }

        private IEnumerator PlayCountdown(int seconds, Action onComplete = null)
        {
            if (seconds <= 0)
            {
                onComplete?.Invoke();
                yield break;
            }

            _readyText.gameObject.SetActive(true);

            for (int i = seconds; i > 0; i--)
            {
                _readyText.DOKill();

                _readyText.text = i.ToString();

                // 初始状态
                _readyText.alpha = 0f;
                _readyText.rectTransform.localScale = Vector3.one * 0.3f;
                _readyText.rectTransform.rotation = Quaternion.Euler(0, 0, -15f);

                Sequence seq = DOTween.Sequence();

                // 淡入 + 放大 + 旋转归位
                seq.Append(_readyText.DOFade(1f, 0.15f));
                seq.Join(_readyText.rectTransform.DOScale(1.3f, 0.2f)
                    .SetEase(Ease.OutBack));
                seq.Join(_readyText.rectTransform.DORotate(Vector3.zero, 0.2f)
                    .SetEase(Ease.OutBack));

                // 稍微停顿
                seq.AppendInterval(0.35f);

                // 缩小 + 淡出 + 旋转
                seq.Append(_readyText.rectTransform.DOScale(0.5f, 0.15f)
                    .SetEase(Ease.InBack));
                seq.Join(_readyText.DOFade(0f, 0.15f));
                seq.Join(_readyText.rectTransform.DORotate(new Vector3(0, 0, 15f), 0.15f));

                yield return seq.WaitForCompletion();
            }

            // 显示 GO
            _readyText.DOKill();
            _readyText.text = "GO!";
            _readyText.alpha = 0f;
            _readyText.rectTransform.localScale = Vector3.one * 0.3f;
            _readyText.rectTransform.rotation = Quaternion.identity;

            Sequence goSeq = DOTween.Sequence();
            goSeq.Append(_readyText.DOFade(1f, 0.1f));
            goSeq.Join(_readyText.rectTransform.DOScale(1.5f, 0.15f)
                .SetEase(Ease.OutBack));

            // 轻微震动效果
            goSeq.Append(_readyText.rectTransform.DOPunchScale(Vector3.one * 0.2f, 0.3f, 5, 0.5f));

            goSeq.AppendInterval(0.2f);
            goSeq.Append(_readyText.DOFade(0f, 0.15f));
            goSeq.Join(_readyText.rectTransform.DOScale(2f, 0.15f));

            yield return goSeq.WaitForCompletion();

            _readyText.gameObject.SetActive(false);

            onComplete?.Invoke();
        }

        public float GetXPosition(int x)
        {
            return x * (_gridSize + SPACING) + _gridSize / 2 + _xOffset;
        }

        public float GetYPosition(int y)
        {
            return -y * (_gridSize + SPACING) - _gridSize / 2 - _yOffset;
        }

        [FOnClick("open-settings-btn")]
        [Preserve]
        public void OpenSettings()
        {
            if (_isTutorialRunning) return;
            FAudioCore.Instance.PlaySfx("tap");
            FViewManager.Instance.Show<GameSettingsView>();
            GameSettingsView.callback = RestartGame;
        }

        private void EndGame(bool isWin)
        {
            if (_gameEnded) return;
            StopRoundTimer();
            var elapsedSeconds = Mathf.Max(0, Mathf.FloorToInt(_roundElapsedTime));
            _gameEnded = true;
            _isInputLocked = true;
            selectedGrids.Clear();
            StopPrecisionLockDurationUi(true);
            ShowStaminaUi(true);

            if (isWin)
            {
                // 胜利时的庆祝动画
                var allGrids = GetAllActiveGrids();
                foreach (var grid in allGrids)
                {
                    var rt = grid.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        var delay = Random.Range(0f, 0.3f);
                        FAsync.Instance.InvokeDelay(() =>
                        {
                            rt.DOPunchScale(Vector3.one * 0.3f, 0.5f, 5, 0.5f);
                        }, delay);
                    }
                }
            }

            FAsync.Instance.InvokeDelay(() =>
            {
                MemoryGameWinView.ShowResult(isWin, elapsedSeconds);
            }, isWin ? 0.5f : 0f);
        }

        private void ResetRoundTimer()
        {
            _roundElapsedTime = 0f;
            _isRoundTiming = false;
            // if (_timeHangout != null) _timeHangout.SetActive(false);
            UpdateConsumeTimeText();
        }

        private void StartRoundTimer()
        {
            _isRoundTiming = true;
        }

        private void StopRoundTimer()
        {
            _isRoundTiming = false;
            UpdateConsumeTimeText();
        }

        private void UpdateConsumeTimeText()
        {
            if (_consumeTime == null) return;
            var totalSeconds = Mathf.FloorToInt(_roundElapsedTime);
            var ts = TimeSpan.FromSeconds(totalSeconds);
            _consumeTime.text = $"{(int)ts.TotalMinutes:00}:{ts.Seconds:00}";
        }

        // 道具相关方法
        public void LockInput(bool locked)
        {
            _isInputLocked = locked;
        }

        public void SetPrecisionLockActive(bool active)
        {
            _isPrecisionLockActive = active;
        }

        public void StartPrecisionLockDurationUi(float duration)
        {
            if (_lockRemaining == null) return;

            _lockRemainingTween?.Kill();
            _lockRemaining.gameObject.SetActive(true);
            _lockRemaining.value = 1f;

            if (_lockRemainingCanvasGroup == null)
            {
                _lockRemainingCanvasGroup = GetOrCreateCanvasGroup(_lockRemaining.transform);
            }

            _lockRemainingCanvasGroup.DOKill();
            _lockRemaining.transform.DOKill();
            _lockRemainingCanvasGroup.alpha = 0f;
            _lockRemaining.transform.localScale = new Vector3(0.92f, 0.92f, 1f);
            _lockRemainingCanvasGroup.DOFade(1f, 0.18f).SetEase(Ease.OutQuad);
            _lockRemaining.transform.DOScale(1f, 0.22f).SetEase(Ease.OutBack);

            HideStaminaUi();

            _lockRemainingTween = DOTween.To(() => _lockRemaining.value, v => _lockRemaining.value = v, 0f, duration)
                .SetEase(Ease.Linear)
                .SetLink(_lockRemaining.gameObject);
        }

        public void StopPrecisionLockDurationUi(bool immediate = false)
        {
            _lockRemainingTween?.Kill();
            _lockRemainingTween = null;
            if (_lockRemaining == null) return;

            if (immediate)
            {
                if (_lockRemainingCanvasGroup != null)
                {
                    _lockRemainingCanvasGroup.DOKill();
                    _lockRemainingCanvasGroup.alpha = 0f;
                }
                _lockRemaining.transform.DOKill();
                _lockRemaining.gameObject.SetActive(false);
                return;
            }

            if (_lockRemainingCanvasGroup == null)
            {
                _lockRemainingCanvasGroup = GetOrCreateCanvasGroup(_lockRemaining.transform);
            }

            _lockRemainingCanvasGroup.DOKill();
            _lockRemaining.transform.DOKill();
            var seq = DOTween.Sequence();
            seq.SetLink(_lockRemaining.gameObject);
            seq.Join(_lockRemainingCanvasGroup.DOFade(0f, 0.18f).SetEase(Ease.InQuad));
            seq.Join(_lockRemaining.transform.DOScale(0.92f, 0.18f).SetEase(Ease.InBack));
            seq.OnComplete(() =>
            {
                if (_lockRemaining != null)
                {
                    _lockRemaining.gameObject.SetActive(false);
                }
            });
        }

        public void PlayFlipAllCountdown(float duration)
        {
            var seconds = Mathf.Max(1, Mathf.CeilToInt(duration));
            FAsync.Instance.StartRoutine(PlayCountdown(seconds));
        }

        private void HideStaminaUi()
        {
            if (_staminaRenderRoot == null) return;
            if (_staminaCanvasGroup == null)
            {
                _staminaCanvasGroup = GetOrCreateCanvasGroup(_staminaRenderRoot);
            }

            _staminaCanvasGroup.DOKill();
            _staminaRenderRoot.DOKill();
            _staminaCanvasGroup.DOFade(0f, 0.18f).SetEase(Ease.InQuad);
            _staminaRenderRoot.DOAnchorPosY(_staminaOriginY - 24f, 0.22f).SetEase(Ease.InBack);
        }

        public void ShowStaminaUi(bool immediate = false)
        {
            if (_staminaRenderRoot == null) return;
            if (_staminaCanvasGroup == null)
            {
                _staminaCanvasGroup = GetOrCreateCanvasGroup(_staminaRenderRoot);
            }

            _staminaCanvasGroup.DOKill();
            _staminaRenderRoot.DOKill();

            if (immediate)
            {
                _staminaCanvasGroup.alpha = 1f;
                _staminaRenderRoot.anchoredPosition = new Vector2(_staminaRenderRoot.anchoredPosition.x, _staminaOriginY);
                return;
            }

            _staminaCanvasGroup.alpha = Mathf.Clamp01(_staminaCanvasGroup.alpha);
            _staminaRenderRoot.DOAnchorPosY(_staminaOriginY, 0.22f).SetEase(Ease.OutBack);
            _staminaCanvasGroup.DOFade(1f, 0.18f).SetEase(Ease.OutQuad);
        }

        private static CanvasGroup GetOrCreateCanvasGroup(Component target)
        {
            if (target == null) return null;
            var group = target.GetComponent<CanvasGroup>();
            return group != null ? group : target.gameObject.AddComponent<CanvasGroup>();
        }

        private void ResetRoundPowerUpInventory()
        {
            _roundFlipAllItemAmount = ROUND_START_POWER_UP_AMOUNT;
            _roundPrecisionLockItemAmount = ROUND_START_POWER_UP_AMOUNT;
            RefreshPowerUpInventoryUi();
        }

        private void RefreshPowerUpInventoryUi()
        {
            UpdatePowerUpInventoryUi(_flipAmountTip, _videoFlip, _roundFlipAllItemAmount);
            UpdatePowerUpInventoryUi(_precisionAmountTip, _videoPrecision, _roundPrecisionLockItemAmount);
        }

        private static void UpdatePowerUpInventoryUi(TMP_Text amountText, GameObject videoIcon, int amount)
        {
            var safeAmount = Mathf.Max(0, amount);
            var amountRoot = amountText != null ? amountText.transform.parent?.gameObject : null;
            if (amountText != null)
            {
                amountText.text = safeAmount.ToString();
            }

            if (amountRoot != null)
            {
                amountRoot.SetActive(safeAmount > 0);
            }

            if (videoIcon != null)
            {
                videoIcon.SetActive(safeAmount <= 0);
            }
        }

        public List<MGrid> GetAllActiveGrids()
        {
            var result = new List<MGrid>();
            if (_gridItems == null) return result;

            for (int x = 0; x < _col; x++)
            {
                for (int y = 0; y < _row; y++)
                {
                    if (_gridItems[x][y] != null)
                    {
                        result.Add(_gridItems[x][y]);
                    }
                }
            }
            return result;
        }

        public MGrid FindMatchingGrid(MGrid targetGrid)
        {
            if (_gridItems == null || targetGrid == null || !targetGrid.IsAlive()) return null;

            for (int x = 0; x < _col; x++)
            {
                for (int y = 0; y < _row; y++)
                {
                    var grid = _gridItems[x][y];
                    // 查找另一张相同ID且背面朝上的牌
                    if (grid != null && grid.IsAlive() && grid != targetGrid && grid.id == targetGrid.id && grid.IsFlipped())
                    {
                        return grid;
                    }
                }
            }
            return null;
        }

        [FOnClick("flip-all-btn")]
        [Preserve]
        public void OnFlipAllButtonClick()
        {
            if (_isTutorialRunning) return;
            Debug.Log("全部翻转按钮被点击");
            TryUsePowerUpWithInventory(
                _flipAllBtn,
                _flipAllPowerUp,
                () => _roundFlipAllItemAmount,
                v => _roundFlipAllItemAmount = v,
                "全部翻转");
        }

        [FOnClick("precision-lock-btn")]
        [Preserve]
        public void OnPrecisionLockButtonClick()
        {
            if (_isTutorialRunning) return;
            Debug.Log("精准锁定按钮被点击");
            TryUsePowerUpWithInventory(
                _precisionLockBtn,
                _precisionLockPowerUp,
                () => _roundPrecisionLockItemAmount,
                v => _roundPrecisionLockItemAmount = v,
                "精准锁定");
        }

        private void TryUsePowerUpWithInventory(
            GameObject buttonObject,
            MemoryPowerUp powerUp,
            Func<int> getItemAmount,
            Action<int> setItemAmount,
            string powerUpLabel)
        {
            var btn = buttonObject;
            if (powerUp == null || !powerUp.CanUse())
            {
                Debug.Log($"{powerUpLabel}道具正在生效中或暂不可用");
                if (btn != null)
                {
                    btn.transform.DOShakePosition(0.3f, 5f, 20, 90, false, true);
                }
                return;
            }

            var amount = Mathf.Max(0, getItemAmount());
            if (amount > 0)
            {
                setItemAmount(amount - 1);
                RefreshPowerUpInventoryUi();
                Debug.Log($"{powerUpLabel}消耗库存，剩余: {amount - 1}");
                ActivatePowerUp(powerUpLabel, powerUp, btn);
                return;
            }

            FAds.Instance.Video(success =>
            {
                if (!success)
                {
                    Debug.Log($"{powerUpLabel}广告未完成，未激活");
                    if (btn != null)
                    {
                        btn.transform.DOShakePosition(0.3f, 5f, 20, 90, false, true);
                    }
                    return;
                }

                ActivatePowerUp(powerUpLabel, powerUp, btn);
            });
        }

        private static void ActivatePowerUp(string powerUpLabel, MemoryPowerUp powerUp, GameObject btn)
        {
            Debug.Log($"{powerUpLabel}道具激活");
            FAudioCore.Instance.PlaySfx("tap");
            powerUp.Use();

            if (btn != null)
            {
                btn.transform.DOPunchScale(Vector3.one * 0.2f, 0.3f, 5, 0.5f);
            }
        }
    }
}
