using System.Collections.Generic;
using GameGrid;
using UnityEngine;

namespace Algorithm
{
    public static class ShortestPath
    {
        private static int GetManhattanDistance(GridState a, GridState b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }
        /// <returns>
        /// (true, path) : 成功找到通往终点的完整路径
        /// (false, path): 无法到达终点，返回的是一条离终点最近的局部路径
        /// (false, empty): 起点被堵死或无法移动
        /// </returns>
        public static (bool success, List<GridState> path) FindPath(GridState start, GridState end, GridState[,] gridMap, bool ignoreObstacle = false)
        {
            // 1. 基础校验
            // 终点校验：如果终点本身无效且不忽略障碍，直接返回失败和空路径
            var isEndInvalid = ignoreObstacle 
                ? end.IsObstacle() 
                : end.IsObstacleOrBusy(end);

            if (start == end || isEndInvalid) 
                return (false, new List<GridState>());

            var row = gridMap.GetLength(0);
            var col = gridMap.GetLength(1);
            var visited = new bool[row, col];
            var directions = new (int, int)[] { (-1, 0), (1, 0), (0, -1), (0, 1) };

            // 2. 状态追踪
            // closestNode 用于记录搜索过程中离终点最近的可达节点
            GridState closestNode = start;
            int minDistance = GetManhattanDistance(start, end);

            visited[start.x, start.y] = true;
            var queue = new Queue<GridState>();
            var parentMap = new GridState[row, col];
            
            queue.Enqueue(start);
            bool found = false;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                // 更新最近节点逻辑：如果当前节点比之前的记录更近，就记下来
                int currentDist = GetManhattanDistance(current, end);
                if (currentDist < minDistance)
                {
                    minDistance = currentDist;
                    closestNode = current;
                }

                if (current == end)
                {
                    found = true;
                    break;
                }

                foreach (var direction in directions)
                {
                    var nextX = current.x + direction.Item1;
                    var nextY = current.y + direction.Item2;
                    var inBound = nextX >= 0 && nextX < row && nextY >= 0 && nextY < col;
                    
                    if (!inBound) continue;

                    var targetNode = gridMap[nextX, nextY];
                    var isBlocked = ignoreObstacle 
                        ? targetNode.IsObstacle() 
                        : targetNode.IsObstacleOrBusy(end);

                    if (!visited[nextX, nextY] && !isBlocked)
                    {
                        visited[nextX, nextY] = true;
                        parentMap[nextX, nextY] = current;
                        queue.Enqueue(targetNode);
                    }
                }
            }

            var path = new List<GridState>();
            
            // 决定回溯的起点：如果找到了终点用 end，否则用离终点最近的那个点
            GridState backtrackStart = found ? end : closestNode;

            // 如果连最近的点都是 start 本身（意味着一步都没走出去），且没找到终点
            if (backtrackStart == start)
            {
                path.Add(start);    
                return (false, path);
            }

            // 回溯路径
            var pathNode = backtrackStart;
            path.Add(pathNode);
            while (pathNode != start)
            {

                pathNode = parentMap[pathNode.x, pathNode.y];
                path.Add(pathNode);
            }
            path.Reverse();

            return (found, path);
        }
    }
}
