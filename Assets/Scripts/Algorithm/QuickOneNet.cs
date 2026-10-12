using System.Collections.Generic;
using FCSharpExt;
using GameGrid;
using UnityEngine;
using View;
using Grid = GameGrid.Grid;

namespace Algorithm
{
    public static class QuickOneNet
    {
        private static int _col, _row;
        public static List<Grid> ShowGridPathTip(List<Grid> grids, int col, int row)
        {
            _col = col;
            _row = row;
            // 1. 预处理：按 ID 分组 (O(N))
            // key: spriteId/Sprite, value: List of Grid
            var groups = new Dictionary<int, List<Grid>>();
            foreach (var grid in grids)
            {
                if (!groups.ContainsKey(grid.Id))
                {
                    groups[grid.Id] = new List<Grid>();
                }
                groups[grid.Id].Add(grid);
            }

            //  随机打乱 ID 顺序，保证每次提示的图案不同
            var keys = new List<int>(groups.Keys);
            keys.Shuffle(); // 使用你现有的 FTools 扩展

            //  遍历分组查找可行解
            foreach (var key in keys)
            {
                var list = groups[key];
                // 如果该组少于2个，无法消除，跳过
                if (list.Count < 2) continue;
                
                // 组内乱序，保证同种图案也能提示不同的一对
                list.Shuffle(); 

                // 两两比较 (O(M^2), M为同种图案数量，通常很小)
                for (int i = 0; i < list.Count; i++)
                {
                    for (int j = i + 1; j < list.Count; j++)
                    {
                        var gridA = list[i];
                        var gridB = list[j];

                        // 几何检测
                        if (!CanConnect(gridA.x, gridA.y, gridB.x, gridB.y)) continue;
                        Debug.Log($"找到提示: {gridA.x},{gridA.y} -> {gridB.x},{gridB.y}");
                            
                        // HighlightGrid(gridA);
                        // HighlightGrid(gridB);
                            
                        return new List<Grid>() { gridA, gridB };
                    }
                }
            }

            Debug.Log("没有找到可以连接的格子 (死局)");
            return null;
        }

        #region High Performance Connection Check Algorithm

        /// <summary>
        /// 核心检测入口：判断两点是否能在 2 折之内连接
        /// </summary>
        private static bool CanConnect(int x1, int y1, int x2, int y2)
        {
            // 1. 直连 (0 折)
            if (IsLinkDirect(x1, y1, x2, y2)) return true;

            // 2. 一折 (1 折)
            if (IsLinkOneTurn(x1, y1, x2, y2)) return true;

            // 3. 两折 (2 折) - 只有前两者失败才跑这个，开销最大
            if (IsLinkTwoTurn(x1, y1, x2, y2)) return true;

            // 兜底，广度优先算法
            return CanConnectAnyPath(x1, y1, x2, y2);
        }
        /// <summary>
        /// 广度优先搜索 (BFS) 检测两点是否连通 (不限拐弯次数)
        /// </summary>
        private static bool CanConnectAnyPath(int startX, int startY, int targetX, int targetY)
        {
            // 基础检查
            if (startX == targetX && startY == targetY) return false;

            // 用于记录已访问的节点，防止死循环 (col * row)
            // 使用 bool[,] 或者是 HashSet<int> (key = x * 1000 + y)
            bool[,] visited = new bool[_col, _row];

            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            queue.Enqueue(new Vector2Int(startX, startY));
            visited[startX, startY] = true;

            // 上下左右四个方向
            int[] dirX = { 0, 0, 1, -1 };
            int[] dirY = { 1, -1, 0, 0 };

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                // 尝试向四个方向走
                for (int i = 0; i < 4; i++)
                {
                    int nextX = current.x + dirX[i];
                    int nextY = current.y + dirY[i];

                    // 1. 越界检查
                    if (nextX < 0 || nextX >= _col || nextY < 0 || nextY >= _row) continue;

                    // 2. 如果已经访问过，跳过
                    if (visited[nextX, nextY]) continue;

                    // 3. 找到目标了！
                    if (nextX == targetX && nextY == targetY) return true;

                    // 4. 通路检查：只有 Empty 才能走
                    // 注意：这里不需要检查 Target 是否 Empty，因为上面第3步已经拦截了
                    if (AutoLayoutGameView.Instance.GridStates[nextX, nextY].state == EGridState.Empty)
                    {
                        visited[nextX, nextY] = true;
                        queue.Enqueue(new Vector2Int(nextX, nextY));
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 检查两点是否在直线上且中间无障碍 (0折)
        /// </summary>
        private static bool IsLinkDirect(int x1, int y1, int x2, int y2)
        {
            // 必须同行或同列
            if (x1 != x2 && y1 != y2) return false;

            if (x1 == x2) // 同列检测
            {
                int min = Mathf.Min(y1, y2);
                int max = Mathf.Max(y1, y2);
                for (int y = min + 1; y < max; y++)
                {
                    if (!IsPassable(x1, y)) return false;
                }
                return true;
            }
            else // 同行检测
            {
                int min = Mathf.Min(x1, x2);
                int max = Mathf.Max(x1, x2);
                for (int x = min + 1; x < max; x++)
                {
                    if (!IsPassable(x, y1)) return false;
                }
                return true;
            }
        }

        /// <summary>
        /// 检查一折连接 (L型)
        /// </summary>
        private static bool IsLinkOneTurn(int x1, int y1, int x2, int y2)
        {
            // 拐点1: (x1, y2)
            // 如果拐点是空的(或就是终点)，并且 两条路径都通
            if (IsPassable(x1, y2) && IsLinkDirect(x1, y1, x1, y2) && IsLinkDirect(x1, y2, x2, y2))
            {
                return true;
            }

            // 拐点2: (x2, y1)
            if (IsPassable(x2, y1) && IsLinkDirect(x1, y1, x2, y1) && IsLinkDirect(x2, y1, x2, y2))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 检查两折连接 (U型或Z型)
        /// 优化思路：从 A 点向四周扫描空地，把所有能直线到达的空地当作“临时起点”，
        /// 如果“临时起点”能和 B 点进行“一折”或“直连”连接，则通过。
        /// </summary>
        private static bool IsLinkTwoTurn(int x1, int y1, int x2, int y2)
        {
            // 扫描方向：水平扫描
            // 从 x1, y1 向左和向右延伸，寻找所有可达的空点 (i, y1)
            for (int i = 0; i < _col; i++)
            {
                if (i == x1) continue; 
                
                // 如果这个水平扫描点 (i, y1) 本身不可通过，且不是两端点之一（逻辑上不需要判断端点，因为i循环）
                // 优化：我们不需要判断所有 i，只需要从 x1 向左搜直到撞墙，向右搜直到撞墙
                // 这里为了代码简洁直接遍历列，配合 IsLinkDirect 其实就是扫描
                
                // 如果 A 能直线走到 (i, y1) 且 (i, y1) 是空的
                if (IsPassable(i, y1) && IsLinkDirect(x1, y1, i, y1))
                {
                    // 检查这个中转点能否通过 0折或1折 连到 B (x2, y2)
                    // 因为我们是在同一行移动出来的，所以只需要检测垂直方向的一折连接即可连到 B
                    // 即：(i, y1) -> (i, y2) -> (x2, y2)
                    // 其中 (i, y1)->(i, y2) 是直线检测
                    // (i, y2)->(x2, y2) 是直线检测
                    // 结合起来就是：检测是否能一折连接
                    
                    if (IsLinkOneTurn(i, y1, x2, y2)) return true;
                    
                    // 特殊情况：如果 B 就在这一行 (y1 == y2)，且中间通畅，会被 IsLinkDirect 捕获，不会进这里
                    // 特殊情况：(i, y1) 能直连 B (i == x2)，会被 IsLinkOneTurn 捕获
                }
            }

            // 扫描方向：垂直扫描
            // 从 x1, y1 向上和向下延伸
            for (int j = 0; j < _row; j++)
            {
                if (j == y1) continue;
                
                if (IsPassable(x1, j) && IsLinkDirect(x1, y1, x1, j))
                {
                    if (IsLinkOneTurn(x1, j, x2, y2)) return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 辅助：判断坐标是否为空或者可通过
        /// </summary>
        private static bool IsPassable(int x, int y)
        {
            // 越界检查
            if (x < 0 || x >= _col || y < 0 || y >= _row) return false;
            
            // 只有 Empty 状态才算通路
            // 注意：GridState 里存的 state，如果是 Obstacle 或 Busy (且不是目标点自己) 都算阻挡
            // 但在这个提示逻辑里，传入的 x1,y1 和 x2,y2 本身肯定是 Busy，
            // 我们的路径检测逻辑是检查“中间”的点，所以直接判断 != Busy && != Obstacle
            return AutoLayoutGameView.Instance.GridStates[x, y].state == EGridState.Empty;
        }

        #endregion

    }
}