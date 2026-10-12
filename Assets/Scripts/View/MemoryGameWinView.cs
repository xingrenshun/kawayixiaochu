using System;
using System.Collections;
using System.Collections.Generic;
using FTools.Cache;
using FTools.Helpers;
using FTools.Network;
using FTools.Platforms;
using FUnityAttribute.RuntimeAttributes;
using FUnityCommon;
using FUnityUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace View
{
    [FViewLoader("View/GameWinView")]
    public class MemoryGameWinView : FUiBase<MemoryGameWinView>
    {
        private const string MEMORY_RANK_BOARD = "memory-time-ranking";
        private const string WX_MEMORY_RANK_KEY = "SDTMRT";
        private const string LOCAL_MEMORY_BEST_TIME_KEY = "LocalMemoryBestTime";
        private const string LOCAL_MEMORY_RANK_KEY = "LocalMemoryRank";
        private const int MAX_REASONABLE_TIME_SECONDS = 36000;

        private static bool _pendingIsWin;
        private static int _pendingElapsedSeconds;

        private CanvasGroup _group;
        private Animator _rootAnimator;
        private ICache _localCache;

  

        [FuiAutowire] private TMP_Text _currentScore;
        [FuiAutowire("Avatar-Icon")] private Image _avatar;
        [FuiAutowire] private TMP_Text _historyScore;
        [FuiAutowire("history-cup-icon")] private Image _historyCupIcon;
        [FGameObject("history-cup-loading-icon")] private GameObject _historyCupLoadingIcon;
        [FuiAutowire("WinTitle")] private TMP_Text _title;
        [FGameObject("Win-Popup")] private GameObject _uiPanel;
        [FuiAutowire("total-rank-change-text")] private TMP_Text _playerRankNum;
        [FGameObject("victory-particle")] private GameObject _victoryParticle;

        public static void ShowResult(bool isWin, int elapsedSeconds)
        {
            _pendingIsWin = isWin;
            _pendingElapsedSeconds = Mathf.Max(0, elapsedSeconds);
            FViewManager.Instance.Show<MemoryGameWinView>();
        }

        public override void OnSetup()
        {
            FAds.Instance.HideBannerAd();
            _localCache = FCacheManager.GetCache(ECacheType.Local);
            _group = GetComponent<CanvasGroup>();
            _rootAnimator = _uiPanel.GetComponent<Animator>();
            // 授权面板已关闭，结算页不再弹出授权弹窗
            if (WxProfileAuthView.EnableAuthPanel)
            {
                var isAuthed = _localCache.Load("IsAuthed", false);
                var needTip = _localCache.Load("IsNeedTipUserAuth", true);
                if (!isAuthed && needTip)
                {
                    WxProfileAuthView.ShowView();
                }
            }

            var currentSeconds = _pendingElapsedSeconds;
            var localBest = _localCache.Load(LOCAL_MEMORY_BEST_TIME_KEY, -1);
            var bestSeconds = Mathf.Max(0, localBest);
            if (_pendingIsWin)
            {
                bestSeconds = GetBestTime(currentSeconds, localBest);
                _localCache.Save(LOCAL_MEMORY_BEST_TIME_KEY, bestSeconds);
            }

            _title.text = _pendingIsWin ? "通关成功" : "体力耗尽";
            _currentScore.text = FormatTime(currentSeconds);
            _historyScore.text = bestSeconds > 0 ? FormatTime(bestSeconds) : "--:--";
            var cachedRank = _localCache.Load(LOCAL_MEMORY_RANK_KEY, -1);
            _playerRankNum.text = cachedRank > 0 ? cachedRank.ToString() : "--";
            _historyCupIcon.enabled = true;
            _historyCupLoadingIcon.SetActive(false);

            if (PlayerNetData.Instance.avatar != null)
            {
                _avatar.sprite = PlayerNetData.Instance.avatar;
            }

            if (!_pendingIsWin)
            {
                return;
            }

            _historyCupIcon.enabled = false;
            _historyCupLoadingIcon.SetActive(true);

#if PT_dy
            // 上报最佳用时到抖音小游戏排行榜（复用 AdSDK 里的 PaiHangBang 封装）
            PaiHangBang.Instance?.UploadScore(EncodeRankScore(bestSeconds), MEMORY_RANK_BOARD);
#endif

            SubmitRank(currentSeconds);
        }

        private static int GetBestTime(int currentSeconds, int localBestSeconds)
        {
            if (currentSeconds <= 0) return Mathf.Max(0, localBestSeconds);
            if (localBestSeconds <= 0) return currentSeconds;
            return Mathf.Min(currentSeconds, localBestSeconds);
        }

        private static string FormatTime(int totalSeconds)
        {
            totalSeconds = Mathf.Max(0, totalSeconds);
            var ts = TimeSpan.FromSeconds(totalSeconds);
            return $"{(int)ts.TotalMinutes:00}:{ts.Seconds:00}";
        }

        // 排行榜默认高分在前：将用时反向编码成“越短越高分”。
        private static int EncodeRankScore(int elapsedSeconds)
        {
            elapsedSeconds = Mathf.Max(0, elapsedSeconds);
            return int.MaxValue - elapsedSeconds;
        }

        // 兼容旧数据：旧版直接上传秒数，新版上传编码分数。
        private static int DecodeRankScoreToElapsedSeconds(int storedScore)
        {
            if (storedScore <= MAX_REASONABLE_TIME_SECONDS)
            {
                return Mathf.Max(0, storedScore);
            }

            return Mathf.Max(0, int.MaxValue - storedScore);
        }

        private void SubmitRank(int currentSeconds)
        {
            FRequest.Instance.Post<bool>("ranking/submit", new Dictionary<string, object>
            {
                { "board", MEMORY_RANK_BOARD },
#if UNITY_EDITOR
                {
                    "openId",
                    FCacheManager.GetCache(ECacheType.Local).Load<string>("openId", "")
                },
#else
                { "openId", FCacheManager.GetCache(ECacheType.Local).Load<string>("openId") },
#endif
                { "score", EncodeRankScore(currentSeconds) }
            }, res =>
            {
                if (res.code != 200)
                {
                    _historyCupIcon.enabled = true;
                    _historyCupLoadingIcon.SetActive(false);
                    AlertTipView.Instance?.FadeIn("网络错误！上报成绩失败,\n系统会在下一次结算时重试.");
                    return;
                }

                GetMyRankInfo();
            });
        }

        private void GetMyRankInfo()
        {
            FRequest.Instance.Post<MyScoreData>("ranking/me", new Dictionary<string, object>
            {
                { "board", MEMORY_RANK_BOARD },
#if UNITY_EDITOR
                {
                    "openId",
                    FCacheManager.GetCache(ECacheType.Local).Load<string>("openId", "")
                }
#else
                { "openId", FCacheManager.GetCache(ECacheType.Local).Load<string>("openId") }
#endif
            }, res =>
            {
                _historyCupIcon.enabled = true;
                _historyCupLoadingIcon.SetActive(false);
                if (res.code != 200)
                {
                    AlertTipView.Instance?.FadeIn("拉取最新数据失败,请稍后再试");
                    return;
                }

                _playerRankNum.text = res.data.rank.ToString();
                var bestSeconds = DecodeRankScoreToElapsedSeconds(res.data.score);
                _historyScore.text = FormatTime(bestSeconds);
                _localCache.Save(LOCAL_MEMORY_BEST_TIME_KEY, bestSeconds);
                _localCache.Save(LOCAL_MEMORY_RANK_KEY, res.data.rank);
            });
        }

        [FOnClick("button-rank")]
        private void ToRankView()
        {
            FViewManager.Instance.DestroyView<MemoryGameView>();
            FViewManager.Instance.Show<RankingView>();
            DestroyView();
        }

        [FOnClick("back-home")]
        private void Close()
        {
            FAudioCore.Instance.PlaySfx("tap");
            if (_rootAnimator.GetCurrentAnimatorStateInfo(0).IsName("Open"))
            {
                _rootAnimator.Play("Close");
            }

            FViewManager.Instance.DestroyView<MemoryGameView>();
            FViewManager.Instance.Show<HomeView>();
            StartCoroutine(RunPopupDestroy());
        }

        [FOnClick("continue-btn")]
        private void ContinueGame()
        {
            FAudioCore.Instance.PlaySfx("tap");
            if (_rootAnimator.GetCurrentAnimatorStateInfo(0).IsName("Open"))
            {
                _rootAnimator.Play("Close");
            }

            var view = MemoryGameView.Instance;
            if (view != null)
            {
                view.RestartGame();
            }
            StartCoroutine(RunPopupDestroy());
            FAds.Instance.ShowBannerAd();
        }

        private IEnumerator RunPopupDestroy()
        {
            Destroy(_victoryParticle);
            var elapsed = 0f;
            const float duration = 0.5f;
            while (_group != null && _group.alpha > 0f)
            {
                var p = Mathf.Clamp01(elapsed / duration);
                _group.alpha = Mathf.Lerp(1f, 0f, p);
                elapsed += Time.deltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(duration);
            FViewManager.Instance.DestroyInstance(this);
        }
    }
}
