using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ReGecko.GridSystem;
using ReGecko.Grid.Entities;
using ReGecko.Game;
using System.Collections;
using ReGecko.GameCore.Flow;
using ReGecko.Framework.Resources;

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
        public Vector2Int InitialHeadCell;         //初始化配置-头部
        public Vector2Int[] InitialBodyCells; // 初始化配置-蛇位置-含头在index 0，可为空

        [Header("配置文件")]
        [Tooltip("蛇身体图片配置文件（可选）")]
        public SnakeBodySpriteConfig Config;


        [Header("颜色配置")]
        [Tooltip("蛇的颜色类型，用于匹配洞的颜色")]
        public SnakeColorType ColorType = SnakeColorType.Red; // 蛇的颜色类型
        
        [Header("移动设置")]
        public float MoveSpeedCellsPerSecond = 10f;
        
        [Header("功能开关")]
        public bool EnableBodySpriteManagement = true;
        public bool IsControllable = true; // 是否可以被玩家控制
        
        [Header("调试选项")]
        public bool ShowDebugStats = false;
        public bool DrawDebugGizmos = false;

        // 保护成员，子类可以访问
        protected GridConfig _grid;
        protected SnakeBodySpriteManager _bodySpriteManager;

        protected bool _consuming; // 洞吞噬中
        
        // 状态相关
        protected SnakeState _state = SnakeState.Alive;

        // 公共属性
        public string Name { get; set; } = "Snake";
        public float MoveSpeed => MoveSpeedCellsPerSecond;


        private bool _IsDragging = false;
        public bool IsDragging
        {
            get { return _IsDragging; }
            set { _IsDragging = value; }
        }

        private bool _dragFromHead = false;
        public bool DragFromHead
        {
            get { return _dragFromHead; }
            set { _dragFromHead = value; }
        }

        public SnakeState State => _state;

        public bool IsAlive() => _state == SnakeState.Alive;
        public bool IsDead() => _state == SnakeState.Dead;
        public bool IsConsuming() => _consuming;

        public virtual LinkedList<Vector2Int> GetBodyCells() => null;
        public virtual List<GameObject> GetSegments() => null;

        // 抽象方法，子类必须实现
        public abstract void Initialize(GridConfig grid);

        protected virtual void InitializeBodySpriteManager()
        {
            if (!EnableBodySpriteManagement) return;

            var bodySpriteGo = new GameObject("BodySpriteManager");
            bodySpriteGo.transform.SetParent(transform, false);
            _bodySpriteManager = bodySpriteGo.AddComponent<SnakeBodySpriteManager>();
            // SnakeBodySpriteManager会通过GetComponent获取SnakeController

            //Material newMaterial = new Material(Shader.Find("Custom/SpriteNineSlice"));
            //newMaterial.mainTexture = ResourceManager.LoadPNG(ResourceDefine.Path_PNG_Snake_Body).texture;
            //newMaterial.color = Color.white;

            Material newMaterial = Resources.Load<Material>("SnakeBody");
            _bodySpriteManager.BodyLineMaterial = newMaterial;

        }

        public abstract void UpdateMovement();

        // 虚方法，子类可以重写
        public virtual void UpdateGridConfig(GridConfig newGrid)
        {
            _grid = newGrid;
        }

        public abstract void SnapCellsToGrid();

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


        protected virtual void OnDestroy()
        {

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