using System.Collections;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Follow Settings")]
    public Transform target;
    public Vector3 offset = new Vector3(0, 0, -10);
    public float followSpeed = 5f;
    public bool smoothFollow = true;
    
    [Header("Screen Shake")]
    public float shakeDecay = 0.95f;
    public float shakeIntensity = 0.1f;
    
    [Header("Boundaries")]
    public bool useBoundaries = true;
    public Rect worldBounds = new Rect(-20f, -12f, 40f, 24f);
    
    private Vector3 originalPosition;
    private Vector3 shakeOffset = Vector3.zero;
    private bool isShaking = false;
    
    void Start()
    {
        if (target == null)
        {
            target = GameObject.FindGameObjectWithTag("Player")?.transform;
        }
        
        originalPosition = transform.position;
        
        // Set initial position
        if (target != null)
        {
            transform.position = target.position + offset;
        }
    }
    
    void LateUpdate()
    {
        if (target != null)
        {
            UpdateCameraPosition();
        }
        
        // Apply screen shake
        if (isShaking)
        {
            UpdateScreenShake();
        }
    }
    
    void UpdateCameraPosition()
    {
        Vector3 targetPosition = target.position + offset;
        
        // Apply boundaries if enabled
        if (useBoundaries)
        {
            targetPosition = ClampToBoundaries(targetPosition);
        }
        
        // Smooth follow or instant follow
        if (smoothFollow)
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, followSpeed * Time.deltaTime);
        }
        else
        {
            transform.position = targetPosition;
        }
        
        // Apply shake offset
        transform.position += shakeOffset;
    }
    
    Vector3 ClampToBoundaries(Vector3 position)
    {
        // Calculate camera bounds based on world bounds
        float cameraHalfHeight = Camera.main.orthographicSize;
        float cameraHalfWidth = cameraHalfHeight * Camera.main.aspect;
        
        float minX = worldBounds.xMin + cameraHalfWidth;
        float maxX = worldBounds.xMax - cameraHalfWidth;
        float minY = worldBounds.yMin + cameraHalfHeight;
        float maxY = worldBounds.yMax - cameraHalfHeight;
        
        position.x = Mathf.Clamp(position.x, minX, maxX);
        position.y = Mathf.Clamp(position.y, minY, maxY);
        
        return position;
    }
    
    void UpdateScreenShake()
    {
        shakeOffset *= shakeDecay;
        
        if (shakeOffset.magnitude < 0.01f)
        {
            shakeOffset = Vector3.zero;
            isShaking = false;
        }
    }
    
    // Public method to trigger screen shake
    public void ShakeCamera(float intensity = -1f, float duration = 0.5f)
    {
        if (intensity < 0)
            intensity = shakeIntensity;
            
        StartCoroutine(ShakeCoroutine(intensity, duration));
    }
    
    IEnumerator ShakeCoroutine(float intensity, float duration)
    {
        isShaking = true;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            shakeOffset = Random.insideUnitSphere * intensity;
            shakeOffset.z = 0; // Keep shake in 2D
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Gradually reduce shake
        while (shakeOffset.magnitude > 0.01f)
        {
            shakeOffset *= shakeDecay;
            yield return null;
        }
        
        shakeOffset = Vector3.zero;
        isShaking = false;
    }
    
    // Method to set target
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
    
    // Method to set offset
    public void SetOffset(Vector3 newOffset)
    {
        offset = newOffset;
    }
    
    // Method to set follow speed
    public void SetFollowSpeed(float speed)
    {
        followSpeed = speed;
    }
    
    // Method to set boundaries
    public void SetBoundaries(Rect bounds)
    {
        worldBounds = bounds;
    }
    
    // Method to enable/disable boundaries
    public void SetBoundariesEnabled(bool enabled)
    {
        useBoundaries = enabled;
    }
    
    // Method to reset camera to original position
    public void ResetCamera()
    {
        transform.position = originalPosition;
        shakeOffset = Vector3.zero;
        isShaking = false;
    }
    
    void OnDrawGizmosSelected()
    {
        if (useBoundaries)
        {
            // Draw world boundaries
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(worldBounds.center, worldBounds.size);
            
            // Draw camera bounds
            if (Camera.main != null)
            {
                float cameraHalfHeight = Camera.main.orthographicSize;
                float cameraHalfWidth = cameraHalfHeight * Camera.main.aspect;
                
                Vector3 cameraMin = new Vector3(worldBounds.xMin + cameraHalfWidth, worldBounds.yMin + cameraHalfHeight, 0);
                Vector3 cameraMax = new Vector3(worldBounds.xMax - cameraHalfWidth, worldBounds.yMax - cameraHalfHeight, 0);
                
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube((cameraMin + cameraMax) * 0.5f, cameraMax - cameraMin);
            }
        }
        
        // Draw target and offset
        if (target != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(target.position, 0.5f);
            Gizmos.DrawLine(target.position, target.position + offset);
            Gizmos.DrawWireSphere(target.position + offset, 0.3f);
        }
    }
}