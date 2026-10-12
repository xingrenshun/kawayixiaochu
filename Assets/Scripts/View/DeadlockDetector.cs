using System.Collections.Generic;
using System.Linq;
using Algorithm;
using GameGrid;
using UnityEngine;
using Grid = GameGrid.Grid;

namespace View
{
    /// <summary>
    /// 死局检测器 - 检测是否存在可连接的格子对
    /// </summary>
    public static class DeadlockDetector
    {
        /// <summary>
        /// 检测是否存在至少一对可以连接的格子
        /// </summary>
        public static bool HasValidMove(List<Grid> grids, GridState[,] gridStates)
        {
            if (grids == null || grids.Count < 2) return false;

            // 按图标ID分组
            var groupedByIcon = grids.GroupBy(g => g.IconId).ToList();

            foreach (var group in groupedByIcon)
            {
                var sameIconGrids = group.ToList();
                if (sameIconGrids.Count < 2) continue;

                // 检查这组中是否有任意两个可以连接
                for (int i = 0; i < sameIconGrids.Count; i++)
                {
                    for (int j = i + 1; j < sameIconGrids.Count; j++)
                    {
                        var grid1 = sameIconGrids[i];
                        var grid2 = sameIconGrids[j];

                        var state1 = gridStates[grid1.x, grid1.y];
                        var state2 = gridStates[grid2.x, grid2.y];

                        var (success, _) = ShortestPath.FindPath(state1, state2, gridStates);
                        if (success)
                        {
                            return true; // 找到一对可连接的格子
                        }
                    }
                }
            }

            return false; // 没有任何可连接的格子对
        }

        /// <summary>
        /// 获取所有可连接的格子对（用于提示）
        /// </summary>
        public static List<(Grid, Grid)> GetValidPairs(List<Grid> grids, GridState[,] gridStates)
        {
            var validPairs = new List<(Grid, Grid)>();
            if (grids == null || grids.Count < 2) return validPairs;

            var groupedByIcon = grids.GroupBy(g => g.IconId).ToList();

            foreach (var group in groupedByIcon)
            {
                var sameIconGrids = group.ToList();
                if (sameIconGrids.Count < 2) continue;

                for (int i = 0; i < sameIconGrids.Count; i++)
                {
                    for (int j = i + 1; j < sameIconGrids.Count; j++)
                    {
                        var grid1 = sameIconGrids[i];
                        var grid2 = sameIconGrids[j];

                        var state1 = gridStates[grid1.x, grid1.y];
                        var state2 = gridStates[grid2.x, grid2.y];

                        var (success, _) = ShortestPath.FindPath(state1, state2, gridStates);
                        if (success)
                        {
                            validPairs.Add((grid1, grid2));
                        }
                    }
                }
            }

            return validPairs;
        }

        /// <summary>
        /// 计算死局风险评分（0-1，越高越危险）
        /// </summary>
        public static float CalculateDeadlockRisk(List<Grid> grids, GridState[,] gridStates)
        {
            if (grids == null || grids.Count < 2) return 1f;

            var validPairs = GetValidPairs(grids, gridStates);
            var totalPossiblePairs = grids.Count * (grids.Count - 1) / 2;

            if (totalPossiblePairs == 0) return 1f;

            // 可连接对数越少，风险越高
            var risk = 1f - (validPairs.Count / (float)totalPossiblePairs);

            return Mathf.Clamp01(risk);
        }
    }
}
