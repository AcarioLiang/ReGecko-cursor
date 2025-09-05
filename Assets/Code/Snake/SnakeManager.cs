using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ReGecko.GridSystem;
using ReGecko.Grid.Entities;
using ReGecko.Levels;
using ReGecko.HingeJointSnake;

namespace ReGecko.SnakeSystem
{
    /// <summary>
    /// 蛇管理器 - 负责管理多条蛇的创建、更新和销毁
    /// </summary>
    public class SnakeManager : MonoBehaviour
    {
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
        private GridConfig _gridConfig;
        private GridEntityManager _entityManager;

        // 属性
        public Transform SnakeContainer { get; private set; }
        public int TotalSnakeCount => _snakes.Count;
        public int AliveSnakeCount => _snakes.Count(s => s != null && s.IsAlive());

        /// <summary>
        /// 初始化蛇管理器
        /// </summary>
        public void Initialize(LevelConfig levelConfig, GridConfig gridConfig, GridEntityManager entityManager = null, Transform container = null)
        {
            _currentLevel = levelConfig;
            _gridConfig = gridConfig;
            _entityManager = entityManager;
            SnakeContainer = container ?? transform;
            
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

            // 根据配置选择蛇的类型，使用新的HingeJointSnakeController
            HingeJointSnakeController hingeSnake = snakeGo.AddComponent<HingeJointSnakeController>();
            BaseSnake snake = hingeSnake;
            
            // 设置蛇的属性
            ConfigureSnake(snake, snakeConfig, snakeId);
            
            // 设置初始身体格子配置
            if (snakeConfig.BodyCells != null && snakeConfig.BodyCells.Length > 0)
            {
                hingeSnake.SetInitialBodyCells(snakeConfig.BodyCells);
            }
            else
            {
                // 根据HeadCell和Length生成默认身体格子
                Vector2Int[] defaultBodyCells = GenerateDefaultBodyCells(snakeConfig.HeadCell, snakeConfig.Length);
                hingeSnake.SetInitialBodyCells(defaultBodyCells);
            }
            
            // 初始化蛇
            snake.Initialize(_gridConfig, _entityManager, this);
            
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
            snake.MoveSpeedCellsPerSecond = config.MoveSpeed > 0 ? config.MoveSpeed : 16f;
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
        /// 根据头部位置和长度生成默认身体格子
        /// </summary>
        private Vector2Int[] GenerateDefaultBodyCells(Vector2Int headCell, int length)
        {
            Vector2Int[] bodyCells = new Vector2Int[length];
            
            // 蛇头在第一个位置
            bodyCells[0] = headCell;
            
            // 身体向下延伸
            for (int i = 1; i < length; i++)
            {
                bodyCells[i] = headCell + Vector2Int.down * i;
            }
            
            // 确保所有格子都在网格范围内
            for (int i = 0; i < length; i++)
            {
                bodyCells[i] = ClampCellToGrid(bodyCells[i]);
            }
            
            return bodyCells;
        }

        /// <summary>
        /// 将格子坐标限制在网格范围内
        /// </summary>
        private Vector2Int ClampCellToGrid(Vector2Int cell)
        {
            if (_gridConfig.IsValid())
            {
                return new Vector2Int(
                    Mathf.Clamp(cell.x, 0, _gridConfig.Width - 1),
                    Mathf.Clamp(cell.y, 0, _gridConfig.Height - 1)
                );
            }
            return cell;
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
            _gridConfig = newGridConfig;
            
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
            // 清理已销毁的蛇
            for (int i = _snakes.Count - 1; i >= 0; i--)
            {
                if (_snakes[i] == null)
                {
                    _snakes.RemoveAt(i);
                }
            }

            // 每帧开始时刷新碰撞缓存
            InvalidateOccupiedCellsCache();

            // 更新所有活着的蛇
            foreach (var snake in _snakes)
            {
                if (snake != null && snake.IsAlive())
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



        // 缓存所有蛇的占用格子，避免重复计算
        private HashSet<Vector2Int> _cachedOccupiedCells = new HashSet<Vector2Int>();
        private Dictionary<BaseSnake, HashSet<Vector2Int>> _snakeOccupiedCells = new Dictionary<BaseSnake, HashSet<Vector2Int>>();
        private bool _occupiedCellsCacheValid = false;

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