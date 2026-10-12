using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using FTools.Helpers;
using FUnityCommon;
using UnityEngine;
using View;

namespace GameGrid
{
    /// <summary>
    /// 网格变换类型
    /// </summary>
    public enum GridTransformType
    {
        Rotate,      // 旋转
        Mirror,      // 镜像翻转
        Wave         // 波浪移动
    }

    /// <summary>
    /// 网格变换管理器 - 管理多种网格变换效果
    /// </summary>
    public class GridTransformManager
    {
        private readonly AutoLayoutGameView _view;
        private int _currentLevel;
        private bool _isTransforming;
        private const float TRANSFORM_CHANCE = 0.3f; // 30%概率触发变换

        public GridTransformManager(AutoLayoutGameView view)
        {
            _view = view;
            _currentLevel = 1;
        }

        public void SetLevel(int level)
        {
            _currentLevel = level;
        }

        public bool ShouldTransform()
        {
            // 第一关不变换，第二关及以后30%概率变换
            return _currentLevel >= 2 && Random.value < TRANSFORM_CHANCE;
        }

        public bool TransformGridsAfterElimination()
        {
            if (!ShouldTransform() || _isTransforming) return false;

            _isTransforming = true;

            // 随机选择一种变换类型
            var transformType = (GridTransformType)Random.Range(0, 3);
            Debug.Log($"触发网格变换 - 类型: {transformType}, 关卡: {_currentLevel}");

            // 锁定输入
            _view.SetInputLocked(true);

            // 播放音效
            FAudioCore.Instance.PlaySfx("tap");

            // 收集所有活动的游戏格子
            var activeGrids = new List<Grid>(_view.GameGrids);
            if (activeGrids.Count == 0)
            {
                _isTransforming = false;
                _view.SetInputLocked(false);
                return false;
            }

            // 检测变换前是否已经是死局
            var hasValidMoveBefore = DeadlockDetector.HasValidMove(activeGrids, _view.GridStates);
            Debug.Log($"变换前可连接: {hasValidMoveBefore}");

            // 执行对应的变换
            switch (transformType)
            {
                case GridTransformType.Rotate:
                    PerformRotation(activeGrids);
                    break;
                case GridTransformType.Mirror:
                    PerformMirror(activeGrids);
                    break;
                case GridTransformType.Wave:
                    PerformWave(activeGrids);
                    break;
            }

            return true;
        }

        #region 旋转变换

        private void PerformRotation(List<Grid> grids)
        {
            Debug.Log("执行旋转变换");
            bool clockwise = Random.value > 0.5f;

            // 计算网格边界和中心
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;

            foreach (var grid in grids)
            {
                minX = Mathf.Min(minX, grid.x);
                maxX = Mathf.Max(maxX, grid.x);
                minY = Mathf.Min(minY, grid.y);
                maxY = Mathf.Max(maxY, grid.y);
            }

            int centerX = (minX + maxX) / 2;
            int centerY = (minY + maxY) / 2;

            var newPositions = new Dictionary<Grid, (int x, int y)>();
            var occupiedPositions = new HashSet<(int x, int y)>();

            foreach (var grid in grids)
            {
                int relX = grid.x - centerX;
                int relY = grid.y - centerY;

                int newRelX, newRelY;
                if (clockwise)
                {
                    newRelX = relY;
                    newRelY = -relX;
                }
                else
                {
                    newRelX = -relY;
                    newRelY = relX;
                }

                int newX = newRelX + centerX;
                int newY = newRelY + centerY;

                if (_view.IsValidPosition(newX, newY) && !occupiedPositions.Contains((newX, newY)))
                {
                    newPositions[grid] = (newX, newY);
                    occupiedPositions.Add((newX, newY));
                }
                else
                {
                    var nearestPos = FindNearestValidPosition(newX, newY, occupiedPositions);
                    if (nearestPos.HasValue)
                    {
                        newPositions[grid] = nearestPos.Value;
                        occupiedPositions.Add(nearestPos.Value);
                    }
                    else
                    {
                        newPositions[grid] = (grid.x, grid.y);
                        occupiedPositions.Add((grid.x, grid.y));
                    }
                }
            }

            AnimateTransform(newPositions, GridTransformType.Rotate);
        }

        #endregion

        #region 镜像翻转

        private void PerformMirror(List<Grid> grids)
        {
            Debug.Log("执行镜像翻转");

            // 随机选择水平或垂直翻转
            bool horizontal = Random.value > 0.5f;
            Debug.Log($"翻转方向: {(horizontal ? "水平" : "垂直")}");

            // 计算网格边界
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;

            foreach (var grid in grids)
            {
                minX = Mathf.Min(minX, grid.x);
                maxX = Mathf.Max(maxX, grid.x);
                minY = Mathf.Min(minY, grid.y);
                maxY = Mathf.Max(maxY, grid.y);
            }

            int centerX = (minX + maxX) / 2;
            int centerY = (minY + maxY) / 2;

            var newPositions = new Dictionary<Grid, (int x, int y)>();
            var occupiedPositions = new HashSet<(int x, int y)>();

            foreach (var grid in grids)
            {
                int newX, newY;

                if (horizontal)
                {
                    // 水平翻转：关于垂直中线对称
                    newX = 2 * centerX - grid.x;
                    newY = grid.y;
                }
                else
                {
                    // 垂直翻转：关于水平中线对称
                    newX = grid.x;
                    newY = 2 * centerY - grid.y;
                }

                if (_view.IsValidPosition(newX, newY) && !occupiedPositions.Contains((newX, newY)))
                {
                    newPositions[grid] = (newX, newY);
                    occupiedPositions.Add((newX, newY));
                }
                else
                {
                    var nearestPos = FindNearestValidPosition(newX, newY, occupiedPositions);
                    if (nearestPos.HasValue)
                    {
                        newPositions[grid] = nearestPos.Value;
                        occupiedPositions.Add(nearestPos.Value);
                    }
                    else
                    {
                        newPositions[grid] = (grid.x, grid.y);
                        occupiedPositions.Add((grid.x, grid.y));
                    }
                }
            }

            AnimateTransform(newPositions, GridTransformType.Mirror);
        }

        #endregion

        #region 波浪移动

        private void PerformWave(List<Grid> grids)
        {
            Debug.Log("执行波浪移动");

            // 随机选择波浪方向
            bool horizontal = Random.value > 0.5f;
            Debug.Log($"波浪方向: {(horizontal ? "水平" : "垂直")}");

            var newPositions = new Dictionary<Grid, (int x, int y)>();
            var occupiedPositions = new HashSet<(int x, int y)>();

            // 按行或列分组
            var groups = new Dictionary<int, List<Grid>>();

            foreach (var grid in grids)
            {
                int key = horizontal ? grid.y : grid.x;
                if (!groups.ContainsKey(key))
                {
                    groups[key] = new List<Grid>();
                }
                groups[key].Add(grid);
            }

            // 每组移动不同的距离，创造波浪效果
            foreach (var kvp in groups)
            {
                int groupKey = kvp.Key;
                var groupGrids = kvp.Value;

                // 根据组的位置计算移动距离（1-3格）
                int offset = (groupKey % 3) + 1;
                if (Random.value > 0.5f) offset = -offset;

                foreach (var grid in groupGrids)
                {
                    int newX = grid.x;
                    int newY = grid.y;

                    if (horizontal)
                    {
                        newX += offset;
                    }
                    else
                    {
                        newY += offset;
                    }

                    // 边界循环
                    var gridStates = _view.GridStates;
                    int maxX = gridStates.GetLength(0) - 1;
                    int maxY = gridStates.GetLength(1) - 1;

                    if (newX < 0) newX = maxX;
                    if (newX > maxX) newX = 0;
                    if (newY < 0) newY = maxY;
                    if (newY > maxY) newY = 0;

                    if (_view.IsValidPosition(newX, newY) && !occupiedPositions.Contains((newX, newY)))
                    {
                        newPositions[grid] = (newX, newY);
                        occupiedPositions.Add((newX, newY));
                    }
                    else
                    {
                        var nearestPos = FindNearestValidPosition(newX, newY, occupiedPositions);
                        if (nearestPos.HasValue)
                        {
                            newPositions[grid] = nearestPos.Value;
                            occupiedPositions.Add(nearestPos.Value);
                        }
                        else
                        {
                            newPositions[grid] = (grid.x, grid.y);
                            occupiedPositions.Add((grid.x, grid.y));
                        }
                    }
                }
            }

            AnimateTransform(newPositions, GridTransformType.Wave);
        }

        #endregion

        #region 通用动画

        private void AnimateTransform(Dictionary<Grid, (int x, int y)> newPositions, GridTransformType transformType)
        {
            int completedCount = 0;
            int totalCount = newPositions.Count;

            Debug.Log($"开始批量更新 {totalCount} 个格子的状态");

            // 先批量更新所有格子的状态，避免闭包问题
            var updateList = new List<(Grid grid, int oldX, int oldY, int newX, int newY)>();

            foreach (var kvp in newPositions)
            {
                var grid = kvp.Key;
                var (newX, newY) = kvp.Value;

                if (grid.x != newX || grid.y != newY)
                {
                    // 保存旧坐标和新坐标
                    updateList.Add((grid, grid.x, grid.y, newX, newY));
                }
            }

            Debug.Log($"需要移动的格子数: {updateList.Count}");

            // 两阶段提交，避免交换位置时把其他格子的新位置误清空：
            // 1) 统一清空所有旧位置
            // 2) 更新所有 Grid 的逻辑坐标
            // 3) 统一占用所有新位置
            foreach (var item in updateList)
            {
                _view.UpdateGridState(item.oldX, item.oldY, EGridState.Empty);
            }

            foreach (var item in updateList)
            {
                item.grid.x = item.newX;
                item.grid.y = item.newY;
            }

            foreach (var item in updateList)
            {
                _view.UpdateGridState(item.newX, item.newY, EGridState.Busy);
            }

            Debug.Log("状态批量更新完成");

            // 按距离中心的距离排序（使用新坐标）
            var sortedGrids = newPositions.Keys.ToList();
            var centerX = sortedGrids.Average(g => g.x);
            var centerY = sortedGrids.Average(g => g.y);

            sortedGrids.Sort((a, b) =>
            {
                var distA = Mathf.Sqrt(Mathf.Pow((float)(a.x - centerX), 2) + Mathf.Pow((float)(a.y - centerY), 2));
                var distB = Mathf.Sqrt(Mathf.Pow((float)(b.x - centerX), 2) + Mathf.Pow((float)(b.y - centerY), 2));
                return distA.CompareTo(distB);
            });

            // 根据变换类型决定动画顺序
            if (transformType == GridTransformType.Wave || Random.value > 0.5f)
            {
                sortedGrids.Reverse();
            }

            // 执行动画（此时坐标和状态都已更新）
            for (int i = 0; i < sortedGrids.Count; i++)
            {
                var grid = sortedGrids[i];
                var (newX, newY) = newPositions[grid];
                var delay = i * 0.015f;

                FAsync.Instance.InvokeDelay(() =>
                {
                    AnimateSingleGridVisual(grid, newX, newY, transformType, () =>
                    {
                        completedCount++;
                        if (completedCount >= totalCount)
                        {
                            OnTransformComplete();
                        }
                    });
                }, delay);
            }
        }

        private void AnimateSingleGridVisual(Grid grid, int newX, int newY, GridTransformType transformType, System.Action onComplete)
        {
            // 此方法只负责视觉动画，不更新逻辑状态（已经在 AnimateTransform 中更新了）
            var targetPos = new Vector2(_view.GetXPosition(newX), _view.GetYPosition(newY));
            var startPos = grid.Rt.anchoredPosition;

            var ab = targetPos - startPos;
            var mid = (startPos + targetPos) * 0.5f;
            var dir = (Vector2)Vector3.Cross(ab, Vector3.forward).normalized;
            var randomOffset = Random.Range(150f, 250f);
            var control = mid + dir * randomOffset;

            // 根据变换类型调整动画效果
            float rotationAmount = 0f;
            float scaleAmount = 1.1f;

            switch (transformType)
            {
                case GridTransformType.Rotate:
                    rotationAmount = Random.Range(180f, 270f) * (Random.value > 0.5f ? 1f : -1f);
                    break;
                case GridTransformType.Mirror:
                    rotationAmount = 180f; // 翻转效果
                    scaleAmount = 1.15f;
                    break;
                case GridTransformType.Wave:
                    rotationAmount = Random.Range(90f, 180f) * (Random.value > 0.5f ? 1f : -1f);
                    scaleAmount = 1.05f;
                    break;
            }

            var seq = DOTween.Sequence();

            seq.Append(grid.Rt.DOScale(scaleAmount, 0.1f).SetEase(Ease.OutQuad));

            seq.Append(DOVirtual.Float(0, 1, 0.35f, t =>
            {
                var pos = FMath.Bezier(startPos, control, targetPos, t);
                grid.Rt.anchoredPosition = pos;
                grid.Rt.rotation = Quaternion.Euler(0, 0, rotationAmount * t);
            }).SetEase(Ease.InOutQuad));

            seq.Append(grid.Rt.DOScale(1f, 0.1f).SetEase(Ease.OutBack));
            seq.Join(grid.Rt.DORotate(Vector3.zero, 0.1f).SetEase(Ease.OutBack));

            seq.OnComplete(() =>
            {
                onComplete?.Invoke();
            });
        }

        #endregion

        #region 辅助方法

        private (int x, int y)? FindNearestValidPosition(int targetX, int targetY, HashSet<(int x, int y)> occupied)
        {
            for (int radius = 1; radius <= 5; radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius) continue;

                        int newX = targetX + dx;
                        int newY = targetY + dy;

                        if (_view.IsValidPosition(newX, newY) && !occupied.Contains((newX, newY)))
                        {
                            return (newX, newY);
                        }
                    }
                }
            }

            return null;
        }

        private void OnTransformComplete()
        {
            Debug.Log("网格变换完成");

            // 验证网格状态同步
            _view.ValidateGridStates();

            // 验证所有格子的状态
            var activeGrids = new List<Grid>(_view.GameGrids);
            int busyCount = 0;
            int emptyCount = 0;
            int obstacleCount = 0;

            var gridStates = _view.GridStates;
            for (int x = 0; x < gridStates.GetLength(0); x++)
            {
                for (int y = 0; y < gridStates.GetLength(1); y++)
                {
                    var state = gridStates[x, y].state;
                    if (state == EGridState.Busy) busyCount++;
                    else if (state == EGridState.Empty) emptyCount++;
                    else if (state == EGridState.Obstacle) obstacleCount++;
                }
            }

            Debug.Log($"状态统计 - Busy: {busyCount}, Empty: {emptyCount}, Obstacle: {obstacleCount}, 实际格子数: {activeGrids.Count}");

            _isTransforming = false;
            if (Runtime.mode == 0)
            {
                // 经典模式：变换结束后强制保障至少有一对可消除目标
                _view.EnsureAtLeastOneValidPair();
            }
            else
            {
                _view.SetInputLocked(false);
            }
        }

        public bool IsTransforming()
        {
            return _isTransforming;
        }

        #endregion
    }
}
