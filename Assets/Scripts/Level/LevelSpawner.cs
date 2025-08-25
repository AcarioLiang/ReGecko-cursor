using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public float spawnInterval = 2f;
    public float spawnDistance = 15f;
    public float difficultyIncreaseRate = 0.1f;
    
    [Header("Obstacles")]
    public GameObject[] obstaclePrefabs;
    public float obstacleSpawnChance = 0.7f;
    public float obstacleSpeed = 5f;
    
    [Header("Collectibles")]
    public GameObject[] collectiblePrefabs;
    public float collectibleSpawnChance = 0.8f;
    public float collectibleSpeed = 3f;
    
    [Header("Power-ups")]
    public GameObject[] powerUpPrefabs;
    public float powerUpSpawnChance = 0.2f;
    public float powerUpSpeed = 4f;
    
    [Header("Level Progression")]
    public float levelLength = 100f;
    public AnimationCurve difficultyCurve;
    
    // Private variables
    private float currentDistance = 0f;
    private float nextSpawnTime = 0f;
    private float currentDifficulty = 1f;
    private Transform player;
    private List<GameObject> spawnedObjects = new List<GameObject>();
    private Camera mainCamera;
    
    // Events
    public System.Action<float> OnLevelProgress;
    public System.Action OnLevelComplete;
    
    void Start()
    {
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        mainCamera = Camera.main;
        if (playerObj != null)
        {
            player = playerObj.transform;
        }

        if (player == null)
        {
            Debug.LogError("Player not found! Make sure player has 'Player' tag.");
            return;
        }
        
        // Initialize difficulty curve if not set
        if (difficultyCurve.length == 0)
        {
            difficultyCurve = AnimationCurve.Linear(0f, 1f, 1f, 3f);
        }
        
        StartCoroutine(SpawnRoutine());
    }
    
    void Update()
    {
        UpdateLevelProgress();
        CleanupOffscreenObjects();
    }
    
    void UpdateLevelProgress()
    {
        if (player != null)
        {
            currentDistance = player.position.x;
            float progress = currentDistance / levelLength;
            OnLevelProgress?.Invoke(progress);
            
            // Update difficulty based on progress
            currentDifficulty = difficultyCurve.Evaluate(progress);
            
            if (progress >= 1f)
            {
                OnLevelComplete?.Invoke();
                enabled = false;
            }
        }
    }
    
    IEnumerator SpawnRoutine()
    {
        while (enabled)
        {
            yield return new WaitForSeconds(spawnInterval);
            
            if (player != null && Time.time >= nextSpawnTime)
            {
                SpawnLevelElements();
                nextSpawnTime = Time.time + spawnInterval / currentDifficulty;
            }
        }
    }
    
    void SpawnLevelElements()
    {
        Vector3 spawnPosition = GetSpawnPosition();
        
        // Spawn obstacles
        if (Random.value < obstacleSpawnChance * currentDifficulty)
        {
            SpawnObstacle(spawnPosition);
        }
        
        // Spawn collectibles
        if (Random.value < collectibleSpawnChance)
        {
            SpawnCollectible(spawnPosition);
        }
        
        // Spawn power-ups (rarer)
        if (Random.value < powerUpSpawnChance)
        {
            SpawnPowerUp(spawnPosition);
        }
    }
    
    Vector3 GetSpawnPosition()
    {
        float x = player.position.x + spawnDistance;
        float y = Random.Range(-4f, 4f);
        return new Vector3(x, y, 0f);
    }
    
    void SpawnObstacle(Vector3 position)
    {
        if (obstaclePrefabs.Length == 0) return;
        
        GameObject prefab = obstaclePrefabs[Random.Range(0, obstaclePrefabs.Length)];
        GameObject obstacle = Instantiate(prefab, position, Quaternion.identity);
        
        // Add movement component if it doesn't have one
        ObstacleMovement movement = obstacle.GetComponent<ObstacleMovement>();
        if (movement == null)
        {
            movement = obstacle.AddComponent<ObstacleMovement>();
        }
        movement.speed = obstacleSpeed * currentDifficulty;
        
        spawnedObjects.Add(obstacle);
    }
    
    void SpawnCollectible(Vector3 position)
    {
        if (collectiblePrefabs.Length == 0) return;
        
        GameObject prefab = collectiblePrefabs[Random.Range(0, collectiblePrefabs.Length)];
        GameObject collectible = Instantiate(prefab, position, Quaternion.identity);
        
        // Add movement component
        ObstacleMovement movement = collectible.GetComponent<ObstacleMovement>();
        if (movement == null)
        {
            movement = collectible.AddComponent<ObstacleMovement>();
        }
        movement.speed = collectibleSpeed;
        
        spawnedObjects.Add(collectible);
    }
    
    void SpawnPowerUp(Vector3 position)
    {
        if (powerUpPrefabs.Length == 0) return;
        
        GameObject prefab = powerUpPrefabs[Random.Range(0, powerUpPrefabs.Length)];
        GameObject powerUp = Instantiate(prefab, position, Quaternion.identity);
        
        // Add movement component
        ObstacleMovement movement = powerUp.GetComponent<ObstacleMovement>();
        if (movement == null)
        {
            movement = powerUp.AddComponent<ObstacleMovement>();
        }
        movement.speed = powerUpSpeed;
        
        spawnedObjects.Add(powerUp);
    }
    
    void CleanupOffscreenObjects()
    {
        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
        {
            if (spawnedObjects[i] == null)
            {
                spawnedObjects.RemoveAt(i);
                continue;
            }
            
            // Remove objects that are far behind the player
            if (spawnedObjects[i].transform.position.x < player.position.x - 20f)
            {
                Destroy(spawnedObjects[i]);
                spawnedObjects.RemoveAt(i);
            }
        }
    }
    
    public void ClearAllObjects()
    {
        foreach (GameObject obj in spawnedObjects)
        {
            if (obj != null)
                Destroy(obj);
        }
        spawnedObjects.Clear();
    }
    
    public void SetDifficulty(float difficulty)
    {
        currentDifficulty = Mathf.Clamp(difficulty, 0.5f, 5f);
    }
    
    void OnDrawGizmosSelected()
    {
        if (player != null)
        {
            Gizmos.color = Color.red;
            Vector3 spawnPos = GetSpawnPosition();
            Gizmos.DrawWireSphere(spawnPos, 0.5f);
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(player.position, spawnPos);
        }
    }
}

// Simple movement component for spawned objects
public class ObstacleMovement : MonoBehaviour
{
    public float speed = 5f;
    public bool destroyOnScreenExit = true;
    
    void Update()
    {
        transform.Translate(Vector3.left * speed * Time.deltaTime);
        
        // Destroy when off screen
        if (destroyOnScreenExit && transform.position.x < -20f)
        {
            Destroy(gameObject);
        }
    }
}