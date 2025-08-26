using UnityEngine;
using System.Collections.Generic;

namespace ReGecko.GridSystem
{
	public class GridRenderer : MonoBehaviour
	{
		public GridConfig Config;
		public Sprite CellSprite;
		public Transform GridRoot;
		public float CellZ = 1f;

		readonly List<GameObject> _cells = new List<GameObject>();

		public void BuildGrid()
		{
			Clear();
			if (GridRoot == null)
			{
				var root = new GameObject("GridRoot");
				root.transform.SetParent(transform, false);
				GridRoot = root.transform;
			}
			for (int y = 0; y < Config.Height; y++)
			{
				for (int x = 0; x < Config.Width; x++)
				{
					var go = new GameObject($"Cell_{x}_{y}");
					go.transform.SetParent(GridRoot, false);
					var sr = go.AddComponent<SpriteRenderer>();
					sr.sprite = CellSprite;
					sr.sortingOrder = -100;
					var pos = Config.CellToWorld(new Vector2Int(x, y));
					pos.z = CellZ;
					go.transform.position = pos;
					_cells.Add(go);
				}
			}
		}

		public void Clear()
		{
			for (int i = 0; i < _cells.Count; i++)
			{
				if (_cells[i] != null)
				{
					if (Application.isPlaying) Destroy(_cells[i]); else DestroyImmediate(_cells[i]);
				}
			}
			_cells.Clear();
			if (GridRoot != null)
			{
				if (Application.isPlaying) Destroy(GridRoot.gameObject); else DestroyImmediate(GridRoot.gameObject);
				GridRoot = null;
			}
		}
	}
}


