using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ReGecko.GridSystem;
using ReGecko.Grid.Entities;
using ReGecko.Levels;
using ReGecko.Framework;

namespace ReGecko.SnakeSystem
{
    /// <summary>
    /// 蛇管理器 - 负责管理多条蛇的创建、更新和销毁
    /// </summary>
    public class SnakeManager : BaseManager
    {
        static SnakeManager _instance;
        public static SnakeManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("SnakeManager");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<SnakeManager>();
                }
                return _instance;
            }
        }

        [Header("调试信息")]
        [SerializeField] private List<BaseSnake> _snakes = new List<BaseSnake>();
        [SerializeField] private int _totalSnakeCount = 0;
        [SerializeField] private int _aliveSnakeCount = 0;

        // 事件
        public static event Action<BaseSnake> OnSnakeCreated;
        public static event Action<BaseSnake> OnSnakeDestroyed;
        public static event Action OnAllSnakesDead;

        // 私有字段
        private readonly Dictionary<string, BaseSnake> _snakeDict = new Dictionary<string, BaseSnake>();
        private LevelConfig _currentLevel;
        private GridConfig _grid;

        // 属性
        public Transform SnakeContainer { get; private set; }
        public Canvas SnakeCanvas { get; private set; }
        public int TotalSnakeCount => _snakes.Count;
        public int AliveSnakeCount => _snakes.Count(s => s != null && s.IsAlive());


        //优化缓存
        Vector3 _lastMousePos;
        bool _hasPackSnake = false;

        /// <summary>
        /// 初始化蛇管理器
        /// </summary>
        public void Init(LevelConfig levelConfig, GridConfig gridConfig, Canvas parentCanvas, Transform container = null)
        {
            _currentLevel = levelConfig;
            _grid = gridConfig;
            SnakeContainer = container ?? transform;
            SnakeCanvas = parentCanvas;

            ClearAllSnakes();
            CreateSnakesFromConfig();
        }

        /// <summary>
        /// 从配置创建所有蛇
        /// </summary>
        private void CreateSnakesFromConfig()
        {
            if (_currentLevel?.Snakes == null) return;

            for (int i = 0; i < _currentLevel.Snakes.Length; i++)
            {
                CreateSnake(_currentLevel.Snakes[i], i);
            }
        }

        /// <summary>
        /// 创建单条蛇
        /// </summary>
        public BaseSnake CreateSnake(SnakeInitConfig snakeConfig, int index = -1)
        {
            string snakeId = !string.IsNullOrEmpty(snakeConfig.Id) ? snakeConfig.Id : $"Snake_{(index >= 0 ? index : _snakes.Count)}";
            
            // 检查ID是否已存在
            if (_snakeDict.ContainsKey(snakeId))
            {
                Debug.LogWarning($"蛇ID '{snakeId}' 已存在，将自动生成新ID");
                snakeId = GenerateUniqueSnakeId(snakeId);
            }

            var snakeGo = new GameObject(snakeId);
            snakeGo.transform.SetParent(SnakeContainer, false);

            // 确保有RectTransform组件（UI系统必需）
            if (snakeGo.GetComponent<RectTransform>() == null)
            {
                snakeGo.AddComponent<RectTransform>();
            }

            // 根据配置选择蛇的类型，目前只有SnakeController
            BaseSnake snake = snakeGo.AddComponent<SnakeController>();
            
            // 设置蛇的属性
            ConfigureSnake(snake, snakeConfig, snakeId);
            
            // 初始化蛇
            snake.Initialize(_grid);
            
            // 添加到管理列表
            _snakes.Add(snake);
            _snakeDict[snakeId] = snake;
            
            OnSnakeCreated?.Invoke(snake);
            
            return snake;
        }

        /// <summary>
        /// 配置蛇的属性
        /// </summary>
        private void ConfigureSnake(BaseSnake snake, SnakeInitConfig config, string snakeId)
        {
            snake.SnakeId = snakeId;
            snake.Name = config.Name;
            snake.BodySprite = config.BodySprite;
            snake.BodyColor = config.Color;
            snake.ColorType = config.ColorType; // 设置颜色类型
            snake.Length = Mathf.Max(1, config.Length);
            snake.InitialHeadCell = config.HeadCell;
            snake.InitialBodyCells = config.BodyCells;
            snake.IsControllable = config.IsControllable;
            
        }

        /// <summary>
        /// 生成唯一的蛇ID
        /// </summary>
        private string GenerateUniqueSnakeId(string baseId)
        {
            int counter = 1;
            string newId = $"{baseId}_{counter}";
            while (_snakeDict.ContainsKey(newId))
            {
                counter++;
                newId = $"{baseId}_{counter}";
            }
            return newId;
        }

        /// <summary>
        /// 移除指定蛇
        /// </summary>
        public bool RemoveSnake(string snakeId)
        {
            if (_snakeDict.TryGetValue(snakeId, out BaseSnake snake))
            {
                _snakes.Remove(snake);
                _snakeDict.Remove(snakeId);
                
                if (snake != null)
                {
                    OnSnakeDestroyed?.Invoke(snake);
                    Destroy(snake.gameObject);
                }
                
                return true;
            }
            return false;
        }

        /// <summary>
        /// 清理所有蛇
        /// </summary>
        public void ClearAllSnakes()
        {
            foreach (var snake in _snakes.ToList())
            {
                if (snake != null)
                {
                    OnSnakeDestroyed?.Invoke(snake);
                    Destroy(snake.gameObject);
                }
            }
            
            _snakes.Clear();
            _snakeDict.Clear();
        }

        /// <summary>
        /// 根据ID获取蛇
        /// </summary>
        public BaseSnake GetSnake(string snakeId)
        {
            _snakeDict.TryGetValue(snakeId, out BaseSnake snake);
            return snake;
        }

        /// <summary>
        /// 获取所有蛇
        /// </summary>
        public List<BaseSnake> GetAllSnakes()
        {
            return new List<BaseSnake>(_snakes);
        }

        /// <summary>
        /// 获取所有活着的蛇
        /// </summary>
        public List<BaseSnake> GetAliveSnakes()
        {
            return _snakes.Where(s => s != null && s.IsAlive()).ToList();
        }

        /// <summary>
        /// 获取所有可控制的蛇
        /// </summary>
        public List<BaseSnake> GetControllableSnakes()
        {
            return _snakes.Where(s => s != null && s.IsAlive() && s.IsControllable).ToList();
        }

        /// <summary>
        /// 更新网格配置
        /// </summary>
        public void UpdateGridConfig(GridConfig newGridConfig)
        {
            _grid = newGridConfig;
            
            foreach (var snake in _snakes)
            {
                if (snake != null)
                {
                    snake.UpdateGridConfig(newGridConfig);
                }
            }
        }

        /// <summary>
        /// 检查是否所有蛇都已死亡
        /// </summary>
        public bool AreAllSnakesDead()
        {
            return _snakes.All(s => s == null || s.IsDead());
        }

        /// <summary>
        /// 获取蛇的数量统计
        /// </summary>
        public SnakeStats GetStats()
        {
            var aliveSnakes = GetAliveSnakes();
            var controllableSnakes = GetControllableSnakes();
            
            return new SnakeStats
            {
                TotalCount = _snakes.Count,
                AliveCount = aliveSnakes.Count,
                ControllableCount = controllableSnakes.Count
            };
        }

        /// <summary>
        /// 更新所有蛇的移动逻辑
        /// </summary>
        private void Update()
        {

            HandleInput();

            // 更新所有活着的蛇
            foreach (var snake in _snakes)
            {
                if (snake != null && snake.IsAlive() && snake.IsDragging)
                {
                    snake.UpdateMovement();
                }
            }

            // 检查是否所有蛇都死了
            _aliveSnakeCount = AliveSnakeCount;
            _totalSnakeCount = TotalSnakeCount;
            
            if (_aliveSnakeCount == 0 && _totalSnakeCount > 0)
            {
                OnAllSnakesDead?.Invoke();
            }
        }

        void HandleInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (_lastMousePos != Input.mousePosition )
                {
                    _lastMousePos = Input.mousePosition;

                    TryPickHeadOrTail(); 
                }
            }
            else if (Input.GetMouseButtonUp(0))
            {
                foreach (var snake in _snakes)
                {
                    if (snake != null && snake.IsAlive() && snake.IsControllable && snake.IsDragging)
                    {
                        snake.IsDragging = false;
                        snake.NeedSnapCellsToGrid = true;
                    }
                }

                _hasPackSnake = false;
                _lastMousePos = Vector3.zero;
            }
        }

        public void TryClearSnakes()
        {
            // 清理已销毁的蛇
            for (int i = _snakes.Count - 1; i >= 0; i--)
            {
                if (_snakes[i] == null)
                {
                    _snakes.RemoveAt(i);
                }
            }
        }

        bool TryPickHeadOrTail()
        {
            if (_hasPackSnake)
                return false;

            var world = ScreenToWorld(Input.mousePosition);
            var curMouseSubCell = SubGridHelper.WorldToSubCell(world, _grid);

            BaseSnake bestSnake = null;
            bool bestFromHead = true;
            int bestDist = int.MaxValue;
            float bestWorldDist = float.MaxValue;

            foreach (var snake in _snakes)
            {
                if (snake == null || !snake.IsAlive() || !snake.IsControllable)
                    continue;

                var ctl = (SnakeController)snake;
                var headCell = ctl.GetHeadSubCell();
                var tailCell = ctl.GetTailSubCell();

                int dHead = Mathf.Abs(curMouseSubCell.x - headCell.x) + Mathf.Abs(curMouseSubCell.y - headCell.y);
                int dTail = Mathf.Abs(curMouseSubCell.x - tailCell.x) + Mathf.Abs(curMouseSubCell.y - tailCell.y);

                // 仅考虑“同格或相邻格”（≤1）
                void TryUpdateCandidate(int dist, bool fromHead, Vector2Int refCell)
                {
                    //if (dist > 1) return;

                    // 主排序：更小的格距，其次：更小的世界距离
                    var refWorld = SubGridHelper.SubCellToWorld(refCell, _grid);
                    float wdist = Vector2.SqrMagnitude(new Vector2(world.x - refWorld.x, world.y - refWorld.y));

                    if (dist < bestDist || (dist == bestDist && wdist < bestWorldDist))
                    {
                        bestDist = dist;
                        bestWorldDist = wdist;
                        bestSnake = snake;
                        bestFromHead = fromHead;
                    }
                }

                TryUpdateCandidate(dHead, true, headCell);
                TryUpdateCandidate(dTail, false, tailCell);
            }

            if (bestSnake != null)
            {
                bestSnake.IsDragging = true;
                bestSnake.DragFromHead = bestFromHead;
                _hasPackSnake = true;
                return true;
            }

            return false;
        }

        public bool IsAdjacent(Vector2Int other, Vector2Int Cell)
        {
            return Mathf.Abs(other.x - Cell.x) + Mathf.Abs(other.y - Cell.y) <= 1;
        }

        Vector3 ScreenToWorld(Vector3 screen)
        {
            // UI渲染模式：使用UI坐标转换
            if (SnakeCanvas != null)
            {
                var rect = SnakeCanvas.transform.parent as RectTransform; // GridContainer
                if (rect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, SnakeCanvas.worldCamera, out Vector2 localPoint))
                {
                    return new Vector3(localPoint.x, localPoint.y, 0f);
                }
            }
            else
            {
                Debug.LogError("cache _parentCanvas error!!!");
            }

            // 最后的fallback：简单的比例转换
            Vector2 screenSize = new Vector2(Screen.width, Screen.height);
            Vector2 normalizedScreen = new Vector2(screen.x / screenSize.x, screen.y / screenSize.y);

            // 假设网格居中在屏幕中，计算相对位置
            float gridWidth = _grid.Width * _grid.CellSize;
            float gridHeight = _grid.Height * _grid.CellSize;

            float worldX = (normalizedScreen.x - 0.5f) * gridWidth;
            float worldY = (normalizedScreen.y - 0.5f) * gridHeight;

            return new Vector3(worldX, worldY, 0f);

        }
        /*
        /// <summary>
        /// 获取所有蛇占用的格子
        /// </summary>
        public HashSet<Vector2Int> GetAllSnakeOccupiedCells()
        {
            var occupiedCells = new HashSet<Vector2Int>();
            foreach (var snake in _snakes)
            {
                if (snake != null && snake.IsAlive())
                {
                    var bodyCells = snake.GetBodyCells();
                    foreach (var cell in bodyCells)
                    {
                        occupiedCells.Add(cell);
                    }
                }
            }
            return occupiedCells;
        }

        /// <summary>
        /// 检查指定格子是否被任何蛇占用
        /// </summary>
        public bool IsCellOccupiedByAnySnake(Vector2Int cell)
        {
            foreach (var snake in _snakes)
            {
                if (snake != null && snake.IsAlive())
                {
                    var bodyCells = snake.GetBodyCells();
                    foreach (var bodyCell in bodyCells)
                    {
                        if (bodyCell == cell) return true;
                    }
                }
            }
            return false;
        }
        */
        // 缓存所有蛇的占用格子，避免重复计算
        private HashSet<Vector2Int> _cachedOccupiedCells = new HashSet<Vector2Int>();
        private Dictionary<BaseSnake, HashSet<Vector2Int>> _snakeOccupiedCells = new Dictionary<BaseSnake, HashSet<Vector2Int>>();
        private bool _occupiedCellsCacheValid = false;

        /// <summary>
        /// 检查指定格子是否被指定蛇以外的其他蛇占用（优化版本）
        /// </summary>
        public bool IsCellOccupiedByOtherSnakes(Vector2Int cell, BaseSnake excludeSnake)
        {
            UpdateOccupiedCellsCache();
            
            foreach (var kvp in _snakeOccupiedCells)
            {
                if (kvp.Key != excludeSnake && kvp.Key != null && kvp.Key.IsAlive())
                {
                    if (kvp.Value.Contains(cell))
                        return true;
                }
            }
            return false;
        }



        /// <summary>
        /// 检查指定格子是否被指定蛇以外的其他蛇占用（优化版本）
        /// </summary>
        public bool IsCellOccupiedBySelfSnakes(Vector2Int cell, BaseSnake Snake)
        {
            UpdateOccupiedCellsCache();

            foreach (var kvp in _snakeOccupiedCells)
            {
                if (kvp.Key == Snake && kvp.Key != null && kvp.Key.IsAlive())
                {
                    if (kvp.Value.Contains(cell))
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 获取所有蛇占用的格子
        /// </summary>
        public HashSet<Vector2Int> GetSnakeOccupiedCells(BaseSnake Snake)
        {
            var occupiedCells = new HashSet<Vector2Int>();

            _snakeOccupiedCells.TryGetValue(Snake, out occupiedCells);
            return occupiedCells;
        }


        /// <summary>
        /// 更新占用格子缓存
        /// </summary>
        private void UpdateOccupiedCellsCache()
        {

            if (_occupiedCellsCacheValid) return;

            _cachedOccupiedCells.Clear();
            _snakeOccupiedCells.Clear();

            foreach (var snake in _snakes)
            {
                if (snake != null && snake.IsAlive())
                {
                    var snakeCells = new HashSet<Vector2Int>();
                    var bodyCells = snake.GetBodyCells().ToList();
                    for (int i = 0; i < bodyCells.Count;)
                    {
                        var bigcell = SubGridHelper.SubCellToBigCell(bodyCells[i]);
                        snakeCells.Add(bigcell);
                        _cachedOccupiedCells.Add(bigcell);
                        i += SubGridHelper.SUB_DIV;
                    }
                    _snakeOccupiedCells[snake] = snakeCells;
                }
            }
            _occupiedCellsCacheValid = true;

        }

        /// <summary>
        /// 标记占用格子缓存为无效（当蛇移动后调用）
        /// </summary>
        public void InvalidateOccupiedCellsCache()
        {
            _occupiedCellsCacheValid = false;
        }

        private void OnDestroy()
        {
            ClearAllSnakes();

        }

        public void DestroyInstance()
        {
            if (_instance != null)
            {
                DestroyImmediate(_instance.gameObject);
                _instance = null;
            }
        }
    }

    /// <summary>
    /// 蛇的数量统计
    /// </summary>
    [Serializable]
    public struct SnakeStats
    {
        public int TotalCount;
        public int AliveCount;
        public int ControllableCount;
    }
}