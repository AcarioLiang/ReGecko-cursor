using UnityEngine;

public abstract class CollectibleBase : MonoBehaviour
{
    [Header("Collectible Settings")]
    public int pointValue = 10;
    public bool isCollected = false;
    public float rotationSpeed = 90f;
    public float bobSpeed = 2f;
    public float bobHeight = 0.2f;
    
    [Header("Visual Effects")]
    public GameObject collectEffect;
    public SpriteRenderer spriteRenderer;
    public Color normalColor = Color.white;
    public Color glowColor = Color.yellow;
    
    [Header("Audio")]
    public AudioClip collectSound;
    public float volume = 0.5f;
    
    protected Vector3 startPosition;
    protected float bobTimer = 0f;
    protected bool isGlowing = false;
    protected AudioSource audioSource;
    
    protected virtual void Start()
    {
        startPosition = transform.position;
        
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
            
        // Set up audio source
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
            
        audioSource.volume = volume;
        audioSource.playOnAwake = false;
        
        // Set up collision as trigger
        SetupCollision();
    }
    
    protected virtual void Update()
    {
        if (!isCollected)
        {
            UpdateVisual();
        }
    }
    
    protected virtual void UpdateVisual()
    {
        // Rotate the collectible
        transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);
        
        // Bob up and down
        bobTimer += Time.deltaTime * bobSpeed;
        float bobOffset = Mathf.Sin(bobTimer) * bobHeight;
        transform.position = startPosition + Vector3.up * bobOffset;
        
        // Glow effect
        if (spriteRenderer != null)
        {
            if (isGlowing)
            {
                spriteRenderer.color = Color.Lerp(normalColor, glowColor, Mathf.Sin(Time.time * 5f) * 0.5f + 0.5f);
            }
            else
            {
                spriteRenderer.color = normalColor;
            }
        }
    }
    
    protected virtual void SetupCollision()
    {
        // Ensure we have a collider set as trigger
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            col = gameObject.AddComponent<CircleCollider2D>();
        }
        
        // Set as trigger
        if (col is CircleCollider2D)
        {
            (col as CircleCollider2D).isTrigger = true;
        }
        else if (col is BoxCollider2D)
        {
            (col as BoxCollider2D).isTrigger = true;
        }
    }
    
    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && !isCollected)
        {
            OnCollected(other.gameObject);
        }
    }
    
    protected virtual void OnCollected(GameObject player)
    {
        if (isCollected) return;
        
        isCollected = true;
        
        // Play collect effect
        if (collectEffect != null)
        {
            Instantiate(collectEffect, transform.position, Quaternion.identity);
        }
        
        // Play collect sound
        if (collectSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(collectSound);
        }
        
        // Add score to player
        GeckoController gecko = player.GetComponent<GeckoController>();
        if (gecko != null)
        {
            gecko.AddScore(pointValue);
        }
        
        // Hide the collectible
        HideCollectible();
        
        // Destroy after a short delay to allow sound to play
        Destroy(gameObject, 0.1f);
    }
    
    protected virtual void HideCollectible()
    {
        // Hide sprite and disable collider
        if (spriteRenderer != null)
            spriteRenderer.enabled = false;
            
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.enabled = false;
    }
    
    // Method to set visual parameters
    public virtual void SetVisual(Color normal, Color glow)
    {
        normalColor = normal;
        glowColor = glow;
    }
    
    // Method to set movement parameters
    public virtual void SetMovement(float rotation, float bob, float height)
    {
        rotationSpeed = rotation;
        bobSpeed = bob;
        bobHeight = height;
    }
    
    // Method to set audio parameters
    public virtual void SetAudio(AudioClip sound, float vol)
    {
        collectSound = sound;
        volume = vol;
        if (audioSource != null)
            audioSource.volume = volume;
    }
    
    // Method to start glowing effect
    public virtual void StartGlowing()
    {
        isGlowing = true;
    }
    
    // Method to stop glowing effect
    public virtual void StopGlowing()
    {
        isGlowing = false;
    }
    
    // Method to set point value
    public virtual void SetPointValue(int points)
    {
        pointValue = points;
    }
    
    void OnDrawGizmosSelected()
    {
        // Draw bob range
        Gizmos.color = Color.cyan;
        Vector3 topPos = transform.position + Vector3.up * bobHeight;
        Vector3 bottomPos = transform.position - Vector3.up * bobHeight;
        Gizmos.DrawLine(topPos, bottomPos);
        Gizmos.DrawWireSphere(topPos, 0.1f);
        Gizmos.DrawWireSphere(bottomPos, 0.1f);
    }
}