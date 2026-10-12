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
    /// 网格旋转管理器 - 在消除后旋转整个目标矩阵
    /// </summary>
    public class GridRotationManager
    {
        private readonly AutoLayoutGameView _view;
        private int _currentLevel;
        private bool _isRotating;

        public GridRotationManager(AutoLayoutGameView view)
        {
            _view = view;
            _currentLevel = 1;
        }

        public void SetLevel(int level)
        {
            _currentLevel = level;
        }

        public bool ShouldRotate()
        {
            // 第一关不旋转，第二关及以后才旋转
            return _currentLevel >= 2;
        }

        public void RotateGridsAfterElimination()
        {
            if (!ShouldRotate() || _isRotating) return;

            _isRotating = true;
            Debug.Log($"开始旋转网格 - 当前关卡: {_currentLevel}");

            // 锁定输入
            _view.SetInputLocked(true);

            // 播放音效
            FAudioCore.Instance.PlaySfx("tap");

            // 收集所有活动的游戏格子
            var activeGrids = new List<Grid>(_view.GameGrids);
            if (activeGrids.Count == 0)
            {
                _isRotating = false;
                _view.SetInputLocked(false);
                return;
            }

            // 计算旋转方向（随机顺时针或逆时针）
            bool clockwise = Random.value > 0.5f;
            Debug.Log($"旋转方向: {(clockwise ? "顺时针" : "逆时针")}");

            // 执行旋转
            PerformRotation(activeGrids, clockwise);
        }

        private void PerformRotation(List<Grid> grids, bool clockwise)
        {
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

            Debug.Log($"网格边界: ({minX},{minY}) 到 ({maxX},{maxY}), 中心: ({centerX},{centerY})");

            // 计算每个格子的新位置
            var newPositions = new Dictionary<Grid, (int x, int y)>();
            var occupiedPositions = new HashSet<(int x, int y)>();

            foreach (var grid in grids)
            {
                // 相对于中心的坐标
                int relX = grid.x - centerX;
                int relY = grid.y - centerY;

                // 旋转90度
                int newRelX, newRelY;
                if (clockwise)
                {
                    // 顺时针: (x, y) -> (y, -x)
                    newRelX = relY;
                    newRelY = -relX;
                }
                else
                {
                    // 逆时针: (x, y) -> (-y, x)
                    newRelX = -relY;
                    newRelY = relX;
                }

                // 转换回绝对坐标
                int newX = newRelX + centerX;
                int newY = newRelY + centerY;

                // 检查新位置是否有效且未被占用
                if (_view.IsValidPosition(newX, newY) && !occupiedPositions.Contains((newX, newY)))
                {
                    newPositions[grid] = (newX, newY);
                    occupiedPositions.Add((newX, newY));
                }
                else
                {
                    // 如果新位置无效或已被占用，尝试找到最近的有效位置
                    var nearestPos = FindNearestValidPosition(newX, newY, occupiedPositions);
                    if (nearestPos.HasValue)
                    {
                        newPositions[grid] = nearestPos.Value;
                        occupiedPositions.Add(nearestPos.Value);
                    }
                    else
                    {
                        // 如果找不到有效位置，保持原位
                        newPositions[grid] = (grid.x, grid.y);
                        occupiedPositions.Add((grid.x, grid.y));
                    }
                }
            }

            // 执行动画移动
            AnimateRotation(newPositions);
        }

        private (int x, int y)? FindNearestValidPosition(int targetX, int targetY, HashSet<(int x, int y)> occupied)
        {
            // 螺旋搜索最近的有效位置
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

        private void AnimateRotation(Dictionary<Grid, (int x, int y)> newPositions)
        {
            int completedCount = 0;
            int totalCount = newPositions.Count;

            // 按距离中心的距离排序，从外向内或从内向外旋转
            var sortedGrids = newPositions.Keys.ToList();
            var centerX = sortedGrids.Average(g => g.x);
            var centerY = sortedGrids.Average(g => g.y);

            sortedGrids.Sort((a, b) =>
            {
                var distA = Mathf.Sqrt(Mathf.Pow((float)(a.x - centerX), 2) + Mathf.Pow((float)(a.y - centerY), 2));
                var distB = Mathf.Sqrt(Mathf.Pow((float)(b.x - centerX), 2) + Mathf.Pow((float)(b.y - centerY), 2));
                return distA.CompareTo(distB);
            });

            // 随机决定是从内向外还是从外向内
            if (Random.value > 0.5f)
            {
                sortedGrids.Reverse();
            }

            for (int i = 0; i < sortedGrids.Count; i++)
            {
                var grid = sortedGrids[i];
                var (newX, newY) = newPositions[grid];

                // 添加延迟，创建波浪效果（减少延迟时间，加快速度）
                var delay = i * 0.015f; // 从0.03秒改为0.015秒

                FAsync.Instance.InvokeDelay(() =>
                {
                    AnimateSingleGrid(grid, newX, newY, () =>
                    {
                        completedCount++;
                        if (completedCount >= totalCount)
                        {
                            OnRotationComplete();
                        }
                    });
                }, delay);
            }
        }

        private void AnimateSingleGrid(Grid grid, int newX, int newY, System.Action onComplete)
        {
            // 如果位置没变，跳过
            if (grid.x == newX && grid.y == newY)
            {
                onComplete?.Invoke();
                return;
            }

            // 更新网格状态
            _view.UpdateGridState(grid.x, grid.y, EGridState.Empty);
            _view.UpdateGridState(newX, newY, EGridState.Busy);

            // 更新格子的逻辑坐标
            grid.x = newX;
            grid.y = newY;

            // 计算目标位置
            var targetPos = new Vector2(_view.GetXPosition(newX), _view.GetYPosition(newY));
            var startPos = grid.Rt.anchoredPosition;

            // 计算贝塞尔曲线控制点
            var ab = targetPos - startPos;
            var mid = (startPos + targetPos) * 0.5f;
            var dir = (Vector2)Vector3.Cross(ab, Vector3.forward).normalized;
            var randomOffset = Random.Range(150f, 250f); // 减少偏移量，让轨迹更直接
            var control = mid + dir * randomOffset;

            // 添加旋转动画（减少旋转角度）
            var rotationAmount = Random.Range(180f, 270f) * (Random.value > 0.5f ? 1f : -1f);

            var seq = DOTween.Sequence();

            // 先轻微放大并发光（加快速度）
            seq.Append(grid.Rt.DOScale(1.1f, 0.1f).SetEase(Ease.OutQuad));

            // 贝塞尔曲线移动 + 旋转（加快速度）
            seq.Append(DOVirtual.Float(0, 1, 0.35f, t =>
            {
                var pos = FMath.Bezier(startPos, control, targetPos, t);
                grid.Rt.anchoredPosition = pos;
                grid.Rt.rotation = Quaternion.Euler(0, 0, rotationAmount * t);
            }).SetEase(Ease.InOutQuad));

            // 恢复缩放和旋转，带弹跳效果（加快速度）
            seq.Append(grid.Rt.DOScale(1f, 0.1f).SetEase(Ease.OutBack));
            seq.Join(grid.Rt.DORotate(Vector3.zero, 0.1f).SetEase(Ease.OutBack));

            seq.OnComplete(() =>
            {
                onComplete?.Invoke();
            });
        }

        private void OnRotationComplete()
        {
            Debug.Log("网格旋转完成");
            _isRotating = false;

            // 解锁输入
            _view.SetInputLocked(false);
        }

        public bool IsRotating()
        {
            return _isRotating;
        }
    }
}
