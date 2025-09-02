using System;

namespace ReGecko.Game
{
    /// <summary>
    /// 游戏状态枚举
    /// </summary>
    public enum GameState
    {
        /// <summary>
        /// 初始化状态 - 加载游戏场景
        /// </summary>
        Initializing,
        
        /// <summary>
        /// 游戏中状态 - 正常运行
        /// </summary>
        Playing,
        
        /// <summary>
        /// 暂停状态 - 游戏时间暂停
        /// </summary>
        Paused,
        
        /// <summary>
        /// 结束状态 - 游戏结束
        /// </summary>
        GameOver
    }

    /// <summary>
    /// 游戏状态变化事件参数
    /// </summary>
    public class GameStateChangedEventArgs : EventArgs
    {
        public GameState OldState { get; }
        public GameState NewState { get; }
        public float StateDuration { get; }

        public GameStateChangedEventArgs(GameState oldState, GameState newState, float stateDuration)
        {
            OldState = oldState;
            NewState = newState;
            StateDuration = stateDuration;
        }
    }

    /// <summary>
    /// 游戏状态变化事件
    /// </summary>
    public delegate void GameStateChangedEventHandler(object sender, GameStateChangedEventArgs e);
}
