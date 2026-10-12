using System.Collections.Generic;
using Algorithm;
using FCSharpExt;
using FUnityAttribute.RuntimeAttributes;
using FUnityCommon;
using FUnityUI;
using GameGrid;
using UnityEngine;
using UnityEngine.UI;
using Grid = GameGrid.Grid;

namespace View
{
    [FViewLoader("View/GameView")]
    public class GameView : FUiBase<GameView>
    {
        [FGameObject] private GameObject _gridItem;

        [FGameObject] private GameObject _gameGrid;

        private Dictionary<Sprite, int> _idCache;


        public Stack<Grid> clickedGridStack;

        [FResourcesLoad("UI/Icons/obstacle")] private Sprite _obstacleIcon;
        


        #region Constants

        private const int COL = 8;
        private const int ROW = 11;
        private const float SPACING = 10f;

        #endregion

        private float _gridWidth, _gridHeight;
        private float _yOffset;
        [FuiAutowire("game-padding")] private RectTransform _renderRoot;

        private List<Grid> _gameGirds;
        private readonly GridState[,] _gridStates = new GridState[COL, ROW];
        private readonly BackgroundGird[,] _backgroundGrids = new BackgroundGird[COL, ROW];

        private Queue<GridState> _enableGridStates = new();
        private Runtime _runtime;

        public override void OnSetup()
        {
            _idCache = new Dictionary<Sprite, int>();
            _runtime = Runtime.Instance;
            // _runtime.pathColor =
            // _runtime.gridOriginalColor = Color.green;
            _gameGirds = new List<Grid>();
            clickedGridStack = new Stack<Grid>();
            var rect = _renderRoot.rect;
            _gridWidth = (rect.width - (COL - 1) * SPACING) / COL;
            _gridHeight = (rect.height - (ROW - 1) * SPACING) / ROW;
            var min = Mathf.Min(_gridWidth, _gridHeight);
            // _yOffset = (rect.height - (ROW - 1) * SPACING - ROW * _gridHeight) / 2f;
            _yOffset = (rect.height - (ROW - 1) * SPACING - ROW * _gridHeight) / 2f;
            // 重设预制体大小
            _gridHeight = _gridWidth = min;
            _gridItem.GetComponent<RectTransform>().sizeDelta = new Vector2(_gridWidth, _gridHeight);
            RenderGrid();

            InitGridStates();
            InitObstacle(); // 生成障碍物
            SpawnInitialGameGrid();
        }

        #region Game Logic




        [FOnClick("refresh-grid-btn")]
        public void RefreshGrid()
        {
            if (clickedGridStack.Count > 0)
            {
                clickedGridStack.Pop().ResetState();
            }

            var enableGrids = new List<GridState>();
            for (var i = 0; i < COL; i++)
            {
                for (var j = 0; j < ROW; j++)
                {
                    if (_gridStates[i, j].IsObstacle()) continue;
                    enableGrids.Add(_gridStates[i, j]);
                }
            }

            enableGrids.Shuffle();
            var queue = new Queue<GridState>(enableGrids);

            foreach (var grid in _gameGirds)
            {
                var item = queue.Dequeue(); // 目标位置
                var gridOrigin = _gridStates[grid.x, grid.y]; // 当前位置
                var target = _gridStates[item.x, item.y];
                // 计算路径
                var (_, path) = ShortestPath.FindPath(gridOrigin, target, _gridStates, true);


                FAsync.Instance.StartRoutine(grid.MoveToTarget(path, null, true));

                // 更新 Grid 对象的数据到新位置
                grid.x = item.x;
                grid.y = item.y;
            }

            // 重新标记 Busy
            foreach (var grid in _gameGirds)
            {
                UpdateGridState(grid.x, grid.y, EGridState.Busy);
            }
        }

        #endregion

        #region Grid State

        private void InitGridStates()
        {
            List<GridState> temp = new();
            for (var i = 0; i < COL; i++)
            {
                for (var j = 0; j < ROW; j++)
                {
                    _gridStates[i, j] = new GridState
                    {
                        x = i,
                        y = j,
                        state = EGridState.Empty
                    };
                    temp.Add(_gridStates[i, j]);
                }
            }

            temp.Shuffle();
            // 打乱后加入队列供随机取用
            _enableGridStates = new Queue<GridState>(temp);
        }

        private void InitObstacle()
        {
            var obstacleCount = Random.Range(10, 20);
            for (var i = 0; i < obstacleCount; i++)
            {
                if (_enableGridStates.Count == 0) break;
                var gridState = _enableGridStates.Dequeue();
                _gridStates[gridState.x, gridState.y].state = EGridState.Obstacle;
                var bgInstance = _backgroundGrids[gridState.x, gridState.y];

                bgInstance.FrontImage.color = Color.white;
                bgInstance.FrontImage.sprite = _obstacleIcon;
            }
        }

        #endregion

        #region Background Grid

        public BackgroundGird[,] BackgroundGrids => _backgroundGrids;

        private void RenderGrid()
        {
            for (var i = 0; i < COL; i++)
            {
                for (var j = 0; j < ROW; j++)
                {
                    var item = Instantiate(_gridItem, _renderRoot);
                    SetupGrid(item, i, j);
                    item.SetActive(true);
                    var ins = new BackgroundGird(item, i, j);
                    _backgroundGrids[i, j] = ins;
                }
            }
        }

        private void SetupGrid(GameObject gridItem, int x, int y)
        {
            var rectTransform = gridItem.GetComponent<RectTransform>();
            rectTransform.anchoredPosition = new Vector2(GetXPosition(x), GetYPosition(y));
            rectTransform.sizeDelta = new Vector2(_gridWidth, _gridHeight);
        }

        #endregion

        #region Helper Methods

        public float GetXPosition(int x)
        {
            return x * _gridWidth + x * SPACING + _gridWidth / 2f;
        }

        public float GetYPosition(int y)
        {
            return -y * _gridHeight - y * SPACING - _gridHeight / 2f - _yOffset;
        }

        private void GetRandomCoord(out Vector2Int coord)
        {
            var rndCoord = _enableGridStates.Dequeue();
            _gridStates[rndCoord.x, rndCoord.y].state = EGridState.Busy;
            coord = new Vector2Int(rndCoord.x, rndCoord.y);
        }

        public void UpdateGridState(int x, int y, EGridState newState)
        {
            _gridStates[x, y].state = newState;
        }

        public GridState GetGridState(int x, int y)
        {
            return _gridStates[x, y];
        }

        public GridState[,] GridStates => _gridStates;

        public void RemoveGameGrid(Grid item)
        {
            _gridStates[item.x, item.y].state = EGridState.Empty;
            _gameGirds.Remove(item);
            Destroy(item.obj);
        }

        #endregion

        #region Game Runtime

        private void SpawnInitialGameGrid()
        {
            // 开始生成游戏格子对象
            var allItems = new List<Sprite>(Runtime.Instance.gameItems);
            allItems.Shuffle();
            // 取前 20个
            var currentLevelIcons = allItems.GetRange(0, Mathf.Min(20, allItems.Count));
            // 所有能用的格子数量
            var cnt = _enableGridStates.Count;
            const int skipRound = 8;
            // 一组是 2 个，两个 2 级的，四个 1 级的
            var maxLoop = Mathf.Max(Mathf.Min(cnt, cnt / 2) - skipRound, 1);
            var id = 0;
            while (currentLevelIcons.Count > 0 && id < maxLoop)
            {
                var asset = currentLevelIcons.Random();
                SpawnGridItem(id, asset);
                id += 1;
            }
        }

        private void SpawnGridItem(int id, Sprite asset)
        {
            for (var i = 0; i < 2; i++)
            {
                SpawnGameGridItem(id, asset);
            }
        }

        private void SpawnGameGridItem(int id, Sprite icon)
        {
            var gameObj = Instantiate(_gameGrid, _renderRoot);
            GetRandomCoord(out var coord);
            if (_idCache.TryGetValue(icon, out var newId))
            {
                id = newId;
            }
            else
            {
                _idCache.Add(icon, id);
            }
            var obj = new Grid(icon, gameObj, id, coord.x, coord.y);
            obj.SetPosition(new Vector2(GetXPosition(coord.x), GetYPosition(coord.y)));
            obj.SetGirdSize(new Vector2(_gridWidth, _gridHeight));
            _gameGirds.Add(obj);
        }

        #endregion
    }
}