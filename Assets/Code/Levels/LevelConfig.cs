using System;
using UnityEngine;
using ReGecko.GridSystem;

namespace ReGecko.Levels
{
	[Serializable]
	public class SnakeInitConfig
	{
		public Color Color = Color.white;
		public Sprite BodySprite;
		public int Length = 5;
		public Vector2Int HeadCell = new Vector2Int(0, 0);
		// 可选：显式身体格子（含头在index 0），若为空则按Length自动生成
		public Vector2Int[] BodyCells;
	}

	[Serializable]
	public class GridEntityConfig
	{
		public enum EntityType { Wall, Hole, Item }
		public EntityType Type = EntityType.Wall;
		public Vector2Int Cell = new Vector2Int(0, 0);
		public Sprite Sprite;
		public Color Color = Color.white;
	}

	[Serializable]
	public class LevelConfig
	{
		public GridConfig Grid;
		public SnakeInitConfig[] Snakes;
		public GridEntityConfig[] Entities;
	}
}


