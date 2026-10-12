using System;
using System.Collections;
using System.Collections.Generic;
using FCSharpExt;
using FTools.Cache;
using FTools.Helpers;
using FTools.Network;
using FTools.Platforms;
using FUnityAttribute.RuntimeAttributes;
using FUnityUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace View
{
    [FViewLoader("View/GameWinView")]
    public class GameWinView : FUiBase<GameWinView>
    {
        private CanvasGroup _group;


        [FuiAutowire] private TMP_Text _currentScore;

        [FuiAutowire("Avatar-Icon")] private Image _avatar;
        [FuiAutowire] private TMP_Text _historyScore;
        [FuiAutowire("history-cup-icon")] private Image _historyCupIcon;

        [FGameObject("history-cup-loading-icon")]
        private GameObject _historyCupLoadingIcon;
        
        
        [FuiAutowire("WinTitle")] private TMP_Text _title;

        [FGameObject("Win-Popup")] private GameObject _uiPanel;


        [FuiAutowire("total-rank-change-text")]
        private TMP_Text _playerRankNum;


        private Animator _rootAnimator;

        // victory-particle
        [FGameObject("victory-particle")] private GameObject _victoryParticle;


        private ICache _localCache, _netCache;


        private const string LOCAL_HISTORY_SCORE_KEY = "LocalHistoryScore";
        private const string LOCAL_RANK_KEY = "LocalHistoryRank";

        public void UpdateTitle(string content)
        {
            _title.text = content;
        }

        public override void OnSetup()
        {
            if (Runtime.mode == 0)
            {
                PlayerNetData.Instance.PlayerLevel++;
            }

            FAds.Instance.HideBannerAd();
            _localCache = FCacheManager.GetCache(ECacheType.Local);
            _netCache = FCacheManager.GetCache(ECacheType.Net);

            var currentScore = Mathf.CeilToInt(AutoLayoutGameView.Instance!.CurrentScore);
            var localHistoryScore = _localCache.Load(LOCAL_HISTORY_SCORE_KEY, 0);

            var finalHistoryScore = Mathf.Max(currentScore, localHistoryScore);
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

            _localCache.Save(LOCAL_HISTORY_SCORE_KEY, finalHistoryScore);

            _currentScore.text = currentScore.ToString();
            _historyScore.text = finalHistoryScore.ToString();

            _group = GetComponent<CanvasGroup>();
            _rootAnimator = _uiPanel.GetComponent<Animator>();

            if (PlayerNetData.Instance.avatar != null)
            {
                _avatar.sprite = PlayerNetData.Instance.avatar;
            }
#if PT_dy
            // 上报分数到抖音小游戏排行榜（复用 AdSDK 里的 PaiHangBang 封装）
            PaiHangBang.Instance?.UploadScore(finalHistoryScore);
#endif
            // 提交成绩
            FRequest.Instance.Post<bool>("ranking/submit", new Dictionary<string, object>
            {
                { "board", "total-ranking" },
#if UNITY_EDITOR
                {
                    "openId",
                    FCacheManager.GetCache(ECacheType.Local).Load<string>("openId", "")
                },
#else
                { "openId", FCacheManager.GetCache(ECacheType.Local).Load<string>("openId") },
#endif
                { "score", currentScore }
            }, res =>
            {
                if (res.code != 200)
                {
                    AlertTipView.Instance!.FadeIn("网络错误！上报成绩失败,\n系统会在下一次结算时重试.");
                    return;
                }

                GetMyRankInfo();
            });
        }

        private void GetMyRankInfo()
        {
            FRequest.Instance.Post<MyScoreData>("ranking/me", new Dictionary<string, object>()
            {
                { "board", "total-ranking" },
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
                    AlertTipView.Instance!.FadeIn("拉取最新数据失败,请稍后再试");
                    return;
                }

                _playerRankNum.text = res.data.rank.ToString();
                _historyScore.text = res.data.score.ToThousands();
                FCacheManager.GetCache(ECacheType.Local).Save(LOCAL_HISTORY_SCORE_KEY, res.data.score);
                FCacheManager.GetCache(ECacheType.Local).Save(LOCAL_RANK_KEY, res.data.rank);
            });
        }

        [FOnClick("button-rank")]
        private void ToTankView()
        {
            FViewManager.Instance.DestroyView<AutoLayoutGameView>();
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

            FViewManager.Instance.DestroyView<AutoLayoutGameView>();
            FViewManager.Instance.Show<HomeView>();
            StartCoroutine(RunPopupDestroy(() => { }));
        }


        [FOnClick("continue-btn")]
        private void ContinueGame()
        {
            FAudioCore.Instance.PlaySfx("tap");
            if (_rootAnimator.GetCurrentAnimatorStateInfo(0).IsName("Open"))
            {
                _rootAnimator.Play("Close");
            }

            var view = AutoLayoutGameView.Instance;
            view.NextLevel();
            StartCoroutine(RunPopupDestroy());
            FAds.Instance.ShowBannerAd();
        }

        private IEnumerator RunPopupDestroy(Action callback = null)
        {
            Destroy(_victoryParticle);
            var alpha = _group.alpha;
            var elpased = 0f;
            const float duration = 0.5f;
            while (alpha > 0)
            {
                var p = elpased / duration;
                alpha = Mathf.Lerp(1, 0, p);
                _group.alpha = alpha;
                elpased += Time.deltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(duration);
            callback?.Invoke();
            FViewManager.Instance.DestroyInstance(this);
        }
    }
}