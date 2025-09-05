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
        public int Length = 5;
        
        [Header("颜色配置")]
        [Tooltip("蛇的颜色类型，用于匹配洞的颜色")]
        public SnakeColorType ColorType = SnakeColorType.Red; // 蛇的颜色类型
        
        [Header("移动设置")]
        public float MoveSpeedCellsPerSecond = 16f;
        public bool IsControllable = true;

        [Header("调试选项")]
        public bool ShowDebugStats = false;
        public bool DrawDebugGizmos = false;

        // 保护成员，子类可以访问
        protected GridConfig _grid;
        protected GridEntityManager _entityManager;
        protected SnakeManager _snakeManager; // 蛇管理器引用，用于碰撞检测

        protected Vector2Int _currentHeadCell;
        protected Vector2Int _currentTailCell;
        protected bool _consuming; // 洞吞噬中
        
        // 状态相关
        protected SnakeState _state = SnakeState.Alive;

        // 公共属性
        public string Name { get; set; } = "Snake";
        public float MoveSpeed => MoveSpeedCellsPerSecond;
        public SnakeState State => _state;
        public Vector2Int CurrentHeadCell => _currentHeadCell;
        public Vector2Int CurrentTailCell => _currentTailCell;
        public bool IsAlive() => _state == SnakeState.Alive;
        public bool IsDead() => _state == SnakeState.Dead;
        public bool IsConsuming() => _consuming;

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

        protected virtual void OnDestroy()
        {
            // 清理资源
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