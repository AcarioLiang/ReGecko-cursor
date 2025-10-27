using UnityEngine;

[CreateAssetMenu(fileName = "GameSettings", menuName = "Gecko Out/Game Settings")]
public class GameSettings : ScriptableObject
{
    [Header("Player Settings")]
    public float playerFollowSpeed = 15f;
    public float playerTailSpacing = 0.3f;
    public int playerMaxHealth = 3;
    public float playerInvincibilityTime = 1f;
    
    [Header("Level Settings")]
    public float levelSpawnInterval = 2f;
    public float levelSpawnDistance = 15f;
    public float levelLength = 100f;
    public float difficultyIncreaseRate = 0.1f;
    
    [Header("Obstacle Settings")]
    public float obstacleSpawnChance = 0.7f;
    public float obstacleSpeed = 5f;
    public float obstacleMoveSpeed = 2f;
    public float obstacleMoveDistance = 5f;
    
    [Header("Collectible Settings")]
    public float collectibleSpawnChance = 0.8f;
    public float collectibleSpeed = 3f;
    public float collectibleRotationSpeed = 90f;
    public float collectibleBobSpeed = 2f;
    public float collectibleBobHeight = 0.2f;
    
    [Header("Power-up Settings")]
    public float powerUpSpawnChance = 0.2f;
    public float powerUpSpeed = 4f;
    public float powerUpDuration = 10f;
    
    [Header("Camera Settings")]
    public float cameraFollowSpeed = 5f;
    public bool cameraSmoothFollow = true;
    public float cameraShakeIntensity = 0.1f;
    public float cameraShakeDecay = 0.95f;
    
    [Header("Audio Settings")]
    public float masterVolume = 1f;
    public float musicVolume = 0.8f;
    public float sfxVolume = 1f;
    public bool vibrationEnabled = true;
    
    [Header("Visual Settings")]
    public bool particleEffectsEnabled = true;
    public bool screenShakeEnabled = true;
    public bool trailEffectsEnabled = true;
    public int maxParticles = 100;
    
    [Header("Performance Settings")]
    public bool objectPoolingEnabled = true;
    public int defaultPoolSize = 20;
    public bool dynamicDifficultyEnabled = true;
    public float targetFrameRate = 60f;
    
    // Singleton instance
    private static GameSettings _instance;
    public static GameSettings Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<GameSettings>("GameSettings");
                if (_instance == null)
                {
                    Debug.LogWarning("GameSettings not found in Resources folder. Creating default settings.");
                    _instance = CreateInstance<GameSettings>();
                }
            }
            return _instance;
        }
    }
    
    void OnEnable()
    {
        LoadSettings();
    }
    
    void OnDisable()
    {
        SaveSettings();
    }
    
    public void LoadSettings()
    {
        // Load settings from PlayerPrefs
        playerFollowSpeed = PlayerPrefs.GetFloat("PlayerFollowSpeed", playerFollowSpeed);
        playerTailSpacing = PlayerPrefs.GetFloat("PlayerTailSpacing", playerTailSpacing);
        playerMaxHealth = PlayerPrefs.GetInt("PlayerMaxHealth", playerMaxHealth);
        playerInvincibilityTime = PlayerPrefs.GetFloat("PlayerInvincibilityTime", playerInvincibilityTime);
        
        levelSpawnInterval = PlayerPrefs.GetFloat("LevelSpawnInterval", levelSpawnInterval);
        levelSpawnDistance = PlayerPrefs.GetFloat("LevelSpawnDistance", levelSpawnDistance);
        levelLength = PlayerPrefs.GetFloat("LevelLength", levelLength);
        difficultyIncreaseRate = PlayerPrefs.GetFloat("DifficultyIncreaseRate", difficultyIncreaseRate);
        
        obstacleSpawnChance = PlayerPrefs.GetFloat("ObstacleSpawnChance", obstacleSpawnChance);
        obstacleSpeed = PlayerPrefs.GetFloat("ObstacleSpeed", obstacleSpeed);
        obstacleMoveSpeed = PlayerPrefs.GetFloat("ObstacleMoveSpeed", obstacleMoveSpeed);
        obstacleMoveDistance = PlayerPrefs.GetFloat("ObstacleMoveDistance", obstacleMoveDistance);
        
        collectibleSpawnChance = PlayerPrefs.GetFloat("CollectibleSpawnChance", collectibleSpawnChance);
        collectibleSpeed = PlayerPrefs.GetFloat("CollectibleSpeed", collectibleSpeed);
        collectibleRotationSpeed = PlayerPrefs.GetFloat("CollectibleRotationSpeed", collectibleRotationSpeed);
        collectibleBobSpeed = PlayerPrefs.GetFloat("CollectibleBobSpeed", collectibleBobSpeed);
        collectibleBobHeight = PlayerPrefs.GetFloat("CollectibleBobHeight", collectibleBobHeight);
        
        powerUpSpawnChance = PlayerPrefs.GetFloat("PowerUpSpawnChance", powerUpSpawnChance);
        powerUpSpeed = PlayerPrefs.GetFloat("PowerUpSpeed", powerUpSpeed);
        powerUpDuration = PlayerPrefs.GetFloat("PowerUpDuration", powerUpDuration);
        
        cameraFollowSpeed = PlayerPrefs.GetFloat("CameraFollowSpeed", cameraFollowSpeed);
        cameraSmoothFollow = PlayerPrefs.GetInt("CameraSmoothFollow", cameraSmoothFollow ? 1 : 0) == 1;
        cameraShakeIntensity = PlayerPrefs.GetFloat("CameraShakeIntensity", cameraShakeIntensity);
        cameraShakeDecay = PlayerPrefs.GetFloat("CameraShakeDecay", cameraShakeDecay);
        
        masterVolume = PlayerPrefs.GetFloat("MasterVolume", masterVolume);
        musicVolume = PlayerPrefs.GetFloat("MusicVolume", musicVolume);
        sfxVolume = PlayerPrefs.GetFloat("SFXVolume", sfxVolume);
        vibrationEnabled = PlayerPrefs.GetInt("VibrationEnabled", vibrationEnabled ? 1 : 0) == 1;
        
        particleEffectsEnabled = PlayerPrefs.GetInt("ParticleEffectsEnabled", particleEffectsEnabled ? 1 : 0) == 1;
        screenShakeEnabled = PlayerPrefs.GetInt("ScreenShakeEnabled", screenShakeEnabled ? 1 : 0) == 1;
        trailEffectsEnabled = PlayerPrefs.GetInt("TrailEffectsEnabled", trailEffectsEnabled ? 1 : 0) == 1;
        maxParticles = PlayerPrefs.GetInt("MaxParticles", maxParticles);
        
        objectPoolingEnabled = PlayerPrefs.GetInt("ObjectPoolingEnabled", objectPoolingEnabled ? 1 : 0) == 1;
        defaultPoolSize = PlayerPrefs.GetInt("DefaultPoolSize", defaultPoolSize);
        dynamicDifficultyEnabled = PlayerPrefs.GetInt("DynamicDifficultyEnabled", dynamicDifficultyEnabled ? 1 : 0) == 1;
        targetFrameRate = PlayerPrefs.GetFloat("TargetFrameRate", targetFrameRate);
    }
    
    public void SaveSettings()
    {
        // Save settings to PlayerPrefs
        PlayerPrefs.SetFloat("PlayerFollowSpeed", playerFollowSpeed);
        PlayerPrefs.SetFloat("PlayerTailSpacing", playerTailSpacing);
        PlayerPrefs.SetInt("PlayerMaxHealth", playerMaxHealth);
        PlayerPrefs.SetFloat("PlayerInvincibilityTime", playerInvincibilityTime);
        
        PlayerPrefs.SetFloat("LevelSpawnInterval", levelSpawnInterval);
        PlayerPrefs.SetFloat("LevelSpawnDistance", levelSpawnDistance);
        PlayerPrefs.SetFloat("LevelLength", levelLength);
        PlayerPrefs.SetFloat("DifficultyIncreaseRate", difficultyIncreaseRate);
        
        PlayerPrefs.SetFloat("ObstacleSpawnChance", obstacleSpawnChance);
        PlayerPrefs.SetFloat("ObstacleSpeed", obstacleSpeed);
        PlayerPrefs.SetFloat("ObstacleMoveSpeed", obstacleMoveSpeed);
        PlayerPrefs.SetFloat("ObstacleMoveDistance", obstacleMoveDistance);
        
        PlayerPrefs.SetFloat("CollectibleSpawnChance", collectibleSpawnChance);
        PlayerPrefs.SetFloat("CollectibleSpeed", collectibleSpeed);
        PlayerPrefs.SetFloat("CollectibleRotationSpeed", collectibleRotationSpeed);
        PlayerPrefs.SetFloat("CollectibleBobSpeed", collectibleBobSpeed);
        PlayerPrefs.SetFloat("CollectibleBobHeight", collectibleBobHeight);
        
        PlayerPrefs.SetFloat("PowerUpSpawnChance", powerUpSpawnChance);
        PlayerPrefs.SetFloat("PowerUpSpeed", powerUpSpeed);
        PlayerPrefs.SetFloat("PowerUpDuration", powerUpDuration);
        
        PlayerPrefs.SetFloat("CameraFollowSpeed", cameraFollowSpeed);
        PlayerPrefs.SetInt("CameraSmoothFollow", cameraSmoothFollow ? 1 : 0);
        PlayerPrefs.SetFloat("CameraShakeIntensity", cameraShakeIntensity);
        PlayerPrefs.SetFloat("CameraShakeDecay", cameraShakeDecay);
        
        PlayerPrefs.SetFloat("MasterVolume", masterVolume);
        PlayerPrefs.SetFloat("MusicVolume", musicVolume);
        PlayerPrefs.SetFloat("SFXVolume", sfxVolume);
        PlayerPrefs.SetInt("VibrationEnabled", vibrationEnabled ? 1 : 0);
        
        PlayerPrefs.SetInt("ParticleEffectsEnabled", particleEffectsEnabled ? 1 : 0);
        PlayerPrefs.SetInt("ScreenShakeEnabled", screenShakeEnabled ? 1 : 0);
        PlayerPrefs.SetInt("TrailEffectsEnabled", trailEffectsEnabled ? 1 : 0);
        PlayerPrefs.SetInt("MaxParticles", maxParticles);
        
        PlayerPrefs.SetInt("ObjectPoolingEnabled", objectPoolingEnabled ? 1 : 0);
        PlayerPrefs.SetInt("DefaultPoolSize", defaultPoolSize);
        PlayerPrefs.SetInt("DynamicDifficultyEnabled", dynamicDifficultyEnabled ? 1 : 0);
        PlayerPrefs.SetFloat("TargetFrameRate", targetFrameRate);
        
        PlayerPrefs.Save();
    }
    
    public void ResetToDefaults()
    {
        // Reset to default values
        playerFollowSpeed = 15f;
        playerTailSpacing = 0.3f;
        playerMaxHealth = 3;
        playerInvincibilityTime = 1f;
        
        levelSpawnInterval = 2f;
        levelSpawnDistance = 15f;
        levelLength = 100f;
        difficultyIncreaseRate = 0.1f;
        
        obstacleSpawnChance = 0.7f;
        obstacleSpeed = 5f;
        obstacleMoveSpeed = 2f;
        obstacleMoveDistance = 5f;
        
        collectibleSpawnChance = 0.8f;
        collectibleSpeed = 3f;
        collectibleRotationSpeed = 90f;
        collectibleBobSpeed = 2f;
        collectibleBobHeight = 0.2f;
        
        powerUpSpawnChance = 0.2f;
        powerUpSpeed = 4f;
        powerUpDuration = 10f;
        
        cameraFollowSpeed = 5f;
        cameraSmoothFollow = true;
        cameraShakeIntensity = 0.1f;
        cameraShakeDecay = 0.95f;
        
        masterVolume = 1f;
        musicVolume = 0.8f;
        sfxVolume = 1f;
        vibrationEnabled = true;
        
        particleEffectsEnabled = true;
        screenShakeEnabled = true;
        trailEffectsEnabled = true;
        maxParticles = 100;
        
        objectPoolingEnabled = true;
        defaultPoolSize = 20;
        dynamicDifficultyEnabled = true;
        targetFrameRate = 60f;
        
        SaveSettings();
    }
    
    // Helper methods for common operations
    public float GetAdjustedVolume(float baseVolume)
    {
        return baseVolume * masterVolume;
    }
    
    public float GetMusicVolume()
    {
        return musicVolume * masterVolume;
    }
    
    public float GetSFXVolume()
    {
        return sfxVolume * masterVolume;
    }
    
    public bool ShouldShowParticles()
    {
        return particleEffectsEnabled;
    }
    
    public bool ShouldShakeScreen()
    {
        return screenShakeEnabled;
    }
    
    public bool ShouldShowTrails()
    {
        return trailEffectsEnabled;
    }
}