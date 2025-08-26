using UnityEngine;
using ReGecko.GridSystem;
using ReGecko.Levels;
using ReGecko.SnakeSystem;

namespace ReGecko.Bootstrap
{
	public class GameBootstrap : MonoBehaviour
	{
		public DummyLevelProvider LevelProvider;
		public GridRenderer GridRenderer;
		public Camera SceneCamera;

		void Awake()
		{
			if (LevelProvider == null) LevelProvider = FindObjectOfType<DummyLevelProvider>();
			if (GridRenderer == null) GridRenderer = FindObjectOfType<GridRenderer>();
			if (SceneCamera == null) SceneCamera = Camera.main;
		}

		void Start()
		{
			var level = LevelProvider.GetLevel();
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
					snake.Initialize(level.Grid);
				}
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


