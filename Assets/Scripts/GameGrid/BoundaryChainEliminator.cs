using System.Collections.Generic;
using DG.Tweening;
using FTools.Helpers;
using FUnityCommon;
using UnityEngine;
using View;

namespace GameGrid
{
    /// <summary>
    /// 边界连锁消除管理器 - 当边界元素被消除时，检查对立边界是否有相同元素
    /// </summary>
    public class BoundaryChainEliminator
    {
        private readonly AutoLayoutGameView _view;
        private bool _isProcessing;

        public BoundaryChainEliminator(AutoLayoutGameView view)
        {
            _view = view;
        }

        /// <summary>
        /// 检查并执行边界连锁消除
        /// </summary>
        public void CheckBoundaryChain(Grid eliminatedGrid)
        {
            if (_isProcessing) return;
            if (!IsOnBoundary(eliminatedGrid)) return;

            Debug.Log($"检测到边界消除: ({eliminatedGrid.x}, {eliminatedGrid.y})");

            // 找到对立边界的相同元素
            var oppositeGrid = FindOppositeGrid(eliminatedGrid);
            if (oppositeGrid == null)
            {
                Debug.Log("对立边界没有相同元素");
                return;
            }

            Debug.Log($"找到对立边界相同元素: ({oppositeGrid.x}, {oppositeGrid.y})");

            // 收集两者之间的所有元素
            var gridsToEliminate = CollectGridsBetween(eliminatedGrid, oppositeGrid);
            if (gridsToEliminate.Count == 0)
            {
                Debug.Log("两者之间没有元素");
                return;
            }

            Debug.Log($"边界连锁消除: 将消除 {gridsToEliminate.Count} 个元素");

            // 执行连锁消除
            ExecuteChainElimination(gridsToEliminate);
        }

        private bool IsOnBoundary(Grid grid)
        {
            var gridStates = _view.GridStates;
            int maxX = gridStates.GetLength(0) - 1;
            int maxY = gridStates.GetLength(1) - 1;

            return grid.x == 0 || grid.x == maxX || grid.y == 0 || grid.y == maxY;
        }

        private Grid FindOppositeGrid(Grid grid)
        {
            var gridStates = _view.GridStates;
            int maxX = gridStates.GetLength(0) - 1;
            int maxY = gridStates.GetLength(1) - 1;

            int oppositeX = grid.x;
            int oppositeY = grid.y;

            // 计算对立边界坐标
            if (grid.x == 0) oppositeX = maxX;
            else if (grid.x == maxX) oppositeX = 0;

            if (grid.y == 0) oppositeY = maxY;
            else if (grid.y == maxY) oppositeY = 0;

            // 查找对立位置的格子
            foreach (var g in _view.GameGrids)
            {
                if (g.x == oppositeX && g.y == oppositeY && g.IconId == grid.IconId)
                {
                    return g;
                }
            }

            return null;
        }

        private List<Grid> CollectGridsBetween(Grid grid1, Grid grid2)
        {
            var result = new List<Grid>();

            // 判断是水平还是垂直方向
            bool isHorizontal = grid1.y == grid2.y;
            bool isVertical = grid1.x == grid2.x;

            if (isHorizontal)
            {
                // 水平方向，收集同一行的所有元素
                int minX = Mathf.Min(grid1.x, grid2.x);
                int maxX = Mathf.Max(grid1.x, grid2.x);

                foreach (var g in _view.GameGrids)
                {
                    if (g.y == grid1.y && g.x > minX && g.x < maxX)
                    {
                        result.Add(g);
                    }
                }
            }
            else if (isVertical)
            {
                // 垂直方向，收集同一列的所有元素
                int minY = Mathf.Min(grid1.y, grid2.y);
                int maxY = Mathf.Max(grid1.y, grid2.y);

                foreach (var g in _view.GameGrids)
                {
                    if (g.x == grid1.x && g.y > minY && g.y < maxY)
                    {
                        result.Add(g);
                    }
                }
            }
            else
            {
                // 对角线方向，收集对角线上的所有元素
                int minX = Mathf.Min(grid1.x, grid2.x);
                int maxX = Mathf.Max(grid1.x, grid2.x);
                int minY = Mathf.Min(grid1.y, grid2.y);
                int maxY = Mathf.Max(grid1.y, grid2.y);

                // 计算斜率
                float slope = (float)(grid2.y - grid1.y) / (grid2.x - grid1.x);

                foreach (var g in _view.GameGrids)
                {
                    if (g.x > minX && g.x < maxX && g.y > minY && g.y < maxY)
                    {
                        // 检查是否在对角线上
                        float expectedY = grid1.y + slope * (g.x - grid1.x);
                        if (Mathf.Abs(g.y - expectedY) < 0.5f)
                        {
                            result.Add(g);
                        }
                    }
                }
            }

            return result;
        }

        private void ExecuteChainElimination(List<Grid> grids)
        {
            _isProcessing = true;

            // 锁定输入
            _view.SetInputLocked(true);

            // 播放音效
            FAudioCore.Instance.PlaySfx("on-score");

            // 依次消除，带延迟效果
            for (int i = 0; i < grids.Count; i++)
            {
                var grid = grids[i];
                var delay = i * 0.05f;

                FAsync.Instance.InvokeDelay(() =>
                {
                    if (grid != null && grid.obj != null)
                    {
                        // 闪烁效果
                        var seq = DOTween.Sequence();
                        seq.Append(grid.Rt.DOScale(1.3f, 0.15f).SetEase(Ease.OutQuad));
                        seq.Append(grid.Rt.DOScale(0f, 0.2f).SetEase(Ease.InBack));
                        seq.OnComplete(() =>
                        {
                            _view.RemoveGameGrid(grid, true);
                            Runtime.Instance.AddOrUpdateCollectionItem(grid.IconId);
                        });
                    }
                }, delay);
            }

            // 等待所有消除完成后解锁
            FAsync.Instance.InvokeDelay(() =>
            {
                _isProcessing = false;
                _view.SetInputLocked(false);
                Debug.Log("边界连锁消除完成");
            }, grids.Count * 0.05f + 0.5f);
        }

        public bool IsProcessing()
        {
            return _isProcessing;
        }
    }
}
