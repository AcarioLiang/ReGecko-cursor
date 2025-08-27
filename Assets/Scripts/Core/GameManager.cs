using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [Header("Game Settings")]
    public float gameTime = 60f;
    public bool isEndlessMode = true;
    
    [Header("UI References")]
    public UIManager uiManager;
    public LevelSpawner levelSpawner;
    
    // Game state
    public enum GameState
    {
        MainMenu,
        Playing,
        Paused,
        GameOver,
        LevelComplete
    }
    
    private GameState currentState = GameState.MainMenu;
    private float currentGameTime;
    private int currentScore = 0;
    private int highScore = 0;
    private bool isGameActive = false;
    
    // Events
    public System.Action<GameState> OnGameStateChanged;
    public System.Action<int> OnScoreChanged;
    public System.Action<float> OnTimeChanged;
    
    // Singleton pattern
    public static GameManager Instance { get; private set; }
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        LoadHighScore();
        SetGameState(GameState.MainMenu);
    }
    
    void Update()
    {
        if (currentState == GameState.Playing)
        {
            UpdateGameTime();
            HandleInput();
        }
    }
    
    void HandleInput()
    {
        // Pause game with Escape key
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            PauseGame();
        }
    }
    
    void UpdateGameTime()
    {
        if (isEndlessMode) return;
        
        currentGameTime -= Time.deltaTime;
        OnTimeChanged?.Invoke(currentGameTime);
        
        if (currentGameTime <= 0)
        {
            GameOver();
        }
    }
    
    public void StartGame()
    {
        currentScore = 0;
        currentGameTime = gameTime;
        isGameActive = true;
        
        SetGameState(GameState.Playing);
        
        // Start level spawning
        if (levelSpawner != null)
        {
            levelSpawner.enabled = true;
        }
        
        // Subscribe to player events
        GeckoController player = FindObjectOfType<GeckoController>();
        if (player != null)
        {
            player.OnScoreChanged += AddScore;
            player.OnHealthChanged += OnPlayerHealthChanged;
            player.OnGameOver += GameOver;
        }
        
        // Subscribe to level events
        if (levelSpawner != null)
        {
            levelSpawner.OnLevelComplete += LevelComplete;
        }
        
        OnScoreChanged?.Invoke(currentScore);
        OnTimeChanged?.Invoke(currentGameTime);
    }
    
    public void PauseGame()
    {
        if (currentState == GameState.Playing)
        {
            SetGameState(GameState.Paused);
            Time.timeScale = 0f;
        }
    }
    
    public void ResumeGame()
    {
        if (currentState == GameState.Paused)
        {
            SetGameState(GameState.Playing);
            Time.timeScale = 1f;
        }
    }
    
    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    
    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SetGameState(GameState.MainMenu);
        
        // Clean up current game
        if (levelSpawner != null)
        {
            levelSpawner.ClearAllObjects();
            levelSpawner.enabled = false;
        }
        
        // Unsubscribe from events
        GeckoController player = FindObjectOfType<GeckoController>();
        if (player != null)
        {
            player.OnScoreChanged -= AddScore;
            player.OnHealthChanged -= OnPlayerHealthChanged;
            player.OnGameOver -= GameOver;
        }
        
        if (levelSpawner != null)
        {
            levelSpawner.OnLevelComplete -= LevelComplete;
        }
    }
    
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    
    void GameOver()
    {
        isGameActive = false;
        SetGameState(GameState.GameOver);
        
        // Check for new high score
        if (currentScore > highScore)
        {
            highScore = currentScore;
            SaveHighScore();
        }
        
        // Stop level spawning
        if (levelSpawner != null)
        {
            levelSpawner.enabled = false;
        }
        
        // Show game over UI
        if (uiManager != null)
        {
            uiManager.ShowGameOver(currentScore, highScore);
        }
    }
    
    void LevelComplete()
    {
        isGameActive = false;
        SetGameState(GameState.LevelComplete);
        
        // Check for new high score
        if (currentScore > highScore)
        {
            highScore = currentScore;
            SaveHighScore();
        }
        
        // Show level complete UI
        if (uiManager != null)
        {
            uiManager.ShowLevelComplete(currentScore, highScore);
        }
    }
    
    public void AddScore(int points)
    {
        currentScore += points;
        OnScoreChanged?.Invoke(currentScore);
        
        // Update high score if needed
        if (currentScore > highScore)
        {
            highScore = currentScore;
            SaveHighScore();
        }
    }
    
    void OnPlayerHealthChanged(int health)
    {
        if (uiManager != null)
        {
            uiManager.UpdateHealth(health);
        }
    }
    
    void SetGameState(GameState newState)
    {
        currentState = newState;
        OnGameStateChanged?.Invoke(newState);
        
        // Update UI based on state
        if (uiManager != null)
        {
            uiManager.OnGameStateChanged(newState);
        }
    }
    
    void LoadHighScore()
    {
        highScore = PlayerPrefs.GetInt("HighScore", 0);
    }
    
    void SaveHighScore()
    {
        PlayerPrefs.SetInt("HighScore", highScore);
        PlayerPrefs.Save();
    }
    
    // Public getters
    public GameState GetCurrentState() => currentState;
    public int GetCurrentScore() => currentScore;
    public int GetHighScore() => highScore;
    public float GetCurrentTime() => currentGameTime;
    public bool IsGameActive() => isGameActive;
    
    void OnDestroy()
    {
        // Clean up event subscriptions
        GeckoController player = FindObjectOfType<GeckoController>();
        if (player != null)
        {
            player.OnScoreChanged -= AddScore;
            player.OnHealthChanged -= OnPlayerHealthChanged;
            player.OnGameOver -= GameOver;
        }
        
        if (levelSpawner != null)
        {
            levelSpawner.OnLevelComplete -= LevelComplete;
        }
    }
}