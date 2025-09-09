using UnityEngine;
using ReGecko.Levels;
using ReGecko.SnakeSystem;

namespace ReGecko.GameCore.Flow
{
    public static class GameContext
    {

        public static int PlayerMaxLevel = 3; // 测试玩家最大等级
        public static bool BootUp = true; // true: 显示开始按钮
        public static bool NextLoadIsPlayer = true; // true: 加载玩家; false: 加载关卡
        public static LevelConfig CurrentLevelConfig;
        public static SnakeBodySpriteConfig SnakeBodyConfig;
        public static GameObject PreloadedUIPrefab_GameMain; // 预加载的UI预制体
        public static GameObject PreloadedUIPrefab_GameFaild; // 预加载的UI预制体
        public static GameObject PreloadedUIPrefab_GameSuccess; // 预加载的UI预制体
        public static GameObject PreloadedUIPrefab_GameSetting; // 
        public static GameObject PreloadedUIPrefab_Lobby; // 
    }
}


