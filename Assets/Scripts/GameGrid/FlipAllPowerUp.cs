using System.Collections.Generic;
using DG.Tweening;
using FTools.Helpers;
using FUnityCommon;
using UnityEngine;
using View;

namespace GameGrid
{
    /// <summary>
    /// 全部翻转道具：场上所有牌翻转3秒后自动归位，期间锁定输入
    /// </summary>
    public class FlipAllPowerUp : MemoryPowerUp
    {
        public const float FLIP_DURATION = 5f;
        private List<MGrid> _flippedGrids = new List<MGrid>();

        public FlipAllPowerUp() : base("全部翻转", 0f)
        {
        }

        protected override void OnUse()
        {
            // Debug.Log("FlipAllPowerUp.OnUse 被调用");
            var view = MemoryGameView.Instance;
            if (view == null)
            {
                Debug.LogError("MemoryGameView.Instance 为 null");
                return;
            }

            view.LockInput(true);
            view.PlayFlipAllCountdown(FLIP_DURATION);
            _flippedGrids.Clear();

            // 收集所有未消除的格子
            var allGrids = view.GetAllActiveGrids();
            // Debug.Log($"找到 {allGrids.Count} 个活动格子");

            // 随机延迟翻转，增加视觉效果
            var maxDelay = 0f;
            foreach (var grid in allGrids)
            {
                if (grid == null) continue;
                var delay = Random.Range(0f, 0.3f);
                maxDelay = Mathf.Max(maxDelay, delay);

                FAsync.Instance.InvokeDelay(() =>
                {
                    if (!grid.IsAlive() || !grid.IsFlipped()) return;
                    // 如果是背面（问号），翻到正面
                    // Debug.Log($"翻转格子 ({grid.x}, {grid.y})");
                    grid.FlipCard(view.cardBack);
                    _flippedGrids.Add(grid);
                }, delay);
            }

            // Debug.Log($"将在 {maxDelay + FLIP_DURATION} 秒后翻回");

            // 3秒后翻回去
            FAsync.Instance.InvokeDelay(() =>
            {
                // Debug.Log("开始翻回卡片");
                var flipBackDelay = 0f;
                foreach (var grid in _flippedGrids)
                {
                    if (grid == null) continue;
                    var delay = Random.Range(0f, 0.3f);
                    flipBackDelay = Mathf.Max(flipBackDelay, delay);

                    FAsync.Instance.InvokeDelay(() =>
                    {
                        if (grid.IsAlive() && !grid.IsFlipped())
                        {
                            // 翻回背面
                            grid.FlipCard(view.cardBack);
                        }
                    }, delay);
                }

                // 等待所有卡片翻回后解锁输入
                FAsync.Instance.InvokeDelay(() =>
                {
                    Debug.Log("解锁输入");
                    view.LockInput(false);
                    Deactivate();
                }, flipBackDelay + 0.35f);

            }, maxDelay + FLIP_DURATION);
        }
    }
}
