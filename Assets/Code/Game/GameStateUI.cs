using UnityEngine;
using UnityEngine.UI;
using ReGecko.Game;
using ReGecko.SnakeSystem;
using ReGecko.GameCore.Flow;

namespace ReGecko.Framework.UI
{
    /// <summary>
    /// 游戏状态UI控制器 - 管理游戏状态相关的UI显示和交互
    /// </summary>
    public class GameStateUI : MonoBehaviour
    {
        [Header("UI组件")]
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private GameObject loadingPanel;
        
        [Header("游戏信息显示")]
        [SerializeField] private Text gameTimeText;
        [SerializeField] private Text remainingTimeText;
        [SerializeField] private Text gameStateText;
        [SerializeField] private Text snakeCountText;

        private GameStateController _gameStateController;
        private SnakeManager _snakeManager;
        private GameObject _GameplayHUD;

        private void Start()
        {
            InitializeUI();
            SubscribeToEvents();
        }

        private void Update()
        {
            UpdateGameInfo();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
            if (_gameStateController != null)
            {
                _gameStateController.RestartGame();
            }
        }

        /// <summary>
        /// 初始化UI
        /// </summary>
        void InitializeUI()
        {
            // 查找游戏状态控制器
            _gameStateController = FindObjectOfType<GameStateController>();
            _snakeManager = FindObjectOfType<SnakeManager>();
            _GameplayHUD = UIManager.Instance.FindUI("GameplayHUD");

            // 注册组件
            if (_GameplayHUD != null)
            {
                // 查找CenterImage4Text组件
                var centerImage4TextTransform = _GameplayHUD.transform.Find("TopBar/MiddleGroup/CenterImages/CenterImage4/CenterImage4Text");
                if (centerImage4TextTransform != null)
                {
                    remainingTimeText = centerImage4TextTransform.GetComponent<Text>();
                }
                else
                {
                    Debug.LogWarning("GameStateUI: 在GameplayHUD中未找到CenterImage4Text子对象");
                }
            }
            else
            {
                Debug.LogWarning("GameStateUI: 未找到GameplayHUD UI");
            }

            // 设置按钮事件
            if (pauseButton != null)
                pauseButton.onClick.AddListener(OnPauseButtonClicked);
            
            if (resumeButton != null)
                resumeButton.onClick.AddListener(OnResumeButtonClicked);
            
            if (restartButton != null)
                restartButton.onClick.AddListener(OnRestartButtonClicked);

            // 初始状态
            UpdateUIForState(GameState.Playing);
        }

        /// <summary>
        /// 订阅事件
        /// </summary>
        void SubscribeToEvents()
        {
            GameStateController.OnGameStateChanged += OnGameStateChanged;
        }

        /// <summary>
        /// 取消订阅事件
        /// </summary>
        void UnsubscribeFromEvents()
        {
            GameStateController.OnGameStateChanged -= OnGameStateChanged;
        }

        /// <summary>
        /// 游戏状态变化事件处理
        /// </summary>
        void OnGameStateChanged(object sender, GameStateChangedEventArgs e)
        {
            UpdateUIForState(e.NewState);
        }

        /// <summary>
        /// 根据游戏状态更新UI
        /// </summary>
        void UpdateUIForState(GameState state)
        {
            // 隐藏所有面板
            //UIManager.Instance.CloseAll();

            // 根据状态显示对应面板
            switch (state)
            {
                case GameState.Initializing:
                    break;
                case GameState.Playing:
                    // 游戏中状态，显示暂停按钮
                    UIManager.Instance.Show("GameplayHUD", GameContext.PreloadedUIPrefab_GameMain);
                    break;
                case GameState.Paused:
                    UIManager.Instance.Show("GameSetting", GameContext.PreloadedUIPrefab_GameSetting);
                    break;
                case GameState.GameOver:
                    if(_gameStateController && _gameStateController.RemainingTime > 0)
                    {
                        UIManager.Instance.Show("GameSuccess", GameContext.PreloadedUIPrefab_GameSuccess);

                    }
                    else
                    {
                        UIManager.Instance.Show("GameFaild", GameContext.PreloadedUIPrefab_GameFaild);
                        if(_snakeManager != null)
                        {
                            _snakeManager.ClearAllSnakes();
                        }
                    }
                    break;
            }

            // 更新状态文本
            if (gameStateText != null)
            {
                gameStateText.text = GetStateDisplayText(state);
            }
        }

        /// <summary>
        /// 更新游戏信息显示
        /// </summary>
        void UpdateGameInfo()
        {
            if (_gameStateController == null) return;

            // 更新游戏时间
            if (gameTimeText != null)
            {
                float gameTime = _gameStateController.CurrentGameTime;
                gameTimeText.text = $"游戏时间: {FormatTime(gameTime)}";
            }

            // 更新剩余时间
            if (remainingTimeText != null)
            {
                float remainingTime = _gameStateController.RemainingTime;
                remainingTimeText.text = $"{FormatTime(remainingTime)}";
            }

            // 更新蛇的数量
            if (snakeCountText != null && _snakeManager != null)
            {
                var aliveSnakes = _snakeManager.GetAliveSnakes();
                var totalSnakes = _snakeManager.GetAllSnakes();
                snakeCountText.text = $"蛇数量: {aliveSnakes.Count}/{totalSnakes.Count}";
            }
        }

        /// <summary>
        /// 格式化时间显示
        /// </summary>
        string FormatTime(float timeInSeconds)
        {
            int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
            int seconds = Mathf.FloorToInt(timeInSeconds % 60f);
            return $"{minutes:00}:{seconds:00}";
        }

        /// <summary>
        /// 获取状态显示文本
        /// </summary>
        string GetStateDisplayText(GameState state)
        {
            switch (state)
            {
                case GameState.Initializing:
                    return "初始化中...";
                case GameState.Playing:
                    return "游戏中";
                case GameState.Paused:
                    return "已暂停";
                case GameState.GameOver:
                    return "游戏结束";
                default:
                    return "未知状态";
            }
        }

        #region 按钮事件处理

        void OnPauseButtonClicked()
        {
            if (_gameStateController != null)
            {
                _gameStateController.PauseGame();
            }
        }

        void OnResumeButtonClicked()
        {
            if (_gameStateController != null)
            {
                _gameStateController.ResumeGame();
            }
        }

        void OnRestartButtonClicked()
        {
            if (_gameStateController != null)
            {
                _gameStateController.RestartGame();
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 设置游戏状态控制器引用
        /// </summary>
        public void SetGameStateController(GameStateController controller)
        {
            _gameStateController = controller;
        }

        /// <summary>
        /// 设置蛇管理器引用
        /// </summary>
        public void SetSnakeManager(SnakeManager manager)
        {
            _snakeManager = manager;
        }

        #endregion
    }
}