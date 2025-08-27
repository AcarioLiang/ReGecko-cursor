using UnityEngine;

public abstract class ObstacleBase : MonoBehaviour
{
    [Header("Obstacle Settings")]
    public float damage = 1;
    public bool isDestructible = false;
    public float health = 1f;
    public GameObject destroyEffect;
    
    [Header("Movement")]
    public bool isMoving = false;
    public float moveSpeed = 2f;
    public Vector2 moveDirection = Vector2.left;
    public float moveDistance = 5f;
    
    [Header("Visual")]
    public SpriteRenderer spriteRenderer;
    public Color normalColor = Color.white;
    public Color warningColor = Color.red;
    
    protected Vector3 startPosition;
    protected float moveTimer = 0f;
    protected bool isWarning = false;
    
    protected virtual void Start()
    {
        startPosition = transform.position;
        
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
            
        // Set up collision
        SetupCollision();
    }
    
    protected virtual void Update()
    {
        if (isMoving)
        {
            UpdateMovement();
        }
        
        UpdateVisual();
    }
    
    protected virtual void UpdateMovement()
    {
        moveTimer += Time.deltaTime;
        
        // Simple back and forth movement
        float t = Mathf.Sin(moveTimer * moveSpeed) * 0.5f + 0.5f;
        Vector3 targetPos = startPosition + (Vector3)(moveDirection * moveDistance * t);
        transform.position = Vector3.Lerp(transform.position, targetPos, moveSpeed * Time.deltaTime);
    }
    
    protected virtual void UpdateVisual()
    {
        if (spriteRenderer != null)
        {
            // Warning effect when player is close
            if (isWarning)
            {
                spriteRenderer.color = Color.Lerp(normalColor, warningColor, Mathf.Sin(Time.time * 10f) * 0.5f + 0.5f);
            }
            else
            {
                spriteRenderer.color = normalColor;
            }
        }
    }
    
    protected virtual void SetupCollision()
    {
        // Ensure we have a collider
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            col = gameObject.AddComponent<BoxCollider2D>();
        }
        
        // Set as trigger if needed
        if (col is BoxCollider2D)
        {
            (col as BoxCollider2D).isTrigger = false;
        }
    }
    
    protected virtual void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            OnPlayerHit(collision.gameObject);
        }
    }
    
    protected virtual void OnPlayerHit(GameObject player)
    {
        // Apply damage to player
        GeckoController gecko = player.GetComponent<GeckoController>();
        if (gecko != null)
        {
            // The damage is handled by the GeckoController's collision system
            // This is just for additional effects
        }
        
        // Play hit effect
        if (destroyEffect != null)
        {
            Instantiate(destroyEffect, transform.position, Quaternion.identity);
        }
        
        // Destroy obstacle if destructible
        if (isDestructible)
        {
            DestroyObstacle();
        }
    }
    
    protected virtual void DestroyObstacle()
    {
        // Play destroy effect
        if (destroyEffect != null)
        {
            Instantiate(destroyEffect, transform.position, Quaternion.identity);
        }
        
        // Destroy the obstacle
        Destroy(gameObject);
    }
    
    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            OnPlayerHit(other.gameObject);
        }
    }
    
    // Called when player gets close (for warning effects)
    protected virtual void OnPlayerNearby()
    {
        isWarning = true;
    }
    
    // Called when player moves away
    protected virtual void OnPlayerFarAway()
    {
        isWarning = false;
    }
    
    // Public method to damage the obstacle
    public virtual void TakeDamage(float damageAmount)
    {
        if (!isDestructible) return;
        
        health -= damageAmount;
        
        if (health <= 0)
        {
            DestroyObstacle();
        }
    }
    
    // Method to set movement parameters
    public virtual void SetMovement(bool moving, float speed, Vector2 direction, float distance)
    {
        isMoving = moving;
        moveSpeed = speed;
        moveDirection = direction.normalized;
        moveDistance = distance;
    }
    
    // Method to set visual parameters
    public virtual void SetVisual(Color normal, Color warning)
    {
        normalColor = normal;
        warningColor = warning;
    }
    
    void OnDrawGizmosSelected()
    {
        if (isMoving)
        {
            Gizmos.color = Color.yellow;
            Vector3 endPos = startPosition + (Vector3)(moveDirection * moveDistance);
            Gizmos.DrawLine(startPosition, endPos);
            Gizmos.DrawWireSphere(startPosition, 0.2f);
            Gizmos.DrawWireSphere(endPos, 0.2f);
        }
    }
}