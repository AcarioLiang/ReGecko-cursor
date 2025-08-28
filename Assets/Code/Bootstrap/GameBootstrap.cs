using UnityEngine;
using ReGecko.GridSystem;
using ReGecko.Levels;
using ReGecko.SnakeSystem;
using ReGecko.GameCore.Flow;
using ReGecko.Grid.Entities;
using ReGecko.Framework.UI;

namespace ReGecko.Bootstrap
{
	public class GameBootstrap : MonoBehaviour
	{
		public DummyLevelProvider LevelProvider;
		public GridRenderer GridRenderer;
		public Camera SceneCamera;
		
		GridEntityManager _entityManager;

		void Awake()
		{
			if (LevelProvider == null) LevelProvider = FindObjectOfType<DummyLevelProvider>();
			if (GridRenderer == null) GridRenderer = FindObjectOfType<GridRenderer>();
			if (SceneCamera == null) SceneCamera = Camera.main;
		}

		void Start()
		{
			var level = GameContext.CurrentLevelConfig != null ? GameContext.CurrentLevelConfig : LevelProvider != null ? LevelProvider.GetLevel() : new LevelConfig();
			
			// 初始化实体管理器
			var entityManagerGo = new GameObject("EntityManager");
			_entityManager = entityManagerGo.AddComponent<GridEntityManager>();
			_entityManager.Grid = level.Grid;
			
			// 构建网格
			GridRenderer.Config = level.Grid;
			GridRenderer.CellSprite = LevelProvider.GridCellSprite;
			GridRenderer.BuildGrid();

			// 初始化摄像机
			SetupCamera(level.Grid);

			// 初始化蛇（临时只一条）
			if (level.Snakes != null)
			{
				for (int i = 0; i < level.Snakes.Length; i++)
				{
					var snakeCfg = level.Snakes[i];
					var go = new GameObject($"Snake_{i}");
					var snake = go.AddComponent<SnakeController>();
					snake.BodySprite = snakeCfg.BodySprite;
					snake.BodyColor = snakeCfg.Color;
					snake.Length = Mathf.Max(1, snakeCfg.Length);
					snake.HeadCell = snakeCfg.HeadCell;
					snake.InitialBodyCells = snakeCfg.BodyCells;
					snake.Initialize(level.Grid);
				}
			}

			// 初始化网格实体
			if (level.Entities != null)
			{
				for (int i = 0; i < level.Entities.Length; i++)
				{
					var entityCfg = level.Entities[i];
					var go = new GameObject($"{entityCfg.Type}_{i}");
					
					GridEntity entity = null;
					switch (entityCfg.Type)
					{
						case GridEntityConfig.EntityType.Wall:
							entity = go.AddComponent<WallEntity>();
							break;
						case GridEntityConfig.EntityType.Hole:
							entity = go.AddComponent<HoleEntity>();
							break;
						case GridEntityConfig.EntityType.Item:
							entity = go.AddComponent<ItemEntity>();
							break;
					}
					
					if (entity != null)
					{
						entity.Cell = entityCfg.Cell;
						entity.Sprite = entityCfg.Sprite;
						var sr = entity.GetComponent<SpriteRenderer>();
						if (sr != null) sr.color = entityCfg.Color;
						
						// 注册到管理器
						_entityManager.Register(entity);
					}
				}
			}
			
			// 显示预加载的UI
			if (GameContext.PreloadedUIPrefab != null)
			{
				UIManager.Instance.Show("GameplayHUD", GameContext.PreloadedUIPrefab);
			}
		}

		void SetupCamera(GridConfig grid)
		{
			if (SceneCamera == null) return;
			SceneCamera.orthographic = true;
			var center = grid.GetGridCenterWorld();
			SceneCamera.transform.position = new Vector3(center.x, center.y, -10f);
			// 调整尺寸以包住网格
			float worldW = (grid.Width) * grid.CellSize;
			float worldH = (grid.Height) * grid.CellSize;
			float aspect = (float)Screen.width / Screen.height;
			float sizeH = worldH * 0.5f + 1f;
			float sizeW = worldW * 0.5f / aspect + 1f;
			SceneCamera.orthographicSize = Mathf.Max(sizeH, sizeW);
		}
	}
}


