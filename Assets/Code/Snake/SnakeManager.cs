using System.Collections.Generic;
using UnityEngine;
using ReGecko.GridSystem;
using ReGecko.Grid.Entities;
using ReGecko.Levels;
using System.Linq;

namespace ReGecko.SnakeSystem
{
    /// <summary>
    /// 蛇管理类，负责管理多条蛇的创建和逻辑处理
    /// </summary>
    public class SnakeManager : MonoBehaviour
    {
        [Header("管理器设置")]
        public Transform SnakeContainer; // 蛇的父容器
        
        // 管理的蛇列表
        private readonly List<BaseSnake> _snakes = new List<BaseSnake>();
        private readonly Dictionary<string, BaseSnake> _snakeDict = new Dictionary<string, BaseSnake>();
        
        // 配置和依赖
        private GridConfig _gridConfig;
        private GridEntityManager _entityManager;
        private LevelConfig _currentLevel;

        // 事件
        public System.Action<BaseSnake> OnSnakeCreated;
        public System.Action<BaseSnake> OnSnakeDestroyed;
        public System.Action OnAllSnakesDead;

        /// <summary>
        /// 初始化蛇管理器
        /// </summary>
        public void Initialize(LevelConfig levelConfig, GridConfig gridConfig, GridEntityManager entityManager, Transform container = null)
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

            // 根据配置选择蛇的类型，目前只有SnakeController
            BaseSnake snake = snakeGo.AddComponent<SnakeController>();
            
            // 设置蛇的属性
            ConfigureSnake(snake, snakeConfig, snakeId);
            
            // 初始化蛇
            snake.Initialize(_gridConfig, _entityManager);
            
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
            snake.BodySprite = config.BodySprite;
            snake.BodyColor = config.Color;
            snake.Length = Mathf.Max(1, config.Length);
            snake.HeadCell = config.HeadCell;
            snake.InitialBodyCells = config.BodyCells;
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
        /// 移除指定蛇
        /// </summary>
        public bool RemoveSnake(string snakeId)
        {
            if (_snakeDict.TryGetValue(snakeId, out BaseSnake snake))
            {
                return RemoveSnake(snake);
            }
            return false;
        }

        /// <summary>
        /// 移除指定蛇
        /// </summary>
        public bool RemoveSnake(BaseSnake snake)
        {
            if (snake == null) return false;

            _snakes.Remove(snake);
            _snakeDict.Remove(snake.SnakeId);
            
            OnSnakeDestroyed?.Invoke(snake);
            
            if (snake.gameObject != null)
            {
                if (Application.isPlaying)
                    Destroy(snake.gameObject);
                else
                    DestroyImmediate(snake.gameObject);
            }

            CheckAllSnakesDead();
            return true;
        }

        /// <summary>
        /// 清理所有蛇
        /// </summary>
        public void ClearAllSnakes()
        {
            var snakesCopy = new List<BaseSnake>(_snakes);
            foreach (var snake in snakesCopy)
            {
                RemoveSnake(snake);
            }
            _snakes.Clear();
            _snakeDict.Clear();
        }

        /// <summary>
        /// 获取指定ID的蛇
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
        /// 获取活着的蛇
        /// </summary>
        public List<BaseSnake> GetAliveSnakes()
        {
            return _snakes.Where(s => s != null && s.IsAlive()).ToList();
        }

        /// <summary>
        /// 获取可控制的蛇
        /// </summary>
        public List<BaseSnake> GetControllableSnakes()
        {
            return _snakes.Where(s => s != null && s.IsAlive() && s.IsControllable).ToList();
        }

        /// <summary>
        /// 更新所有蛇的网格配置
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
        /// 检查是否所有蛇都死了
        /// </summary>
        private void CheckAllSnakesDead()
        {
            bool allDead = _snakes.Count == 0 || _snakes.All(s => s == null || !s.IsAlive());
            if (allDead)
            {
                OnAllSnakesDead?.Invoke();
            }
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

            // 更新所有活着的蛇
            foreach (var snake in _snakes)
            {
                if (snake != null && snake.IsAlive())
                {
                    snake.UpdateMovement();
                }
            }
        }

        /// <summary>
        /// 获取蛇的数量统计
        /// </summary>
        public SnakeStats GetStats()
        {
            return new SnakeStats
            {
                TotalCount = _snakes.Count,
                AliveCount = GetAliveSnakes().Count,
                ControllableCount = GetControllableSnakes().Count
            };
        }

        private void OnDestroy()
        {
            ClearAllSnakes();
        }
    }

    /// <summary>
    /// 蛇的统计信息
    /// </summary>
    [System.Serializable]
    public class SnakeStats
    {
        public int TotalCount;
        public int AliveCount;
        public int ControllableCount;
    }
}
