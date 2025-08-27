using UnityEngine;

public class TailSegment : MonoBehaviour
{
    [Header("Tail Settings")]
    public float fadeInTime = 0.3f;
    public float fadeOutTime = 0.2f;
    public Color startColor = Color.white;
    public Color endColor = new Color(1f, 1f, 1f, 0.5f);
    
    [Header("Visual Effects")]
    public TrailRenderer trailRenderer;
    public SpriteRenderer spriteRenderer;
    public ParticleSystem tailParticles;
    
    private float fadeTimer = 0f;
    private bool isFadingIn = true;
    private bool isFadingOut = false;
    private Vector3 targetPosition;
    private float moveSpeed = 8f;
    
    void Start()
    {
        InitializeTail();
    }
    
    void Update()
    {
        UpdateFading();
        UpdateMovement();
    }
    
    void InitializeTail()
    {
        // Get components
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
            
        if (trailRenderer == null)
            trailRenderer = GetComponent<TrailRenderer>();
            
        // Set initial color
        if (spriteRenderer != null)
        {
            spriteRenderer.color = startColor;
        }
        
        // Set up trail renderer
        if (trailRenderer != null)
        {
            trailRenderer.startColor = startColor;
            trailRenderer.endColor = endColor;
            trailRenderer.startWidth = 0.2f;
            trailRenderer.endWidth = 0.05f;
            trailRenderer.time = 0.5f;
        }
        
        // Set up particles
        if (tailParticles != null)
        {
            var main = tailParticles.main;
            main.startColor = startColor;
        }
    }
    
    void UpdateFading()
    {
        if (isFadingIn)
        {
            fadeTimer += Time.deltaTime;
            float t = fadeTimer / fadeInTime;
            
            if (t >= 1f)
            {
                isFadingIn = false;
                fadeTimer = 0f;
                if (spriteRenderer != null)
                    spriteRenderer.color = startColor;
            }
            else
            {
                Color color = Color.Lerp(new Color(startColor.r, startColor.g, startColor.b, 0f), startColor, t);
                if (spriteRenderer != null)
                    spriteRenderer.color = color;
            }
        }
        else if (isFadingOut)
        {
            fadeTimer += Time.deltaTime;
            float t = fadeTimer / fadeOutTime;
            
            if (t >= 1f)
            {
                // Destroy tail segment
                Destroy(gameObject);
            }
            else
            {
                Color color = Color.Lerp(startColor, new Color(startColor.r, startColor.g, startColor.b, 0f), t);
                if (spriteRenderer != null)
                    spriteRenderer.color = color;
            }
        }
    }
    
    void UpdateMovement()
    {
        if (Vector3.Distance(transform.position, targetPosition) > 0.01f)
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, moveSpeed * Time.deltaTime);
        }
    }
    
    public void SetTargetPosition(Vector3 position)
    {
        targetPosition = position;
    }
    
    public void SetMoveSpeed(float speed)
    {
        moveSpeed = speed;
    }
    
    public void SetColors(Color start, Color end)
    {
        startColor = start;
        endColor = end;
        
        if (spriteRenderer != null)
            spriteRenderer.color = startColor;
            
        if (trailRenderer != null)
        {
            trailRenderer.startColor = startColor;
            trailRenderer.endColor = endColor;
        }
        
        if (tailParticles != null)
        {
            var main = tailParticles.main;
            main.startColor = startColor;
        }
    }
    
    public void StartFadeOut()
    {
        isFadingOut = true;
        fadeTimer = 0f;
    }
    
    public void SetTrailEnabled(bool enabled)
    {
        if (trailRenderer != null)
            trailRenderer.enabled = enabled;
    }
    
    public void SetParticlesEnabled(bool enabled)
    {
        if (tailParticles != null)
        {
            if (enabled)
                tailParticles.Play();
            else
                tailParticles.Stop();
        }
    }
    
    // Method to make tail segment glow
    public void SetGlow(bool glowing, Color glowColor)
    {
        if (spriteRenderer != null)
        {
            if (glowing)
            {
                spriteRenderer.color = glowColor;
            }
            else
            {
                spriteRenderer.color = startColor;
            }
        }
    }
    
    // Method to set trail properties
    public void SetTrailProperties(float width, float time, Color start, Color end)
    {
        if (trailRenderer != null)
        {
            trailRenderer.startWidth = width;
            trailRenderer.endWidth = width * 0.25f;
            trailRenderer.time = time;
            trailRenderer.startColor = start;
            trailRenderer.endColor = end;
        }
    }
}