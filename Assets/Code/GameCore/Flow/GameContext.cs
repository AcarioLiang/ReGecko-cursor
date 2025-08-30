using UnityEngine;
using ReGecko.Levels;
using ReGecko.SnakeSystem;

namespace ReGecko.GameCore.Flow
{
	public static class GameContext
	{
		public static bool NextLoadIsPlayer = false; // true: 加载玩家; false: 加载关卡
		public static LevelConfig CurrentLevelConfig;
		public static SnakeBodySpriteConfig SnakeBodyConfig;
		public static GameObject PreloadedUIPrefab; // 预加载的UI预制体
	}
}


