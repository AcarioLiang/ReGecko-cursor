using System;
using UnityEngine;
using ReGecko.GridSystem;
using ReGecko.Game;

namespace ReGecko.Levels
{
	[Serializable]
	public class SnakeInitConfig
	{
		[Header("基本信息")]
		public string Id = ""; // 蛇的唯一ID，为空时自动生成
		public string Name = "Snake"; // 蛇的显示名称
		public Color Color = Color.white;
		public Sprite BodySprite;
		
		[Header("颜色配置")]
		[Tooltip("蛇的颜色类型，用于匹配洞的颜色")]
		public SnakeColorType ColorType = SnakeColorType.Red; // 蛇的颜色类型
		
		[Header("初始配置")]
		public int Length = 5;
		public Vector2Int HeadCell = new Vector2Int(0, 0);
		// 可选：显式身体格子（含头在index 0），若为空则按Length自动生成
		public Vector2Int[] BodyCells;
		
		[Header("行为设置")]
		public float MoveSpeed = 16f; // 移动速度（格子/秒）
		public bool IsControllable = true; // 是否可被玩家控制
		public bool EnableAI = false; // 是否启用AI控制（预留）
		
		[Header("特殊属性")]
		public bool CanEatOthers = false; // 是否可以吃其他蛇（预留）
		public bool CanBeEaten = true; // 是否可以被其他蛇吃（预留）
		public int Priority = 0; // 优先级，用于碰撞判定等（预留）
	}

	[Serializable]
	public class GridEntityConfig
	{
		public enum EntityType { Wall, Hole, Item }
		public EntityType Type = EntityType.Wall;
		public Vector2Int Cell = new Vector2Int(0, 0);
		public Sprite Sprite;
		public Color Color = Color.white;
		
		[Header("颜色配置")]
		[Tooltip("洞的颜色类型，只有相同颜色的蛇才能进入")]
		public SnakeColorType ColorType = SnakeColorType.Red; // 洞的颜色类型
	}

	[Serializable]
	public class LevelConfig
	{
		[Header("关卡配置")]
		public GridConfig Grid;
		public SnakeInitConfig[] Snakes;
		public GridEntityConfig[] Entities;
		
		[Header("游戏规则")]
		[Tooltip("游戏时间限制（秒），0表示无限制")]
		public float GameTimeLimit = 0f; // 游戏时间限制
		[Tooltip("是否启用时间限制")]
		public bool EnableTimeLimit = false; // 是否启用时间限制
	}
}

