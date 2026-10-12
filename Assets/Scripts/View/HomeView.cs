using System;
using System.Collections;
using System.Collections.Generic;
using Effect;
using FTools.Cache;
using FTools.Helpers;
using FTools.Platforms;
using FUnityAttribute.RuntimeAttributes;
using FUnityCommon;
using FUnityUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace View
{
    [FViewLoader("View/HomeView")]
    public class HomeView : FUiBase<HomeView>
    {
        [FuiAutowire] private Image _staminaIcon;
        [FuiAutowire] private Image _playerAvatar;
        [FuiAutowire] private TMP_Text _rankNum;
        [FuiAutowire] private TMP_Text _staminaTimeText;
        [FuiAutowire] private TMP_Text _staminaValueText;
        [FuiAutowire] private RectTransform _particleSpawnRoot;
        [FuiAutowire("Button-Chest-Gift-Opening")] public RectTransform chestGiftOpeningButton;
        [FGameObject("Eye-lock-cover")] private GameObject _eyeLockCover;


        private bool _isTapedStart;

        [FuiAutowire("Button-Mode1")] private RectTransform _classicModeButton;
        [FuiAutowire("Button-Mode2")] private Button _eyeModeButton;
        private Action<int> _onStaminaChanged;
        private Action<TimeSpan> _onRecoveryChanged;



        [FOnClick("Button-Game-Club")]
        private void OpenGameClub()
        {
            FPlatformCore.GotoGameClub();
        }

        [FOnClick("PopStamina")]
        private void OpenStamina()
        {
            VideoStaminaView.ShowView();
        }

        public override void OnSetup()
        {
            // 
            Runtime.Instance.isGameOver = true;
            FAds.Instance.HideBannerAd();
            var level = PlayerNetData.Instance.PlayerLevel;
            _eyeLockCover.SetActive(level < 5);
            if (PlayerLocalData.Instance.IsUserShared)
            {
                chestGiftOpeningButton.gameObject.SetActive(false);
            }
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
            
            
            UpdateUserAvatar();
            
        }

        [FOnClick("player-avatar-btn")]
        private void OpenAuthView()
        {
            // 授权面板已关闭，点击头像不再弹出授权弹窗
            if (!WxProfileAuthView.EnableAuthPanel) return;

            var localCache = FCacheManager.GetCache(ECacheType.Local);
            var isAuthed = localCache.Load("IsAuthed", false);
            if (isAuthed) return;
            var view = WxProfileAuthView.ShowView();
            view.onDestroyCallback += UpdateUserAvatar;
        }
        private void UpdateUserAvatar()
        {
            var rank = FCacheManager.GetCache(ECacheType.Local).Load("LocalHistoryRank", -1);
            _rankNum.text = rank is -1 or > 99 ? "99+" : rank.ToString();
            var localCache = FCacheManager.GetCache(ECacheType.Local);
            var isAuthed = localCache.Load("IsAuthed", false);
            if (!isAuthed) return;
            var avatar = localCache.Load<string>("avatarUrl");
            if (PlayerNetData.Instance.avatar != null)
            {
                _playerAvatar.sprite = PlayerNetData.Instance.avatar;
            }
            else
            {
                FUnityNetworkImageLoader.LoadImage(avatar, icon =>
                {
                    _playerAvatar.sprite = icon;
                    PlayerNetData.Instance.avatar = icon;
                });
            }
        }

        public void AwareStamina(int stamina)
        {
            FAsync.Instance.StartRoutine(IconMoveVfx.StartIconCircleMove(_staminaIcon.gameObject,
                _staminaIcon.transform,
                stamina, () => { PlayerLocalData.Instance.PlayerStamina++; }, null, _particleSpawnRoot));
        }

        private void OnDisable()
        {
            var data = PlayerLocalData.Instance;
            data.OnStaminaChanged -= _onStaminaChanged;
            data.OnNextRecoveryTimeChanged -= _onRecoveryChanged;
        }

        [FOnClick("Button-Chest-Gift-Opening")]
        private void OpenShareView()
        {
            FAudioCore.Instance.PlaySfx("tap");
            FViewManager.Instance.Show<ShareView>();
        }


        [FOnClick("Button-Mode1")]
        public void StartGameMode1()
        {
            FAudioCore.Instance.PlaySfx("tap");
            CheckStamina(_classicModeButton.transform, StaminaCheck, () =>
            {
                // 1 普通模式
                Runtime.mode = 0;
                Runtime.Instance.StartGame();
                FViewManager.Instance.DestroyInstance(this);
                PlayerLocalData.Instance.PlayerStamina--;
            });
        }

        [FOnClick("Button-Mode2")]
        public void StartGameMode2()
        {
            FAudioCore.Instance.PlaySfx("tap");
            CheckStamina(_eyeModeButton.transform, () =>
            {
                if (PlayerNetData.Instance.PlayerLevel >= 5) return StaminaCheck();
                AlertTipView.Instance.FadeIn("通关第5关解锁");
                return false;
            }, () =>
            {
                // 1 眼力模式
                Runtime.mode = 1;
                Runtime.Instance.StartGame();
                FViewManager.Instance.DestroyInstance(this);
                PlayerLocalData.Instance.PlayerStamina--;
            });
        }

        [FOnClick("Button-Mode3")]
        public void StartMemoryGame()
        {
            FAudioCore.Instance.PlaySfx("tap");
            CheckStamina(_eyeModeButton.transform, StaminaCheck, () =>
            {
                // PlayerLocalData.Instance.PlayerStamina--;
                MemoryGameView.ShowView();
                FViewManager.Instance.DestroyInstance(this);
            });
        }

        private bool StaminaCheck()
        {
            var staminaIsEnough = PlayerLocalData.Instance.PlayerStamina > 0;
            if (!staminaIsEnough)
            {
                AlertTipView.Instance.FadeIn("没有体力啦~");
            }

            return staminaIsEnough;
        }

        [FOnClick("Button-Rank")]
        public void OpenRank()
        {
            FAudioCore.Instance.PlaySfx("tap");
            FViewManager.Instance.Show<RankingView>();
            FViewManager.Instance.DestroyInstance(this);
        }


        private void CheckStamina(Transform target, Func<bool> preCheck, Action callback)
        {
            if (!preCheck() || _isTapedStart) return;
            _isTapedStart = true;
            FAsync.Instance.StartRoutine(IconMoveVfx.StartIconCircleMove(_staminaIcon.gameObject, target,
                10, null, callback));
        }

        [FOnClick("Button-Settings")]
        public void OpenSettings()
        {
            FAudioCore.Instance.PlaySfx("tap");
            FViewManager.Instance.Show<MenuSettingsView>();
        }
    }
}
