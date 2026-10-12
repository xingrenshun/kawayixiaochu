using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Effect;
using FCSharpExt;
using FTools.Cache;
using FTools.Network;
using FTools.Platforms;
using FUnityAttribute.RuntimeAttributes;
using FUnityCommon;
using FUnityUI;
using Newtonsoft.Json;
using Response;
using TMPro;
using Ui;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UI;

namespace View
{
    public struct RankingData
    {
        public long score;
        public int rank;
        public UserInfoDto user;
    }

    [Serializable]
    internal class RankingNetData
    {
        public string board;
        public long topN;
        public List<RankingData> list;
    }

    [Serializable]
    internal class MyScoreData
    {
        public int rank;
        public int score;
    }

    [FViewLoader("View/RankingView")]
    public class RankingView : FUiBase<RankingView>
    {
        private const string BOARD_TOTAL = "total-ranking";
        private const string BOARD_MEMORY = "memory-time-ranking";
        private const string TITLE_TOTAL = "总分榜";
        private const string TITLE_MEMORY = "记忆榜";
        private const int MEMORY_MAX_REASONABLE_SECONDS = 36000;
        private const float REQUEST_TIMEOUT_SECONDS = 8f;

        // 当前排行榜名称，默认显示总分榜
        [FuiAutowire] private TMP_Text _rankingTitle;
        [FuiAutowire] private Image _staminaIcon;
        [FuiAutowire] private TMP_Text _staminaTimeText;
        [FuiAutowire] private TMP_Text _staminaValueText;
        [FuiAutowire] private TMP_Text _rankNum;
        [FuiAutowire] private TMP_Text _playerName;
        [FuiAutowire] private TMP_Text _playerRankNum;
        [FuiAutowire] private TMP_Text _playerScore;
        [FuiAutowire] private Image _playerAvatar;
        [FuiAutowire] private Image _topAvatar;
        [FuiAutowire] private RectTransform _topRankArea;
        [FuiAutowire("List-Render-Root")] private RectTransform _listRenderRoot;
        [FGameObject("OtherPlayerRenderItem")] private GameObject _otherPlayerPrefab;
        [FuiAutowire("Button-StartGame")] private RectTransform _startGameButton;
        private bool _isTapedStart;

        private List<RankItem> _topRankItems;

        private Action<int> _onStaminaChanged;
        private Action<TimeSpan> _onRecoveryChanged;

        private ICache _localCache;
        private string _currentBoard = BOARD_TOTAL;
        private bool _isRankingLoading;
        private bool _isMyScoreLoading;
        private int _rankingRequestVersion;
        private int _myScoreRequestVersion;
        private Coroutine _rankingTimeoutCoroutine;
        private Coroutine _myScoreTimeoutCoroutine;
        private readonly Dictionary<string, RankingNetData> _rankingCache = new Dictionary<string, RankingNetData>();
        private readonly Dictionary<string, MyScoreData> _myScoreCache = new Dictionary<string, MyScoreData>();
        private readonly HashSet<GameObject> _fixedListChildren = new HashSet<GameObject>();

        public override void OnSetup()
        {
            // 显示插屏广告
            FAds.Instance.ShowInterstitialAd();
            _topRankItems = _topRankArea.GetComponentsInChildren<RankItem>().ToList();
            _localCache = FCacheManager.GetCache(ECacheType.Local);
            _playerName.text = _localCache.Load("nickname", "点击使用平台名称");

            var data = PlayerLocalData.Instance;
            _staminaValueText.text = data.PlayerStamina.ToString();
            _onStaminaChanged = stamina => { _staminaValueText.text = stamina.ToString(); };

            _onRecoveryChanged = time =>
            {
                _staminaTimeText.text = time == TimeSpan.Zero
                    ? "已满"
                    : $"{time.Minutes:D2}:{time.Seconds:D2}";
            };

            data.OnStaminaChanged += _onStaminaChanged;
            data.OnNextRecoveryTimeChanged += _onRecoveryChanged;
            data.ImmediateTriggerCallback();
            var avatarUrl = _localCache.Load("avatarUrl", "");
            if (!string.IsNullOrEmpty(avatarUrl))
            {
                UpdateUserAvatar(_playerAvatar, avatarUrl, true);
            }

            CacheFixedListChildren();
            SwitchRankingBoard(BOARD_TOTAL);
        }


        [FOnClick("toggle-ranking")]
        private void ToggleRanking()
        {
            var targetBoard = _currentBoard == BOARD_TOTAL ? BOARD_MEMORY : BOARD_TOTAL;
            SwitchRankingBoard(targetBoard);
        }
        
        
        [FOnClick("PopStamina")]
        private void OpenStamina()
        {
            VideoStaminaView.ShowView();
        }

        [FOnClick("share-ranking")]
        [Preserve]
        private void ShareRankingInfo()
        {
            FPlatformCore.Instance.ShareApp(null, null);
        }
        private void GetMyRankInfo(string board)
        {
            var requestVersion = StartMyScoreRequest();
            FRequest.Instance.Post<MyScoreData>("ranking/me", new Dictionary<string, object>()
            {
                { "board", board },
#if UNITY_EDITOR
                { "openId", FCacheManager.GetCache(ECacheType.Local).Load<string>("openId","") }
#else
                { "openId", FCacheManager.GetCache(ECacheType.Local).Load<string>("openId") }
#endif
            }, res =>
            {
                if (!TryCompleteMyScoreRequest(requestVersion)) return;
                if (res.code != 200 || res.data == null)
                {
                    if (board == _currentBoard)
                    {
                        RenderMyScoreDefault(board);
                        ShowRankRequestError("加载个人排名失败，请稍后再试");
                    }
                    return;
                }

                _myScoreCache[board] = res.data;
                if (board == _currentBoard)
                {
                    RenderMyScore(board, res.data);
                }
            });
        }

        private void UpdateUserAvatar(Image image, string avatarLink, bool isOwner = false)
        {
            if (isOwner)
            {
                if (PlayerNetData.Instance.avatar != null)
                {
                    _topAvatar.sprite = PlayerNetData.Instance.avatar;
                    image.sprite = PlayerNetData.Instance.avatar;
                    return;
                }
            }
            FUnityNetworkImageLoader.LoadImage(avatarLink, icon =>
            {
                image.sprite = icon;
                if (!isOwner) return;
                _topAvatar.sprite = icon;
                PlayerNetData.Instance.avatar = icon;
            });
        }

        private void GetTotalRanking(string board)
        {
            var requestVersion = StartRankingRequest();
            try
            {
                var sendPayload = new Dictionary<string, object>
                {
                    { "topN", 10 },
                    { "board", board }
                };
                FRequest.Instance.Post<RankingNetData>("ranking/list", sendPayload, res =>
                {
                    if (!TryCompleteRankingRequest(requestVersion)) return;
                    Debug.Log(JsonConvert.SerializeObject(res));
                    if (res.code != 200 || res.data == null)
                    {
                        if (board == _currentBoard)
                        {
                            ClearRankingRender();
                            ShowRankRequestError("加载排行榜失败，请稍后再试");
                        }
                        return;
                    }

                    var normalizedData = NormalizeRankingData(res.data);
                    _rankingCache[board] = normalizedData;
                    if (board == _currentBoard)
                    {
                        RenderRanking(normalizedData, board);
                    }
                });
            }
            catch (Exception e)
            {
                TryCompleteRankingRequest(requestVersion);
                Debug.LogError(e);
                if (board == _currentBoard)
                {
                    ClearRankingRender();
                    ShowRankRequestError("加载排行榜失败，请稍后再试");
                }
            }
        }

        [FOnClick("Ranking-Item-Self")]
        private void CheckOrPullWxUserProfileAuthView()
        {
            // 授权面板已关闭，点击自己那一行不再拉起认证页面
            if (!WxProfileAuthView.EnableAuthPanel) return;

            // 认证过了就不做任何响应
            if (FCacheManager.GetCache(ECacheType.Local).Load("IsAuthed", false)) return;
            // 拉起认证页面
            var view = WxProfileAuthView.ShowView();
            view.onDestroyCallback += OnUpdateAuthInfo;
        }


        private void OnUpdateAuthInfo()
        {
            var isAuthed = FCacheManager.GetCache(ECacheType.Local).Load("IsAuthed", false);
            if (!isAuthed) return;
            var localCache = FCacheManager.GetCache(ECacheType.Local);
            var avatar = localCache.Load<string>("avatarUrl");
            var nickname = localCache.Load<string>("nickname");
            UpdateUserAvatar(_playerAvatar, avatar, true);
            _playerName.text = nickname;
        }

        private void RenderRanking(RankingNetData data, string board)
        {
            ClearRankingRender();
            var list = data.list;
            if (list.IsNullOrEmpty()) return;
            var topIndex = Mathf.Min(list.Count, 3);
            var topList = list.GetRange(0, topIndex);
            for (var index = 0; index < topList.Count; index++)
            {
                var t = topList[index];
                _topRankItems[index].UpdateInfo(t, FormatScoreForBoard(board, t.score));
            }
            var remainList = list.GetRange(topIndex, list.Count - topIndex);
            foreach (var i in remainList)
            {
                // 渲染剩余的
                var instance = Instantiate(_otherPlayerPrefab, _listRenderRoot);
                instance.SetActive(true);
                var script = instance.GetComponent<RankItem>();
                script.UpdateInfo(i, FormatScoreForBoard(board, i.score));
            }
        }

        private void SwitchRankingBoard(string board)
        {
            _currentBoard = board;
            UpdateRankingTitle(board);
            LoadCurrentBoardData();
        }

        private void LoadCurrentBoardData()
        {
            CancelPendingRequests();
            if (_rankingCache.TryGetValue(_currentBoard, out var cachedRanking))
            {
                RenderRanking(cachedRanking, _currentBoard);
            }
            else
            {
                ClearRankingRender();
                GetTotalRanking(_currentBoard);
            }

            if (_myScoreCache.TryGetValue(_currentBoard, out var cachedMyScore))
            {
                RenderMyScore(_currentBoard, cachedMyScore);
            }
            else
            {
                RenderMyScoreDefault(_currentBoard);
                GetMyRankInfo(_currentBoard);
            }
        }

        private void RenderMyScore(string board, MyScoreData data)
        {
            _playerRankNum.text = data.rank.ToString();
            _rankNum.text = data.rank > 99 ? "99+" : data.rank.ToString();
            _playerScore.text = FormatScoreForBoard(board, data.score);
        }

        private void RenderMyScoreDefault(string board)
        {
            _playerRankNum.text = "--";
            _rankNum.text = "99+";
            _playerScore.text = IsMemoryBoard(board) ? "--:--" : "0";
        }

        private void UpdateRankingTitle(string board)
        {
            _rankingTitle.text = board == BOARD_MEMORY ? TITLE_MEMORY : TITLE_TOTAL;
        }

        private void ClearRankingRender()
        {
            foreach (var item in _topRankItems)
            {
                if (item == null) continue;
                item.ResetDisplay();
            }

            for (int i = _listRenderRoot.childCount - 1; i >= 0; i--)
            {
                var child = _listRenderRoot.GetChild(i).gameObject;
                if (_fixedListChildren.Contains(child)) continue;
                Destroy(child);
            }
        }

        private void CacheFixedListChildren()
        {
            _fixedListChildren.Clear();
            if (_listRenderRoot == null) return;
            for (int i = 0; i < _listRenderRoot.childCount; i++)
            {
                var child = _listRenderRoot.GetChild(i).gameObject;
                _fixedListChildren.Add(child);
            }
        }

        private int StartRankingRequest()
        {
            _rankingRequestVersion++;
            StopTimeoutCoroutine(ref _rankingTimeoutCoroutine);
            SetRankingLoading(true);
            _rankingTimeoutCoroutine = StartCoroutine(HandleRequestTimeout(
                _rankingRequestVersion,
                isRankingRequest: true,
                board: _currentBoard));
            return _rankingRequestVersion;
        }

        private int StartMyScoreRequest()
        {
            _myScoreRequestVersion++;
            StopTimeoutCoroutine(ref _myScoreTimeoutCoroutine);
            SetMyScoreLoading(true);
            _myScoreTimeoutCoroutine = StartCoroutine(HandleRequestTimeout(
                _myScoreRequestVersion,
                isRankingRequest: false,
                board: _currentBoard));
            return _myScoreRequestVersion;
        }

        private bool TryCompleteRankingRequest(int requestVersion)
        {
            if (requestVersion != _rankingRequestVersion) return false;
            StopTimeoutCoroutine(ref _rankingTimeoutCoroutine);
            SetRankingLoading(false);
            return true;
        }

        private bool TryCompleteMyScoreRequest(int requestVersion)
        {
            if (requestVersion != _myScoreRequestVersion) return false;
            StopTimeoutCoroutine(ref _myScoreTimeoutCoroutine);
            SetMyScoreLoading(false);
            return true;
        }

        private IEnumerator HandleRequestTimeout(int requestVersion, bool isRankingRequest, string board)
        {
            yield return new WaitForSeconds(REQUEST_TIMEOUT_SECONDS);

            var isCurrentRequest = isRankingRequest
                ? requestVersion == _rankingRequestVersion
                : requestVersion == _myScoreRequestVersion;
            if (!isCurrentRequest) yield break;

            if (isRankingRequest)
            {
                _rankingRequestVersion++;
                _rankingTimeoutCoroutine = null;
                SetRankingLoading(false);
                if (board == _currentBoard)
                {
                    ClearRankingRender();
                    ShowRankRequestError("排行榜加载超时，请检查网络后重试");
                }
                yield break;
            }

            _myScoreRequestVersion++;
            _myScoreTimeoutCoroutine = null;
            SetMyScoreLoading(false);
            if (board == _currentBoard)
            {
                RenderMyScoreDefault(board);
                ShowRankRequestError("个人排名加载超时，请检查网络后重试");
            }
        }

        private void CancelPendingRequests()
        {
            _rankingRequestVersion++;
            _myScoreRequestVersion++;
            StopTimeoutCoroutine(ref _rankingTimeoutCoroutine);
            StopTimeoutCoroutine(ref _myScoreTimeoutCoroutine);
            SetRankingLoading(false);
            SetMyScoreLoading(false);
        }

        private void SetRankingLoading(bool isLoading)
        {
            _isRankingLoading = isLoading;
            RefreshLoadingState();
        }

        private void SetMyScoreLoading(bool isLoading)
        {
            _isMyScoreLoading = isLoading;
            RefreshLoadingState();
        }

        private void RefreshLoadingState()
        {
            if (_isRankingLoading || _isMyScoreLoading)
            {
                LoadingView.ShowView();
                return;
            }

            LoadingView.HideView();
        }

        private void StopTimeoutCoroutine(ref Coroutine coroutine)
        {
            if (coroutine == null) return;
            StopCoroutine(coroutine);
            coroutine = null;
        }

        private void ShowRankRequestError(string message)
        {
            AlertTipView.Instance?.FadeIn(message);
        }

        private void OnDestroy()
        {
            CancelPendingRequests();
            _rankingCache.Clear();
            _myScoreCache.Clear();
            LoadingView.HideView();
        }

        private static bool IsMemoryBoard(string board)
        {
            return board == BOARD_MEMORY;
        }

        private static int DecodeMemoryScore(long score)
        {
            if (score <= MEMORY_MAX_REASONABLE_SECONDS)
            {
                return Mathf.Max(0, (int)score);
            }

            var decoded = int.MaxValue - score;
            return Mathf.Max(0, (int)Math.Min(int.MaxValue, decoded));
        }

        private static string FormatTime(int seconds)
        {
            var ts = TimeSpan.FromSeconds(Mathf.Max(0, seconds));
            return $"{(int)ts.TotalMinutes:00}:{ts.Seconds:00}";
        }

        private static string FormatScoreForBoard(string board, long score)
        {
            if (!IsMemoryBoard(board))
            {
                return score.ToThousands();
            }

            return FormatTime(DecodeMemoryScore(score));
        }

        private static RankingNetData NormalizeRankingData(RankingNetData data)
        {
            if (data == null)
            {
                return new RankingNetData
                {
                    list = new List<RankingData>()
                };
            }

            var normalizedList = (data.list ?? new List<RankingData>())
                .OrderBy(item => item.rank > 0 ? 0 : 1)
                .ThenBy(item => item.rank > 0 ? item.rank : int.MaxValue)
                .ThenByDescending(item => item.score)
                .ToList();

            for (var i = 0; i < normalizedList.Count; i++)
            {
                if (normalizedList[i].rank > 0) continue;
                var item = normalizedList[i];
                item.rank = i + 1;
                normalizedList[i] = item;
            }

            return new RankingNetData
            {
                board = data.board,
                topN = data.topN,
                list = normalizedList
            };
        }

        private bool StaminaCheck()
        {
            var staminaIsEnough = PlayerLocalData.Instance.PlayerStamina > 0;
            if (!staminaIsEnough)
            {
                AlertTipView.Instance!.FadeIn("没有体力啦~");
            }

            return staminaIsEnough;
        }

        private void OnDisable()
        {
            var data = PlayerLocalData.Instance;
            data.OnStaminaChanged -= _onStaminaChanged;
            data.OnNextRecoveryTimeChanged -= _onRecoveryChanged;
        }

        [FOnClick("Button-StartGame")]
        public void ToGame()
        {
            CheckStamina(_startGameButton, StaminaCheck, () =>
            {
                Runtime.Instance.StartGame();
                PlayerLocalData.Instance.PlayerStamina--;
                DestroyView();
            });
        }

        private void CheckStamina(Transform target, Func<bool> preCheck, Action callback)
        {
            if (!preCheck() || _isTapedStart) return;
            _isTapedStart = true;
            FAsync.Instance.StartRoutine(IconMoveVfx.StartIconCircleMove(_staminaIcon.gameObject, target,
                10, null, callback));
        }

        [FOnClick("Button-Back")]
        public void ToHome()
        {
            HomeView.ShowView();
            DestroyView();
        }
    }
}
