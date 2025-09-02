using System;
using UnityEngine;
using ReGecko.Framework;
using ReGecko.SnakeSystem;

namespace ReGecko.Game
{
    /// <summary>
    /// 游戏状态控制器 - 管理游戏的各种状态切换
    /// </summary>
    public class GameStateController : MonoBehaviour
    {
        [Header("游戏配置")]
        [SerializeField] private float gameTimeLimit = 300f; // 游戏时间限制（秒）
        [SerializeField] private bool enableTimeLimit = true; // 是否启用时间限制
        
        [Header("调试信息")]
        [SerializeField] private GameState currentState = GameState.Initializing;
        [SerializeField] private float currentGameTime = 0f;
        [SerializeField] private float stateStartTime = 0f;

        // 事件
        public static event GameStateChangedEventHandler OnGameStateChanged;

        // 属性
        public GameState CurrentState => currentState;
        public float CurrentGameTime => currentGameTime;
        public float RemainingTime => Mathf.Max(0f, gameTimeLimit - currentGameTime);
        public bool IsTimeUp => enableTimeLimit && currentGameTime >= gameTimeLimit;
        public bool IsGameActive => currentState == GameState.Playing;
        public bool IsGamePaused => currentState == GameState.Paused;
        public bool IsGameOver => currentState == GameState.GameOver;

        // 私有字段
        private float _stateDuration = 0f;
        private bool _isInitialized = false;

        private void Awake()
        {
            // 确保只有一个实例
            if (FindObjectsOfType<GameStateController>().Length > 1)
            {
                Destroy(gameObject);
                return;
            }
            
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            Initialize();
        }

        private void Update()
        {
            UpdateGameTime();
            CheckGameConditions();
        }

        /// <summary>
        /// 初始化游戏状态控制器
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized) return;

            currentState = GameState.Initializing;
            currentGameTime = 0f;
            stateStartTime = Time.time;
            _stateDuration = 0f;
            _isInitialized = true;

            Debug.Log("游戏状态控制器初始化完成");
        }

        /// <summary>
        /// 切换到指定状态
        /// </summary>
        public void ChangeState(GameState newState)
        {
            if (currentState == newState) return;

            GameState oldState = currentState;
            _stateDuration = Time.time - stateStartTime;

            // 退出当前状态
            OnExitState(oldState, newState);

            // 切换状态
            currentState = newState;
            stateStartTime = Time.time;

            // 进入新状态
            OnEnterState(oldState, newState);

            // 触发事件
            OnGameStateChanged?.Invoke(this, new GameStateChangedEventArgs(oldState, newState, _stateDuration));

            Debug.Log($"游戏状态切换: {oldState} -> {newState} (持续时间: {_stateDuration:F2}秒)");
        }

        /// <summary>
        /// 开始游戏
        /// </summary>
        public void StartGame()
        {
            if (currentState == GameState.Initializing)
            {
                ChangeState(GameState.Playing);
            }
        }

        /// <summary>
        /// 暂停游戏
        /// </summary>
        public void PauseGame()
        {
            if (currentState == GameState.Playing)
            {
                ChangeState(GameState.Paused);
            }
        }

        /// <summary>
        /// 恢复游戏
        /// </summary>
        public void ResumeGame()
        {
            if (currentState == GameState.Paused)
            {
                ChangeState(GameState.Playing);
            }
        }

        /// <summary>
        /// 切换暂停状态
        /// </summary>
        public void TogglePause()
        {
            if (currentState == GameState.Playing)
            {
                PauseGame();
            }
            else if (currentState == GameState.Paused)
            {
                ResumeGame();
            }
        }

        /// <summary>
        /// 结束游戏
        /// </summary>
        public void EndGame()
        {
            if (currentState == GameState.Playing || currentState == GameState.Paused)
            {
                ChangeState(GameState.GameOver);
            }
        }

        /// <summary>
        /// 重新开始游戏
        /// </summary>
        public void RestartGame()
        {
            currentGameTime = 0f;
            ChangeState(GameState.Initializing);
        }

        /// <summary>
        /// 设置游戏时间限制
        /// </summary>
        public void SetGameTimeLimit(float timeLimit)
        {
            gameTimeLimit = Mathf.Max(0f, timeLimit);
            enableTimeLimit = timeLimit > 0f;
        }

        /// <summary>
        /// 获取状态持续时间
        /// </summary>
        public float GetStateDuration()
        {
            return Time.time - stateStartTime;
        }

        /// <summary>
        /// 更新游戏时间
        /// </summary>
        private void UpdateGameTime()
        {
            if (currentState == GameState.Playing)
            {
                currentGameTime += Time.deltaTime;
            }
        }

        /// <summary>
        /// 检查游戏结束条件
        /// </summary>
        private void CheckGameConditions()
        {
            if (currentState != GameState.Playing) return;

            // 检查时间限制
            if (IsTimeUp)
            {
                Debug.Log("游戏时间结束");
                EndGame();
                return;
            }

            // 检查蛇是否全部消失
            if (AreAllSnakesDead())
            {
                Debug.Log("所有蛇都已死亡");
                EndGame();
                return;
            }
        }

        /// <summary>
        /// 检查是否所有蛇都已死亡
        /// </summary>
        private bool AreAllSnakesDead()
        {
            // 查找SnakeManager
            var snakeManager = FindObjectOfType<SnakeManager>();
            if (snakeManager == null) return false;

            var aliveSnakes = snakeManager.GetAliveSnakes();
            return aliveSnakes.Count == 0;
        }

        /// <summary>
        /// 进入状态时的处理
        /// </summary>
        private void OnEnterState(GameState oldState, GameState newState)
        {
            switch (newState)
            {
                case GameState.Initializing:
                    OnEnterInitializing();
                    break;
                case GameState.Playing:
                    OnEnterPlaying();
                    break;
                case GameState.Paused:
                    OnEnterPaused();
                    break;
                case GameState.GameOver:
                    OnEnterGameOver();
                    break;
            }
        }

        /// <summary>
        /// 退出状态时的处理
        /// </summary>
        private void OnExitState(GameState oldState, GameState newState)
        {
            switch (oldState)
            {
                case GameState.Initializing:
                    OnExitInitializing();
                    break;
                case GameState.Playing:
                    OnExitPlaying();
                    break;
                case GameState.Paused:
                    OnExitPaused();
                    break;
                case GameState.GameOver:
                    OnExitGameOver();
                    break;
            }
        }

        #region 状态进入/退出处理

        private void OnEnterInitializing()
        {
            Debug.Log("进入初始化状态 - 开始加载游戏场景");
            // 这里可以触发场景加载逻辑
        }

        private void OnExitInitializing()
        {
            Debug.Log("退出初始化状态");
        }

        private void OnEnterPlaying()
        {
            Debug.Log("进入游戏中状态 - 游戏开始运行");
            Time.timeScale = 1f; // 确保时间正常流动
        }

        private void OnExitPlaying()
        {
            Debug.Log("退出游戏中状态");
        }

        private void OnEnterPaused()
        {
            Debug.Log("进入暂停状态 - 游戏时间暂停");
            Time.timeScale = 0f; // 暂停游戏时间
        }

        private void OnExitPaused()
        {
            Debug.Log("退出暂停状态");
            Time.timeScale = 1f; // 恢复游戏时间
        }

        private void OnEnterGameOver()
        {
            Debug.Log("进入游戏结束状态");
            Time.timeScale = 0f; // 暂停游戏时间
        }

        private void OnExitGameOver()
        {
            Debug.Log("退出游戏结束状态");
            Time.timeScale = 1f; // 恢复游戏时间
        }

        #endregion

        #region 调试和编辑器支持

        [ContextMenu("切换到初始化状态")]
        private void DebugChangeToInitializing() => ChangeState(GameState.Initializing);

        [ContextMenu("切换到游戏中状态")]
        private void DebugChangeToPlaying() => ChangeState(GameState.Playing);

        [ContextMenu("切换到暂停状态")]
        private void DebugChangeToPaused() => ChangeState(GameState.Paused);

        [ContextMenu("切换到结束状态")]
        private void DebugChangeToGameOver() => ChangeState(GameState.GameOver);

        [ContextMenu("切换暂停状态")]
        private void DebugTogglePause() => TogglePause();

        private void OnValidate()
        {
            // 在编辑器中验证配置
            gameTimeLimit = Mathf.Max(0f, gameTimeLimit);
        }

        #endregion
    }
}
