using UnityEngine;
using ReGecko.GridSystem;
using ReGecko.Levels;
using ReGecko.SnakeSystem;
using ReGecko.Grid.Entities;
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

        // 管理的游戏对象
        readonly List<SnakeController> _snakes = new List<SnakeController>();
        readonly List<GridEntity> _entities = new List<GridEntity>();
        GridEntityManager _entityManager;

        LevelConfig _currentLevel;

        public void Initialize(LevelConfig level)
        {
            _currentLevel = level;
            SetupCanvases();
            SetupContainers();
            SetupEntityManager();
        }

        void SetupCanvases()
        {
            // 使用单一Canvas，通过sortingOrder控制层级
            var canvasGo = new GameObject("GameCanvas");
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.overrideSorting = false; // 不覆盖排序，使用父Canvas的设置

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
            var entityManagerGo = new GameObject("EntityManager");
            entityManagerGo.transform.SetParent(transform, false);
            _entityManager = entityManagerGo.AddComponent<GridEntityManager>();
            _entityManager.Grid = _currentLevel.Grid;
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

            // 创建蛇
            BuildSnakes();

            // 更新所有蛇的网格配置（确保它们使用正确的自适应尺寸）
            UpdateSnakeGridConfigs();
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
                entity.Cell = entityConfig.Cell;
                entity.Sprite = entityConfig.Sprite;

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

                _entityManager.Register(entity);
                _entities.Add(entity);
            }
        }

        void BuildSnakes()
        {
            if (_currentLevel?.Snakes == null) return;

            for (int i = 0; i < _currentLevel.Snakes.Length; i++)
            {
                CreateSnake(_currentLevel.Snakes[i], i);
            }
        }

        void CreateSnake(SnakeInitConfig snakeConfig, int index)
        {
            var snakeGo = new GameObject($"Snake_{index}");

            // 将蛇放到网格容器中，以便使用相同的坐标系
            var gridContainer = GridRenderer?.GetGridContainer();
            if (gridContainer != null)
            {
                snakeGo.transform.SetParent(gridContainer, false);
            }
            else
            {
                // Fallback: 直接放在GridCanvas下
                snakeGo.transform.SetParent(GridCanvas.transform, false);
            }

            // 确保有RectTransform组件（UI系统必需）
            if (snakeGo.GetComponent<RectTransform>() == null)
            {
                snakeGo.AddComponent<RectTransform>();
            }

            var snake = snakeGo.AddComponent<SnakeController>();
            snake.BodySprite = snakeConfig.BodySprite;
            snake.BodyColor = snakeConfig.Color;
            snake.Length = Mathf.Max(1, snakeConfig.Length);
            snake.HeadCell = snakeConfig.HeadCell;
            snake.InitialBodyCells = snakeConfig.BodyCells;



            // 传入实体管理器供蛇使用（使用GridRenderer中更新后的配置）
            snake.Initialize(GridRenderer.Config, _entityManager);

            _snakes.Add(snake);
        }

        void UpdateSnakeGridConfigs()
        {
            if (GridRenderer == null) return;

            // 获取更新后的网格配置（包含自适应的CellSize）
            var updatedGrid = GridRenderer.Config; // 使用GridRenderer中更新后的配置，而不是原始的_currentLevel.Grid



            // 更新所有蛇的网格配置
            foreach (var snake in _snakes)
            {
                if (snake != null)
                {

                    snake.UpdateGridConfig(updatedGrid);
                }
            }
        }

        public void ClearGame()
        {
            // 清理蛇
            foreach (var snake in _snakes)
            {
                if (snake != null)
                {
                    if (Application.isPlaying) Destroy(snake.gameObject);
                    else DestroyImmediate(snake.gameObject);
                }
            }
            _snakes.Clear();

            // 清理实体
            foreach (var entity in _entities)
            {
                if (entity != null)
                {
                    if (Application.isPlaying) Destroy(entity.gameObject);
                    else DestroyImmediate(entity.gameObject);
                }
            }
            _entities.Clear();

            // 清理网格
            if (GridRenderer != null)
            {
                GridRenderer.ClearGrid();
            }
        }

        //public void SetCellSprite(Sprite cellSprite)
        //{
        //    if (GridRenderer != null)
        //    {
        //        GridRenderer.CellSprite = cellSprite;
        //    }
        //}

        public GridEntityManager GetEntityManager() => _entityManager;
        public List<SnakeController> GetSnakes() => _snakes;

        void OnDestroy()
        {
            ClearGame();
        }
    }
}
