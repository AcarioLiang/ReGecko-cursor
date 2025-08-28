using UnityEngine;
using UnityEngine.UI;
using ReGecko.GridSystem;
using ReGecko.Levels;
using System.Collections.Generic;

namespace ReGecko.Framework.UI
{
	/// <summary>
	/// 游戏渲染管理器：在UI系统中渲染网格、蛇和游戏实体
	/// </summary>
	public class GameRenderer : MonoBehaviour
	{
		[Header("渲染设置")]
		public Canvas GameCanvas; // 游戏专用Canvas
		public RenderTexture GameRenderTexture; // 渲染纹理
		public Camera GameCamera; // 游戏专用摄像机
		
		[Header("层级设置")]
		public int GridSortingOrder = 0;
		public int EntitySortingOrder = 10;
		public int SnakeSortingOrder = 20;
		
		// 内部组件
		GridConfig _gridConfig;
		Sprite _cellSprite;
		readonly List<GameObject> _gridCells = new List<GameObject>();
		readonly List<GameObject> _gameObjects = new List<GameObject>();
		
		Transform _gridRoot;
		Transform _entityRoot;
		Transform _snakeRoot;

		public void Initialize(GridConfig gridConfig, Sprite cellSprite)
		{
			_gridConfig = gridConfig;
			_cellSprite = cellSprite;
			
			SetupCanvas();
			SetupCamera();
			SetupRootObjects();
		}
		
		void SetupCanvas()
		{
			if (GameCanvas == null)
			{
				var canvasGo = new GameObject("GameCanvas");
				canvasGo.transform.SetParent(transform, false);
				GameCanvas = canvasGo.AddComponent<Canvas>();
				GameCanvas.renderMode = RenderMode.ScreenSpaceCamera;
				GameCanvas.sortingLayerName = "Default";
				GameCanvas.sortingOrder = -100; // 确保在游戏UI下方
			}
		}
		
		void SetupCamera()
		{
			if (GameCamera == null)
			{
				var cameraGo = new GameObject("GameCamera");
				cameraGo.transform.SetParent(transform, false);
				GameCamera = cameraGo.AddComponent<Camera>();
			}
			
			GameCamera.orthographic = true;
			GameCamera.clearFlags = CameraClearFlags.SolidColor;
			GameCamera.backgroundColor = new Color(0.1f, 0.1f, 0.15f, 1f);
			GameCamera.cullingMask = 1 << LayerMask.NameToLayer("Default"); // 只渲染默认层
			
			// 设置摄像机位置和尺寸
			if (_gridConfig .IsValid())
			{
				var center = _gridConfig.GetGridCenterWorld();
				GameCamera.transform.position = new Vector3(center.x, center.y, -10f);
				
				float worldW = _gridConfig.Width * _gridConfig.CellSize;
				float worldH = _gridConfig.Height * _gridConfig.CellSize;
				float aspect = (float)Screen.width / Screen.height;
				float sizeH = worldH * 0.5f + 1f;
				float sizeW = worldW * 0.5f / aspect + 1f;
				GameCamera.orthographicSize = Mathf.Max(sizeH, sizeW);
			}
			
			// 设置Canvas摄像机
			GameCanvas.worldCamera = GameCamera;
		}
		
		void SetupRootObjects()
		{
			// 创建分层根对象
			_gridRoot = CreateRoot("GridRoot", GridSortingOrder);
			_entityRoot = CreateRoot("EntityRoot", EntitySortingOrder);
			_snakeRoot = CreateRoot("SnakeRoot", SnakeSortingOrder);
		}
		
		Transform CreateRoot(string name, int sortingOrder)
		{
			var go = new GameObject(name);
			go.transform.SetParent(GameCanvas.transform, false);
			
			// 添加Canvas组件用于排序
			var canvas = go.AddComponent<Canvas>();
			canvas.overrideSorting = true;
			canvas.sortingOrder = sortingOrder;
			
			return go.transform;
		}
		
		public void BuildGrid()
		{
			ClearGrid();
			if (!_gridConfig.IsValid() || _cellSprite == null) return;
			
			for (int y = 0; y < _gridConfig.Height; y++)
			{
				for (int x = 0; x < _gridConfig.Width; x++)
				{
					CreateGridCell(x, y);
				}
			}
		}
		
		void CreateGridCell(int x, int y)
		{
			var go = new GameObject($"Cell_{x}_{y}");
			go.transform.SetParent(_gridRoot, false);
			
			// 使用Image组件替代SpriteRenderer
			var image = go.AddComponent<Image>();
			image.sprite = _cellSprite;
			image.raycastTarget = false; // 不响应射线检测
			
			// 设置RectTransform
			var rt = go.GetComponent<RectTransform>();
			var worldPos = _gridConfig.CellToWorld(new Vector2Int(x, y));
			rt.anchoredPosition = new Vector2(worldPos.x, worldPos.y);
			rt.sizeDelta = new Vector2(_gridConfig.CellSize, _gridConfig.CellSize);
			
			_gridCells.Add(go);
		}
		
		public void ClearGrid()
		{
			foreach (var cell in _gridCells)
			{
				if (cell != null)
				{
					if (Application.isPlaying) Destroy(cell);
					else DestroyImmediate(cell);
				}
			}
			_gridCells.Clear();
		}
		
		public Transform GetEntityRoot() => _entityRoot;
		public Transform GetSnakeRoot() => _snakeRoot;
		public Camera GetGameCamera() => GameCamera;
		
		/// <summary>
		/// 注册游戏对象到渲染器管理
		/// </summary>
		public void RegisterGameObject(GameObject go)
		{
			if (!_gameObjects.Contains(go))
			{
				_gameObjects.Add(go);
			}
		}
		
		/// <summary>
		/// 清理所有游戏对象
		/// </summary>
		public void ClearAll()
		{
			ClearGrid();
			
			foreach (var go in _gameObjects)
			{
				if (go != null)
				{
					if (Application.isPlaying) Destroy(go);
					else DestroyImmediate(go);
				}
			}
			_gameObjects.Clear();
		}
		
		void OnDestroy()
		{
			ClearAll();
		}
	}
}
