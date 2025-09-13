using UnityEngine;
using ReGecko.GridSystem;
using ReGecko.Levels;
using ReGecko.SnakeSystem;
using ReGecko.Grid.Entities;
using ReGecko.Game;
using System.Collections;
using System.Collections.Generic;

namespace ReGecko.Framework.UI
{
    /// <summary>
    /// UI游戏管理器：管理游戏在UI系统中的渲染和交互
    /// </summary>
    public class UIGameManager : MonoBehaviour
    {
        [Header("组件引用")]
        public UIGridRenderer GridRenderer;
        public Transform EntityContainer;
        public Transform SnakeContainer;

        [Header("层级设置")]
        public Canvas GridCanvas;
        public Canvas EntityCanvas;
        public Canvas SnakeCanvas;

        GameStateController _gameStateController;
        LevelConfig _currentLevel;

        public void Initialize(LevelConfig level)
        {
            _currentLevel = level;
            SetupCanvases();
            SetupContainers();
            SetupEntityManager();
            SetupGameStateController();
        }

        void SetupCanvases()
        {
            // 使用单一Canvas，通过sortingOrder控制层级
            var canvasGo = new GameObject("GameCanvas");
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.overrideSorting = false; // 不覆盖排序，使用父Canvas的设置
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;

            // 设置为填充父容器 (GameRenderArea)
            var rt = canvasGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero; // 确保位置为父容器中心

            // 所有Canvas都指向同一个
            GridCanvas = EntityCanvas = SnakeCanvas = canvas;
        }

        Canvas CreateLayerCanvas(string name, int sortingOrder)
        {
            var canvasGo = new GameObject(name);
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;

            // 添加GraphicRaycaster以支持UI事件
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // 设置为全屏
            var rt = canvasGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            return canvas;
        }

        void SetupContainers()
        {
            // 设置网格渲染器 - 直接在GameCanvas下
            if (GridRenderer == null)
            {
                var gridGo = new GameObject("GridRenderer");
                gridGo.transform.SetParent(GridCanvas.transform, false);

                // 设置RectTransform填满父容器
                var gridRT = gridGo.GetComponent<RectTransform>();
                if (gridRT == null)
                {
                    gridRT = gridGo.AddComponent<RectTransform>();
                }

                // 让GridRenderer填满整个Canvas
                gridRT.anchorMin = Vector2.zero;
                gridRT.anchorMax = Vector2.one;
                gridRT.offsetMin = Vector2.zero;
                gridRT.offsetMax = Vector2.zero;
                gridRT.anchoredPosition = Vector2.zero;

                GridRenderer = gridGo.AddComponent<UIGridRenderer>();
            }

            // 容器不再需要，所有对象都直接在GridRenderer的容器中
            EntityContainer = null;
            SnakeContainer = null;
        }

        Transform CreateContainer(string name, Transform parent)
        {
            var containerGo = new GameObject(name);
            containerGo.transform.SetParent(parent, false);

            // 添加RectTransform组件（UI系统必需）
            var rt = containerGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            return containerGo.transform;
        }

        void SetupEntityManager()
        {
            GridEntityManager.Instance.Init(_currentLevel.Grid);
        }

        void SetupSnakeManager()
        {
            if (GridRenderer == null) return;

            // 获取蛇容器
            var snakeContainer = GridRenderer.GetGridContainer();
            if (snakeContainer == null && GridCanvas != null)
            {
                snakeContainer = GridCanvas.transform;
            }

            // 注意：蛇的实际创建延迟到网格构建完成后
            // 现在网格已经构建完成，使用正确的配置初始化蛇管理器
            SnakeManager.Instance.Init(_currentLevel, GridRenderer.Config, GridCanvas, snakeContainer);

            Debug.Log($"蛇管理器初始化完成，CellSize: {GridRenderer.Config.CellSize}, 蛇数量: {SnakeManager.Instance.GetStats().TotalCount}");
        }

        void SetupGameStateController()
        {
            // 创建游戏状态控制器组件
            _gameStateController = gameObject.AddComponent<GameStateController>();

            // 订阅游戏状态变化事件
            GameStateController.OnGameStateChanged += OnGameStateChanged;

            // 设置游戏时间限制（如果有的话）
            if (_currentLevel != null && _currentLevel.EnableTimeLimit && _currentLevel.GameTimeLimit > 0)
            {
                _gameStateController.SetGameTimeLimit(/*_currentLevel.GameTimeLimit*/9999);//todo
            }
        }

        public void BuildGame()
        {
            ClearGame();

            // 延迟构建，确保网格完成后再创建其他对象
            StartCoroutine(BuildGameDelayed());
        }

        IEnumerator BuildGameDelayed()
        {
            // 构建网格
            BuildGrid();

            // 等待三帧，确保网格构建完成并且尺寸计算完毕
            yield return null;
            yield return null;
            yield return null;

            // 验证网格是否构建完成
            if (GridRenderer == null || GridRenderer.GetAdaptiveCellSize() <= 0)
            {
                Debug.LogError("Grid not ready, delaying entity creation");
                yield return null;
            }



            // 创建游戏实体
            BuildEntities();

            // 现在网格已经构建完成，初始化蛇管理器并创建蛇
            InitializeSnakesAfterGrid();
        }

        void BuildGrid()
        {
            if (GridRenderer != null && _currentLevel != null)
            {
                GridRenderer.Config = _currentLevel.Grid;
                // 这里需要从外部设置CellSprite
                GridRenderer.BuildGrid();
            }
        }

        void BuildEntities()
        {
            if (_currentLevel?.Entities == null) return;

            foreach (var entityConfig in _currentLevel.Entities)
            {
                CreateEntity(entityConfig);
            }
        }

        void CreateEntity(GridEntityConfig entityConfig)
        {
            var entityGo = new GameObject($"{entityConfig.Type}_{entityConfig.Cell.x}_{entityConfig.Cell.y}");

            // 将实体放到网格容器中，以便使用相同的坐标系
            var gridContainer = GridRenderer?.GetGridContainer();
            if (gridContainer != null)
            {
                entityGo.transform.SetParent(gridContainer, false);
            }
            else
            {
                // Fallback: 直接放在GridCanvas下
                entityGo.transform.SetParent(GridCanvas.transform, false);
                Debug.LogWarning("GridContainer is null, using GridCanvas as fallback");
            }

            // 确保有RectTransform组件（UI系统必需）
            var rt = entityGo.GetComponent<RectTransform>();
            if (rt == null)
            {
                rt = entityGo.AddComponent<RectTransform>();
            }

            // 设置正确的锚点和轴心（居中）
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            GridEntity entity = null;
            switch (entityConfig.Type)
            {
                case GridEntityConfig.EntityType.Wall:
                    entity = entityGo.AddComponent<WallEntity>();
                    break;
                case GridEntityConfig.EntityType.Hole:
                    entity = entityGo.AddComponent<HoleEntity>();
                    break;
                case GridEntityConfig.EntityType.Item:
                    entity = entityGo.AddComponent<ItemEntity>();
                    break;
            }

            if (entity != null)
            {
                entity.GameObj = entityGo;
                entity.Cell = entityConfig.Cell;
                entity.Sprite = entityConfig.Sprite;
                entity.Blocking = false;

                // 如果是洞实体，设置颜色类型
                if (entity is HoleEntity holeEntity)
                {
                    holeEntity.ColorType = entityConfig.ColorType;
                    Debug.Log($"创建洞实体 {entityConfig.Cell}，颜色类型：{entityConfig.ColorType.GetDisplayName()}");
                }

                // 使用Image组件替代SpriteRenderer
                var image = entityGo.AddComponent<UnityEngine.UI.Image>();
                image.sprite = entityConfig.Sprite;
                image.color = entityConfig.Color;
                image.raycastTarget = false;

                // 设置位置和尺寸（使用Grid坐标系）
                float adaptiveCellSize = GridRenderer.GetAdaptiveCellSize();

                if (adaptiveCellSize <= 0)
                {
                    Debug.LogError($"Invalid adaptive cell size: {adaptiveCellSize} for entity {entityConfig.Type}_{entityConfig.Cell.x}_{entityConfig.Cell.y}");
                    adaptiveCellSize = 50f; // fallback
                }

                // 使用GridRenderer中更新后的Grid坐标系计算位置
                Vector3 worldPos = GridRenderer.Config.CellToWorld(entityConfig.Cell);
                rt.anchoredPosition = new Vector2(worldPos.x, worldPos.y);
                rt.sizeDelta = new Vector2(adaptiveCellSize, adaptiveCellSize);

                GridEntityManager.Instance.Register(entity);
            }
        }



        void InitializeSnakesAfterGrid()
        {
            SetupSnakeManager();

            // 初始化完成后，开始游戏
            if (_gameStateController != null)
            {
                _gameStateController.StartGame();
            }
        }

        void UpdateSnakeGridConfigs()
        {
            if (GridRenderer == null ) return;

            // 获取更新后的网格配置（包含自适应的CellSize）
            var updatedGrid = GridRenderer.Config;

            // 更新蛇管理器的网格配置
            SnakeManager.Instance.UpdateGridConfig(updatedGrid);
        }

        public void ClearGame()
        {
            // 清理蛇
            SnakeManager.Instance.ClearAllSnakes();
            GridEntityManager.Instance.ClearAllEntities();

            // 清理网格
            if (GridRenderer != null)
            {
                GridRenderer.ClearGrid();
            }

            SnakeManager.Instance.DestroyInstance();
            GridEntityManager.Instance.DestroyInstance();
        }

        public GameStateController GetGameStateController() => _gameStateController;

        /// <summary>
        /// 游戏状态变化事件处理
        /// </summary>
        void OnGameStateChanged(object sender, GameStateChangedEventArgs e)
        {
            Debug.Log($"游戏状态变化: {e.OldState} -> {e.NewState}");

            switch (e.NewState)
            {
                case GameState.Initializing:
                    OnEnterInitializingState();
                    break;
                case GameState.Playing:
                    OnEnterPlayingState();
                    break;
                case GameState.Paused:
                    OnEnterPausedState();
                    break;
                case GameState.GameOver:
                    OnEnterGameOverState();
                    break;
            }
        }

        #region 游戏状态处理

        void OnEnterInitializingState()
        {
            Debug.Log("UI管理器: 进入初始化状态");
            // 可以在这里显示加载界面
        }

        void OnEnterPlayingState()
        {
            Debug.Log("UI管理器: 进入游戏中状态");
            // 可以在这里隐藏暂停界面，显示游戏UI
        }

        void OnEnterPausedState()
        {
            Debug.Log("UI管理器: 进入暂停状态");
            // 可以在这里显示暂停界面
        }

        void OnEnterGameOverState()
        {
            Debug.Log("UI管理器: 进入游戏结束状态");
            // 可以在这里显示游戏结束界面
        }

        #endregion

        void OnDestroy()
        {
            // 取消订阅事件
            if (_gameStateController != null)
            {
                GameStateController.OnGameStateChanged -= OnGameStateChanged;
            }

            ClearGame();

        }
    }
}
