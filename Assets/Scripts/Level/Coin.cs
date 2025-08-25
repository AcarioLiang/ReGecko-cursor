using UnityEngine;

public class Coin : CollectibleBase
{
    [Header("Coin Specific")]
    public int coinValue = 1;
    public float magnetRange = 3f;
    public float magnetForce = 5f;
    
    private Transform player;
    private bool isBeingMagneted = false;
    
    protected override void Start()
    {
        base.Start();
        
        // Set coin-specific values
        pointValue = coinValue * 10; // 1 coin = 10 points
        
        // Find player for magnet effect
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
    }
    
    protected override void Update()
    {
        if (!isCollected)
        {
            base.Update();
            UpdateMagnetEffect();
        }
    }
    
    void UpdateMagnetEffect()
    {
        if (player == null) return;
        
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        
        if (distanceToPlayer <= magnetRange)
        {
            // Start magnet effect
            isBeingMagneted = true;
            
            // Move towards player
            Vector3 direction = (player.position - transform.position).normalized;
            transform.position += direction * magnetForce * Time.deltaTime;
            
            // Increase rotation speed when magneted
            rotationSpeed = 180f;
        }
        else
        {
            isBeingMagneted = false;
            rotationSpeed = 90f;
        }
    }
    
    protected override void OnCollected(GameObject player)
    {
        // Play special coin collect effect
        if (collectEffect != null)
        {
            GameObject effect = Instantiate(collectEffect, transform.position, Quaternion.identity);
            
            // Scale effect based on coin value
            if (coinValue > 1)
            {
                effect.transform.localScale *= Mathf.Min(coinValue * 0.5f, 2f);
            }
        }
        
        // Call base collection logic
        base.OnCollected(player);
    }
    
    // Method to set coin value
    public void SetCoinValue(int value)
    {
        coinValue = value;
        pointValue = coinValue * 10;
    }
    
    // Method to set magnet properties
    public void SetMagnetProperties(float range, float force)
    {
        magnetRange = range;
        magnetForce = force;
    }
    
    void OnDrawGizmosSelected()
    {
        // Draw magnet range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, magnetRange);
        
        // Draw magnet direction if player is nearby
        if (player != null)
        {
            float distance = Vector3.Distance(transform.position, player.position);
            if (distance <= magnetRange)
            {
                Gizmos.color = Color.orange;
                Gizmos.DrawLine(transform.position, player.position);
            }
        }
    }
}