using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject mainMenuPanel;
    public GameObject gamePanel;
    public GameObject pausePanel;
    public GameObject gameOverPanel;
    public GameObject levelCompletePanel;
    
    [Header("Main Menu")]
    public Button startGameButton;
    public Button settingsButton;
    public Button quitButton;
    
    [Header("Game UI")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI timeText;
    public Slider healthSlider;
    public Image[] healthIcons;
    public Button pauseButton;
    
    [Header("Pause Menu")]
    public Button resumeButton;
    public Button restartButton;
    public Button mainMenuButton;
    
    [Header("Game Over")]
    public TextMeshProUGUI finalScoreText;
    public TextMeshProUGUI highScoreText;
    public Button retryButton;
    public Button mainMenuFromGameOverButton;
    
    [Header("Level Complete")]
    public TextMeshProUGUI levelScoreText;
    public TextMeshProUGUI levelHighScoreText;
    public Button nextLevelButton;
    public Button mainMenuFromLevelButton;
    
    [Header("Settings")]
    public GameObject settingsPanel;
    public Slider musicSlider;
    public Slider sfxSlider;
    public Toggle vibrationToggle;
    
    private GameManager gameManager;
    
    void Start()
    {
        gameManager = GameManager.Instance;
        if (gameManager != null)
        {
            gameManager.OnGameStateChanged += OnGameStateChanged;
            gameManager.OnScoreChanged += UpdateScore;
            gameManager.OnTimeChanged += UpdateTime;
        }
        
        SetupButtonListeners();
        ShowMainMenu();
    }
    
    void SetupButtonListeners()
    {
        // Main menu buttons
        if (startGameButton != null)
            startGameButton.onClick.AddListener(OnStartGameClicked);
        if (settingsButton != null)
            settingsButton.onClick.AddListener(OnSettingsClicked);
        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitClicked);
        
        // Game UI buttons
        if (pauseButton != null)
            pauseButton.onClick.AddListener(OnPauseClicked);
        
        // Pause menu buttons
        if (resumeButton != null)
            resumeButton.onClick.AddListener(OnResumeClicked);
        if (restartButton != null)
            restartButton.onClick.AddListener(OnRestartClicked);
        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(OnMainMenuClicked);
        
        // Game over buttons
        if (retryButton != null)
            retryButton.onClick.AddListener(OnRetryClicked);
        if (mainMenuFromGameOverButton != null)
            mainMenuFromGameOverButton.onClick.AddListener(OnMainMenuClicked);
        
        // Level complete buttons
        if (nextLevelButton != null)
            nextLevelButton.onClick.AddListener(OnNextLevelClicked);
        if (mainMenuFromLevelButton != null)
            mainMenuFromLevelButton.onClick.AddListener(OnMainMenuClicked);
        
        // Settings
        if (musicSlider != null)
            musicSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        if (sfxSlider != null)
            sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        if (vibrationToggle != null)
            vibrationToggle.onValueChanged.AddListener(OnVibrationToggled);
    }
    
    void OnGameStateChanged(GameManager.GameState newState)
    {
        switch (newState)
        {
            case GameManager.GameState.MainMenu:
                ShowMainMenu();
                break;
            case GameManager.GameState.Playing:
                ShowGameUI();
                break;
            case GameManager.GameState.Paused:
                ShowPauseMenu();
                break;
            case GameManager.GameState.GameOver:
                ShowGameOver();
                break;
            case GameManager.GameState.LevelComplete:
                ShowLevelComplete();
                break;
        }
    }
    
    void ShowMainMenu()
    {
        SetAllPanelsInactive();
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);
    }
    
    void ShowGameUI()
    {
        SetAllPanelsInactive();
        if (gamePanel != null)
            gamePanel.SetActive(true);
    }
    
    void ShowPauseMenu()
    {
        if (pausePanel != null)
            pausePanel.SetActive(true);
    }
    
    void ShowGameOver()
    {
        SetAllPanelsInactive();
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            UpdateGameOverUI();
        }
    }
    
    void ShowLevelComplete()
    {
        SetAllPanelsInactive();
        if (levelCompletePanel != null)
        {
            levelCompletePanel.SetActive(true);
            UpdateLevelCompleteUI();
        }
    }
    
    void SetAllPanelsInactive()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (gamePanel != null) gamePanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (levelCompletePanel != null) levelCompletePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }
    
    void UpdateScore(int score)
    {
        if (scoreText != null)
            scoreText.text = "Score: " + score.ToString();
    }
    
    void UpdateTime(float time)
    {
        if (timeText != null)
        {
            int minutes = Mathf.FloorToInt(time / 60f);
            int seconds = Mathf.FloorToInt(time % 60f);
            timeText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }
    }
    
    public void UpdateHealth(int health)
    {
        // Update health slider
        if (healthSlider != null)
        {
            healthSlider.value = (float)health / 3f; // Assuming max health is 3
        }
        
        // Update health icons
        if (healthIcons != null)
        {
            for (int i = 0; i < healthIcons.Length; i++)
            {
                if (healthIcons[i] != null)
                {
                    healthIcons[i].enabled = i < health;
                }
            }
        }
    }
    
    void UpdateGameOverUI()
    {
        if (gameManager != null)
        {
            if (finalScoreText != null)
                finalScoreText.text = "Final Score: " + gameManager.GetCurrentScore().ToString();
            if (highScoreText != null)
                highScoreText.text = "High Score: " + gameManager.GetHighScore().ToString();
        }
    }
    
    void UpdateLevelCompleteUI()
    {
        if (gameManager != null)
        {
            if (levelScoreText != null)
                levelScoreText.text = "Level Score: " + gameManager.GetCurrentScore().ToString();
            if (levelHighScoreText != null)
                levelHighScoreText.text = "High Score: " + gameManager.GetHighScore().ToString();
        }
    }
    
    // Button event handlers
    void OnStartGameClicked()
    {
        if (gameManager != null)
            gameManager.StartGame();
    }
    
    void OnSettingsClicked()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(true);
    }
    
    void OnQuitClicked()
    {
        if (gameManager != null)
            gameManager.QuitGame();
    }
    
    void OnPauseClicked()
    {
        if (gameManager != null)
            gameManager.PauseGame();
    }
    
    void OnResumeClicked()
    {
        if (gameManager != null)
            gameManager.ResumeGame();
    }
    
    void OnRestartClicked()
    {
        if (gameManager != null)
            gameManager.RestartGame();
    }
    
    void OnMainMenuClicked()
    {
        if (gameManager != null)
            gameManager.ReturnToMainMenu();
    }
    
    void OnRetryClicked()
    {
        if (gameManager != null)
            gameManager.RestartGame();
    }
    
    void OnNextLevelClicked()
    {
        // TODO: Implement next level logic
        if (gameManager != null)
            gameManager.RestartGame();
    }
    
    // Settings handlers
    void OnMusicVolumeChanged(float value)
    {
        // TODO: Implement music volume control
        PlayerPrefs.SetFloat("MusicVolume", value);
        PlayerPrefs.Save();
    }
    
    void OnSFXVolumeChanged(float value)
    {
        // TODO: Implement SFX volume control
        PlayerPrefs.SetFloat("SFXVolume", value);
        PlayerPrefs.Save();
    }
    
    void OnVibrationToggled(bool enabled)
    {
        // TODO: Implement vibration control
        PlayerPrefs.SetInt("VibrationEnabled", enabled ? 1 : 0);
        PlayerPrefs.Save();
    }
    
    void OnDestroy()
    {
        if (gameManager != null)
        {
            gameManager.OnGameStateChanged -= OnGameStateChanged;
            gameManager.OnScoreChanged -= UpdateScore;
            gameManager.OnTimeChanged -= UpdateTime;
        }
    }
}