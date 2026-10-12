using FUnityCommon;
using UnityEngine;
using View;

namespace GameGrid
{
    /// <summary>
    /// 精准锁定道具：玩家点击翻开一张牌后，系统自动翻开另一张相同的牌进行消除，持续5秒
    /// </summary>
    public class PrecisionLockPowerUp : MemoryPowerUp
    {
        public const float ACTIVE_DURATION = 5f;
        private const float AUTO_TRIGGER_INTERVAL = 0.5f;
        private float _activatedTime;
        private float _lastAutoTriggerTime = -AUTO_TRIGGER_INTERVAL;

        public PrecisionLockPowerUp() : base("精准锁定", 0f)
        {
        }

        protected override void OnUse()
        {
            _activatedTime = Time.time;
            _lastAutoTriggerTime = Time.time - AUTO_TRIGGER_INTERVAL;

            var view = MemoryGameView.Instance;
            if (view == null) return;

            view.SetPrecisionLockActive(true);
            view.StartPrecisionLockDurationUi(ACTIVE_DURATION);

            // 5秒后自动失效f
            FAsync.Instance.InvokeDelay(() =>
            {
                Deactivate();
            }, ACTIVE_DURATION);

            // 如果激活时玩家已经翻开了一张牌，立即补触发一次自动配对
            if (view.selectedGrids.Count == 1)
            {
                var selected = view.selectedGrids[0];
                if (selected != null && selected.IsAlive())
                {
                    OnPlayerFlipCard(selected);
                }
            }
        }

        public override void Update()
        {
            if (!isActive) return;

            // 检查是否超时
            if (Time.time - _activatedTime >= ACTIVE_DURATION)
            {
                Deactivate();
            }
        }

        public override void Deactivate()
        {
            base.Deactivate();
            var view = MemoryGameView.Instance;
            if (view != null)
            {
                view.SetPrecisionLockActive(false);
                view.StopPrecisionLockDurationUi();
                view.ShowStaminaUi();
            }
        }

        /// <summary>
        /// 当玩家翻开一张牌时，自动找到并翻开匹配的牌
        /// </summary>
        public void OnPlayerFlipCard(MGrid flippedGrid)
        {
            if (!isActive || flippedGrid == null || !flippedGrid.IsAlive()) return;
            if (Time.time - _lastAutoTriggerTime < AUTO_TRIGGER_INTERVAL) return;

            var view = MemoryGameView.Instance;
            if (view == null) return;

            // 找到另一张相同的牌
            var matchingGrid = view.FindMatchingGrid(flippedGrid);
            if (matchingGrid == null) return;

            _lastAutoTriggerTime = Time.time;
            view.LockInput(true);

            if (!matchingGrid.IsAlive() || !matchingGrid.IsFlipped())
            {
                view.LockInput(false);
                return;
            }

            // 立刻翻开匹配牌
            matchingGrid.FlipCard(view.cardBack, () =>
            {
                var currentView = MemoryGameView.Instance;
                if (currentView == null)
                {
                    view.LockInput(false);
                    return;
                }
                if (matchingGrid != null && matchingGrid.IsAlive())
                {
                    currentView.DoSelected(matchingGrid);
                    return;
                }

                currentView.LockInput(false);
            });
        }
    }
}
