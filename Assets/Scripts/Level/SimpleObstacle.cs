using UnityEngine;

public class SimpleObstacle : ObstacleBase
{
    [Header("Simple Obstacle")]
    public bool isRotating = false;
    public float rotationSpeed = 45f;
    public bool isScaling = false;
    public float scaleSpeed = 1f;
    public float scaleRange = 0.2f;
    
    private Vector3 originalScale;
    private float scaleTimer = 0f;
    
    protected override void Start()
    {
        base.Start();
        originalScale = transform.localScale;
    }
    
    protected override void Update()
    {
        base.Update();
        
        if (isRotating)
        {
            UpdateRotation();
        }
        
        if (isScaling)
        {
            UpdateScaling();
        }
    }
    
    void UpdateRotation()
    {
        transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);
    }
    
    void UpdateScaling()
    {
        scaleTimer += Time.deltaTime * scaleSpeed;
        float scale = 1f + Mathf.Sin(scaleTimer) * scaleRange;
        transform.localScale = originalScale * scale;
    }
    
    protected override void OnPlayerHit(GameObject player)
    {
        // Play hit effect
        if (destroyEffect != null)
        {
            Instantiate(destroyEffect, transform.position, Quaternion.identity);
        }
        
        // Call base hit logic
        base.OnPlayerHit(player);
    }
    
    // Method to set rotation properties
    public void SetRotation(bool rotating, float speed)
    {
        isRotating = rotating;
        rotationSpeed = speed;
    }
    
    // Method to set scaling properties
    public void SetScaling(bool scaling, float speed, float range)
    {
        isScaling = scaling;
        scaleSpeed = speed;
        scaleRange = range;
    }
    
    // Method to make obstacle destructible
    public void MakeDestructible(float healthPoints)
    {
        isDestructible = true;
        health = healthPoints;
    }
    
    void OnDrawGizmosSelected()
    {
        // Draw rotation indicator
        if (isRotating)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            Gizmos.DrawRay(transform.position, transform.right * 0.8f);
        }
        
        // Draw scale indicator
        if (isScaling)
        {
            Gizmos.color = Color.blue;
            Vector3 minScale = originalScale * (1f - scaleRange);
            Vector3 maxScale = originalScale * (1f + scaleRange);
            Gizmos.DrawWireCube(transform.position, minScale);
            Gizmos.DrawWireCube(transform.position, maxScale);
        }
    }
}