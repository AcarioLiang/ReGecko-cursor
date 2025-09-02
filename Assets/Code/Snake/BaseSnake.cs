using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ReGecko.GridSystem;
using ReGecko.Grid.Entities;
using System.Collections;

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
        protected readonly List<Transform> _segments = new List<Transform>();
        protected readonly LinkedList<Vector2Int> _bodyCells = new LinkedList<Vector2Int>(); // 离散身体占用格，头在First
        protected readonly Queue<Vector2Int> _pathQueue = new Queue<Vector2Int>(); // 待消费路径（目标格序列）
        protected readonly List<Vector2Int> _pathBuildBuffer = new List<Vector2Int>(64); // 复用的路径构建缓冲
        
        protected Vector2Int _currentHeadCell;
        protected Vector2Int _currentTailCell;
        protected bool _consuming; // 洞吞噬中
        
        protected SnakeBodySpriteManager _bodySpriteManager;

        // 蛇的状态枚举
        public enum SnakeState
        {
            Idle,       // 空闲状态
            Moving,     // 移动状态
            Consuming,  // 被消费状态
            Dead        // 死亡状态
        }
        
        protected SnakeState _state = SnakeState.Idle;
        public SnakeState State => _state;

        // 公共接口
        public virtual Vector2Int GetHeadCell() => _bodyCells.Count > 0 ? _currentHeadCell : Vector2Int.zero;
        public virtual Vector2Int GetTailCell() => _bodyCells.Count > 0 ? _currentTailCell : Vector2Int.zero;
        public virtual LinkedList<Vector2Int> GetBodyCells() => _bodyCells;
        public virtual int GetCurrentLength() => _bodyCells.Count;
        public virtual bool IsAlive() => _state != SnakeState.Dead && _bodyCells.Count > 0;

        // 抽象方法，子类必须实现
        public abstract void Initialize(GridConfig grid, GridEntityManager entityManager = null);
        public abstract void UpdateMovement();
        
        // 虚方法，子类可以重写
        public virtual void UpdateGridConfig(GridConfig newGrid)
        {
            _grid = newGrid;
            for (int i = 0; i < _segments.Count; i++)
            {
                var rt = _segments[i].GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.sizeDelta = new Vector2(_grid.CellSize, _grid.CellSize);
                }
            }
        }

        public virtual void SetState(SnakeState newState)
        {
            if (_state != newState)
            {
                var oldState = _state;
                _state = newState;
                OnStateChanged(oldState, newState);
            }
        }

        // 事件回调
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

        protected virtual void InitializeBodySpriteManager()
        {
            if (!EnableBodySpriteManagement) return;
            
            // 检查是否已经有身体图片管理器
            _bodySpriteManager = GetComponent<SnakeBodySpriteManager>();
            if (_bodySpriteManager == null)
            {
                _bodySpriteManager = gameObject.AddComponent<SnakeBodySpriteManager>();
            }
        }

        // 销毁时清理资源
        protected virtual void OnDestroy()
        {
            if (_segments != null)
            {
                foreach (var segment in _segments)
                {
                    if (segment != null)
                    {
                        if (Application.isPlaying)
                            Destroy(segment.gameObject);
                        else
                            DestroyImmediate(segment.gameObject);
                    }
                }
                _segments.Clear();
            }
        }
    }
}
