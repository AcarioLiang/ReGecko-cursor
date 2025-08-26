using System;
using UnityEngine;

namespace ReGecko.GridSystem
{
	[Serializable]
	public struct GridConfig
	{
		public int Width;
		public int Height;
		public float CellSize;

		// 阻挡占位：暂时全部为无阻挡
		public bool HasBlock(int x, int y)
		{
			return false;
		}

		public bool IsInside(Vector2Int cell)
		{
			return cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height;
		}

		public Vector3 CellToWorld(Vector2Int cell)
		{
			return new Vector3(cell.x * CellSize, cell.y * CellSize, 0f);
		}

		public Vector2Int WorldToCell(Vector3 world)
		{
			int x = Mathf.RoundToInt(world.x / CellSize);
			int y = Mathf.RoundToInt(world.y / CellSize);
			return new Vector2Int(x, y);
		}

		public Vector3 GetGridCenterWorld()
		{
			float widthWorld = (Width - 1) * CellSize;
			float heightWorld = (Height - 1) * CellSize;
			return new Vector3(widthWorld * 0.5f, heightWorld * 0.5f, 0f);
		}
	}
}


