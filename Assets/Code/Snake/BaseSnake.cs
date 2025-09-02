using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ReGecko.GridSystem;
using ReGecko.Grid.Entities;
using ReGecko.Game;
using System.Collections;
using ReGecko.GameCore.Flow;

namespace ReGecko.SnakeSystem
{
    /// <summary>
    /// 蛇的基类，定义蛇的基本属性和接口
    /// </summary>
    public abstract class BaseSnake : MonoBehaviour
    {
        [Header("基本属性")]
        public string SnakeId;
        public Sprite BodySprite;
        public Color BodyColor = Color.white;
        public int Length = 4;
        public Vector2Int HeadCell;
        public Vector2Int[] InitialBodyCells; // 含头在index 0，可为空
        
        [Header("颜色配置")]
        [Tooltip("蛇的颜色类型，用于匹配洞的颜色")]
        public SnakeColorType ColorType = SnakeColorType.Red; // 蛇的颜色类型
        
        [Header("移动设置")]
        public float MoveSpeedCellsPerSecond = 16f;
        public float SnapThreshold = 0.05f;
        public int MaxCellsPerFrame = 12;
        
        [Header("功能开关")]
        public bool EnableBodySpriteManagement = true;
        public bool IsControllable = true; // 是否可以被玩家控制
        
        [Header("调试选项")]
        public bool ShowDebugStats = false;
        public bool DrawDebugGizmos = false;

        // 保护成员，子类可以访问
        protected GridConfig _grid;
        protected GridEntityManager _entityManager;
        protected SnakeManager _snakeManager; // 蛇管理器引用，用于碰撞检测
        protected readonly List<Transform> _segments = new List<Transform>();
        protected readonly LinkedList<Vector2Int> _bodyCells = new LinkedList<Vector2Int>(); // 离散身体占用格，头在First
        protected readonly HashSet<Vector2Int> _bodyCellsSet = new HashSet<Vector2Int>(); // 用于快速查找的身体格子集合
        protected readonly Queue<Vector2Int> _pathQueue = new Queue<Vector2Int>(); // 待消费路径（目标格序列）
        protected readonly List<Vector2Int> _pathBuildBuffer = new List<Vector2Int>(64); // 复用的路径构建缓冲
        
        protected Vector2Int _currentHeadCell;
        protected Vector2Int _currentTailCell;
        protected bool _consuming; // 洞吞噬中
        
        protected SnakeBodySpriteManager _bodySpriteManager;

        // 状态相关
        protected SnakeState _state = SnakeState.Alive;

        // 公共属性
        public string Name { get; set; } = "Snake";
        public float MoveSpeed => MoveSpeedCellsPerSecond;
        public SnakeState State => _state;
        public Vector2Int CurrentHeadCell => _currentHeadCell;
        public Vector2Int CurrentTailCell => _currentTailCell;
        public int CurrentLength => _bodyCells.Count;
        public bool IsAlive() => _state == SnakeState.Alive;
        public bool IsDead() => _state == SnakeState.Dead;
        public bool IsConsuming() => _consuming;
        public virtual LinkedList<Vector2Int> GetBodyCells() => _bodyCells;

        // 抽象方法，子类必须实现
        public abstract void Initialize(GridConfig grid, GridEntityManager entityManager = null, SnakeManager snakeManager = null);
        public abstract void UpdateMovement();

        // 虚方法，子类可以重写
        public virtual void UpdateGridConfig(GridConfig newGrid)
        {
            _grid = newGrid;
        }

        public virtual void SetState(SnakeState newState)
        {
            if (_state != newState)
            {
                SnakeState oldState = _state;
                _state = newState;
                OnStateChanged(oldState, newState);
            }
        }

        protected virtual void OnStateChanged(SnakeState oldState, SnakeState newState)
        {
            // 子类可以重写此方法来处理状态变化
        }

        // 通用的辅助方法
        protected Vector2Int ClampInside(Vector2Int cell)
        {
            if (!_grid.IsValid()) return cell;
            cell.x = Mathf.Clamp(cell.x, 0, _grid.Width - 1);
            cell.y = Mathf.Clamp(cell.y, 0, _grid.Height - 1);
            return cell;
        }

        protected int Manhattan(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        /// <summary>
        /// 同步身体格子的HashSet缓存
        /// </summary>
        protected virtual void SyncBodyCellsSet()
        {
            _bodyCellsSet.Clear();
            foreach (var cell in _bodyCells)
            {
                _bodyCellsSet.Add(cell);
            }
        }

        /// <summary>
        /// 检查格子是否被自身占用（优化版本）
        /// </summary>
        protected virtual bool IsOccupiedBySelf(Vector2Int cell)
        {
            return _bodyCellsSet.Contains(cell);
        }

        protected virtual void InitializeBodySpriteManager()
        {
            if (!EnableBodySpriteManagement) return;
            
            var bodySpriteGo = new GameObject("BodySpriteManager");
            bodySpriteGo.transform.SetParent(transform, false);
            _bodySpriteManager = bodySpriteGo.AddComponent<SnakeBodySpriteManager>();
            // SnakeBodySpriteManager会通过GetComponent获取SnakeController
            
            if (GameContext.SnakeBodyConfig != null)
            {
                _bodySpriteManager.Config = GameContext.SnakeBodyConfig;
            }
        }

        protected virtual void OnDestroy()
        {
            // 清理资源
            if (_bodySpriteManager != null)
            {
                Destroy(_bodySpriteManager.gameObject);
            }
        }
    }

    /// <summary>
    /// 蛇的状态枚举
    /// </summary>
    public enum SnakeState
    {
        Alive,    // 活着
        Dead,     // 死亡
        Consuming // 正在被洞吞噬
    }
}