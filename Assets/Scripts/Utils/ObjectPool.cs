using System.Collections.Generic;
using UnityEngine;

public class ObjectPool : MonoBehaviour
{
    [System.Serializable]
    public class PoolItem
    {
        public string tag;
        public GameObject prefab;
        public int size;
    }
    
    [Header("Pool Settings")]
    public List<PoolItem> pools;
    
    private Dictionary<string, Queue<GameObject>> poolDictionary;
    private Dictionary<string, GameObject> prefabDictionary;
    
    void Awake()
    {
        poolDictionary = new Dictionary<string, Queue<GameObject>>();
        prefabDictionary = new Dictionary<string, GameObject>();
        
        InitializePools();
    }
    
    void InitializePools()
    {
        foreach (PoolItem item in pools)
        {
            if (string.IsNullOrEmpty(item.tag) || item.prefab == null)
                continue;
                
            Queue<GameObject> objectPool = new Queue<GameObject>();
            
            for (int i = 0; i < item.size; i++)
            {
                GameObject obj = CreateNewObject(item.prefab, item.tag);
                objectPool.Enqueue(obj);
            }
            
            poolDictionary.Add(item.tag, objectPool);
            prefabDictionary.Add(item.tag, item.prefab);
        }
    }
    
    GameObject CreateNewObject(GameObject prefab, string tag)
    {
        GameObject obj = Instantiate(prefab);
        obj.SetActive(false);
        
        // Add pool tag component
        PooledObject pooledObj = obj.GetComponent<PooledObject>();
        if (pooledObj == null)
        {
            pooledObj = obj.AddComponent<PooledObject>();
        }
        pooledObj.poolTag = tag;
        
        return obj;
    }
    
    public GameObject SpawnFromPool(string tag, Vector3 position, Quaternion rotation)
    {
        if (!poolDictionary.ContainsKey(tag))
        {
            Debug.LogWarning($"Pool with tag {tag} doesn't exist.");
            return null;
        }
        
        Queue<GameObject> pool = poolDictionary[tag];
        
        // If pool is empty, create new object
        if (pool.Count == 0)
        {
            GameObject prefab = prefabDictionary[tag];
            GameObject newObj = CreateNewObject(prefab, tag);
            pool.Enqueue(newObj);
        }
        
        GameObject objectToSpawn = pool.Dequeue();
        
        // Reset object state
        objectToSpawn.SetActive(true);
        objectToSpawn.transform.position = position;
        objectToSpawn.transform.rotation = rotation;
        
        // Reset pooled object component
        PooledObject pooledObj = objectToSpawn.GetComponent<PooledObject>();
        if (pooledObj != null)
        {
            pooledObj.ResetObject();
        }
        
        return objectToSpawn;
    }
    
    public void ReturnToPool(GameObject obj)
    {
        PooledObject pooledObj = obj.GetComponent<PooledObject>();
        if (pooledObj == null)
        {
            Debug.LogWarning("Object doesn't have PooledObject component.");
            return;
        }
        
        string tag = pooledObj.poolTag;
        
        if (!poolDictionary.ContainsKey(tag))
        {
            Debug.LogWarning($"Pool with tag {tag} doesn't exist.");
            return;
        }
        
        // Reset object
        obj.SetActive(false);
        obj.transform.SetParent(transform);
        
        // Return to pool
        poolDictionary[tag].Enqueue(obj);
    }
    
    public void ReturnToPool(GameObject obj, float delay)
    {
        StartCoroutine(ReturnToPoolDelayed(obj, delay));
    }
    
    System.Collections.IEnumerator ReturnToPoolDelayed(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        ReturnToPool(obj);
    }
    
    // Method to expand pool size
    public void ExpandPool(string tag, int additionalSize)
    {
        if (!poolDictionary.ContainsKey(tag))
        {
            Debug.LogWarning($"Pool with tag {tag} doesn't exist.");
            return;
        }
        
        GameObject prefab = prefabDictionary[tag];
        Queue<GameObject> pool = poolDictionary[tag];
        
        for (int i = 0; i < additionalSize; i++)
        {
            GameObject obj = CreateNewObject(prefab, tag);
            pool.Enqueue(obj);
        }
    }
    
    // Method to clear pool
    public void ClearPool(string tag)
    {
        if (!poolDictionary.ContainsKey(tag))
            return;
            
        Queue<GameObject> pool = poolDictionary[tag];
        
        while (pool.Count > 0)
        {
            GameObject obj = pool.Dequeue();
            if (obj != null)
            {
                Destroy(obj);
            }
        }
    }
    
    // Method to get pool info
    public int GetPoolSize(string tag)
    {
        if (!poolDictionary.ContainsKey(tag))
            return 0;
            
        return poolDictionary[tag].Count;
    }
    
    // Method to check if pool exists
    public bool PoolExists(string tag)
    {
        return poolDictionary.ContainsKey(tag);
    }
    
    void OnDestroy()
    {
        // Clean up all pools
        foreach (var pool in poolDictionary.Values)
        {
            while (pool.Count > 0)
            {
                GameObject obj = pool.Dequeue();
                if (obj != null)
                {
                    Destroy(obj);
                }
            }
        }
    }
}

// Component to track pooled objects
public class PooledObject : MonoBehaviour
{
    public string poolTag;
    
    public virtual void ResetObject()
    {
        // Override this method in derived classes to reset object state
    }
    
    // Auto-return to pool when disabled
    void OnDisable()
    {
        if (!string.IsNullOrEmpty(poolTag))
        {
            ObjectPool pool = FindObjectOfType<ObjectPool>();
            if (pool != null)
            {
                pool.ReturnToPool(gameObject);
            }
        }
    }
}