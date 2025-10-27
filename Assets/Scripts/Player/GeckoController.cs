using System.Collections.Generic;
using UnityEngine;

public class GeckoController : MonoBehaviour
{
    [Header("Movement")]
    public float followSpeed = 15f;
    public float maxDistanceFromTouch = 8f;
    public Rect worldBounds = new Rect(-10f, -6f, 20f, 12f);

    [Header("Tail")]
    public Transform tailPrefab;
    public int tailCount = 10;
    public float tailSpacing = 0.3f;
    public float tailFollowSpeed = 12f;

    [Header("Gameplay")]
    public int maxHealth = 3;
    public float invincibilityTime = 1f;
    public LayerMask obstacleLayerMask = 1;
    public LayerMask collectibleLayerMask = 1;

    [Header("Visual Effects")]
    public TrailRenderer trailRenderer;
    public ParticleSystem collectEffect;
    public ParticleSystem hitEffect;

    // Private variables
    private List<Vector3> path = new List<Vector3>();
    private List<Transform> tails = new List<Transform>();
    private Camera cam;
    private bool dragging;
    private Vector3 offset;
    private int currentHealth;
    private bool isInvincible;
    private float invincibilityTimer;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;

    // Events
    public System.Action<int> OnHealthChanged;
    public System.Action<int> OnScoreChanged;
    public System.Action OnGameOver;

    private int currentScore = 0;

    void Awake()
    {
        cam = Camera.main;
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody2D>();
        
        rb.gravityScale = 0f;
        rb.drag = 0.5f;
        
        InitializeTail();
        currentHealth = maxHealth;
    }

    void Start()
    {
        // Initialize path with current position
        for (int i = 0; i < tailCount * 5; i++)
            path.Add(transform.position);
    }

    void Update()
    {
        HandleInput();
        UpdateTail();
        UpdateInvincibility();
        UpdateVisualEffects();
    }

    void HandleInput()
    {
#if UNITY_EDITOR
        HandleMouse();
#else
        HandleTouch();
#endif
    }

    void HandleTouch()
    {
        if (Input.touchCount == 0)
        {
            dragging = false;
            return;
        }

        Touch t = Input.GetTouch(0);
        Vector3 worldPos = cam.ScreenToWorldPoint(new Vector3(t.position.x, t.position.y, cam.nearClipPlane));
        worldPos.z = 0;

        if (t.phase == TouchPhase.Began)
        {
            offset = transform.position - worldPos;
            dragging = true;
        }

        if (dragging)
        {
            Vector3 target = worldPos + offset;
            target = ClampToBounds(target);
            
            // Smooth movement using Lerp
            transform.position = Vector3.Lerp(transform.position, target, followSpeed * Time.deltaTime);
            
            // Update path for tail following
            if (Vector3.Distance(path[0], transform.position) > tailSpacing)
            {
                path.Insert(0, transform.position);
                if (path.Count > tailCount * 5)
                    path.RemoveAt(path.Count - 1);
            }
        }
    }

    void HandleMouse()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 worldPos = cam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, cam.nearClipPlane));
            worldPos.z = 0;
            offset = transform.position - worldPos;
            dragging = true;
        }

        if (Input.GetMouseButton(0) && dragging)
        {
            Vector3 worldPos = cam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, cam.nearClipPlane));
            worldPos.z = 0;
            Vector3 target = worldPos + offset;
            target = ClampToBounds(target);
            
            transform.position = Vector3.Lerp(transform.position, target, followSpeed * Time.deltaTime);
            
            if (Vector3.Distance(path[0], transform.position) > tailSpacing)
            {
                path.Insert(0, transform.position);
                if (path.Count > tailCount * 5)
                    path.RemoveAt(path.Count - 1);
            }
        }
        else
        {
            dragging = false;
        }
    }

    Vector3 ClampToBounds(Vector3 pos)
    {
        pos.x = Mathf.Clamp(pos.x, worldBounds.xMin, worldBounds.xMax);
        pos.y = Mathf.Clamp(pos.y, worldBounds.yMin, worldBounds.yMax);
        return pos;
    }

    void InitializeTail()
    {
        if (tailPrefab == null) return;

        for (int i = 0; i < tailCount; i++)
        {
            Transform tail = Instantiate(tailPrefab, transform.position, Quaternion.identity);
            tail.name = "Tail_" + i;
            tails.Add(tail);
        }
    }

    void UpdateTail()
    {
        for (int i = 0; i < tails.Count; i++)
        {
            if (i * 5 < path.Count)
            {
                int pathIndex = Mathf.Min(i * 5, path.Count - 1);
                Vector3 targetPos = path[pathIndex];
                tails[i].position = Vector3.Lerp(tails[i].position, targetPos, tailFollowSpeed * Time.deltaTime);
                
                // Rotate tail to face movement direction
                if (i > 0 && i < path.Count - 1)
                {
                    Vector3 direction = (path[pathIndex] - path[pathIndex + 1]).normalized;
                    float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                    tails[i].rotation = Quaternion.Lerp(tails[i].rotation, Quaternion.AngleAxis(angle, Vector3.forward), tailFollowSpeed * Time.deltaTime);
                }
            }
        }
    }

    void UpdateInvincibility()
    {
        if (isInvincible)
        {
            invincibilityTimer -= Time.deltaTime;
            if (invincibilityTimer <= 0)
            {
                isInvincible = false;
                spriteRenderer.color = Color.white;
            }
            else
            {
                // Blinking effect
                spriteRenderer.color = Mathf.Sin(Time.time * 10f) > 0 ? Color.white : Color.red;
            }
        }
    }

    void UpdateVisualEffects()
    {
        if (trailRenderer != null)
        {
            trailRenderer.startWidth = dragging ? 0.3f : 0.1f;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Handle collectibles
        if (((1 << other.gameObject.layer) & collectibleLayerMask) != 0)
        {
            CollectItem(other.gameObject);
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Handle obstacles
        if (((1 << collision.gameObject.layer) & obstacleLayerMask) != 0)
        {
            TakeDamage();
        }
    }

    void CollectItem(GameObject item)
    {
        // Add score
        currentScore += 10;
        OnScoreChanged?.Invoke(currentScore);
        
        // Play collect effect
        if (collectEffect != null)
        {
            collectEffect.transform.position = item.transform.position;
            collectEffect.Play();
        }
        
        // Destroy item
        Destroy(item);
    }

    void TakeDamage()
    {
        if (isInvincible) return;
        
        currentHealth--;
        OnHealthChanged?.Invoke(currentHealth);
        
        // Play hit effect
        if (hitEffect != null)
        {
            hitEffect.transform.position = transform.position;
            hitEffect.Play();
        }
        
        // Screen shake effect
        StartCoroutine(ScreenShake(0.1f, 0.3f));
        
        if (currentHealth <= 0)
        {
            GameOver();
        }
        else
        {
            // Become invincible
            isInvincible = true;
            invincibilityTimer = invincibilityTime;
        }
    }

    System.Collections.IEnumerator ScreenShake(float duration, float magnitude)
    {
        Vector3 originalPos = cam.transform.localPosition;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;
            
            cam.transform.localPosition = new Vector3(x, y, originalPos.z);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        cam.transform.localPosition = originalPos;
    }

    void GameOver()
    {
        OnGameOver?.Invoke();
        gameObject.SetActive(false);
    }

    public void AddScore(int points)
    {
        currentScore += points;
        OnScoreChanged?.Invoke(currentScore);
    }

    public int GetScore()
    {
        return currentScore;
    }

    public int GetHealth()
    {
        return currentHealth;
    }

    void OnDrawGizmosSelected()
    {
        // Draw world bounds
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(worldBounds.center, worldBounds.size);
        
        // Draw path
        Gizmos.color = Color.green;
        for (int i = 0; i < path.Count - 1; i++)
        {
            Gizmos.DrawLine(path[i], path[i + 1]);
        }
    }
}