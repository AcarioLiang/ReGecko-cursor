using ReGecko.Levels;

namespace ReGecko.GameCore.Flow
{
	public static class GameContext
	{
		public static bool NextLoadIsPlayer = true; // true: 加载玩家; false: 加载关卡
		public static LevelConfig CurrentLevelConfig;
	}
}


