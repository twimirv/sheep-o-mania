using UnityEngine;
using System.Collections;

public class SheepSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    [Tooltip("Prefab for the Enemy Alpha Sheep")]
    public GameObject enemyPrefab;
    [Tooltip("Prefab for the Follower Sheep")]
    public GameObject herdSheepPrefab;

    [Header("Spawn Settings")]
    [Tooltip("Number of Enemy Alpha Sheep to spawn")]
    public int enemyCount = 30;
    
    [Tooltip("Minimum number of followers per enemy")]
    public int minHerdSize = 0;
    [Tooltip("Maximum number of followers per enemy")]
    public int maxHerdSize = 10; 

    [Tooltip("Number of free sheep to spawn at start")]
    public int initialFreeSheepCount = 50;

    [Tooltip("Time in seconds between spawning random herd sheep")]
    public float randomSpawnInterval = 0.5f; // Decreased from 1.0f
    
    [Tooltip("Maximum distance from the player to spawn sheep")]
    public float maxSpawnDistance = 100f;
    [Tooltip("Minimum distance from the player to spawn sheep")]
    public float minSpawnDistance = 10f; 

    [Header("Terrain Settings")]
    [Tooltip("LayerMask for Ground detection (if Terrain is activeTerrain is null)")]
    public LayerMask groundLayer = 1; // Default to Default layer
    public Terrain terrain;
    public float spawnHeightOffset = 1.0f; 

    private Transform _playerTransform;

    private void Start()
    {
        if (terrain == null)
        {
            terrain = Terrain.activeTerrain;
        }

        // We no longer return early if terrain is null, we will try Raycast spawning later.
        if (terrain == null)
        {
            // Debug.Log("SheepSpawner: No active Terrain found. Will attempt Raycast spawning on Ground Layer.");
        }

        // Find Player
        var player = FindObjectOfType<AlphaSheepController>();
        if (player != null) 
        {
            _playerTransform = player.transform;
        }
        else
        {
             // Fallback to tag if controller not found
             GameObject tagPlayer = GameObject.FindGameObjectWithTag("Player");
             if (tagPlayer != null) _playerTransform = tagPlayer.transform;
        }

        // Reordered: Spawn free sheep first, then enemies.
        // This ensures free sheep appear even if enemy spawning crashes.
        try
        {
            SpawnInitialFreeSheep(); // Spawn initial batch
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SheepSpawner: Failed to SpawnInitialFreeSheep: {e}");
        }

        try
        {
            SpawnEnemies();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SheepSpawner: Failed to SpawnEnemies: {e}");
        }

        StartCoroutine(SpawnRandomSheepRoutine());
    }

    private void SpawnInitialFreeSheep()
    {
        if (herdSheepPrefab == null) 
        {
            Debug.LogError("SheepSpawner: Herd Prefab is Missing in Initial Spawn!");
            return;
        }

        Debug.Log($"SheepSpawner: Attempting to spawn {initialFreeSheepCount} free sheep...");
        
        int spawned = 0;
        int attempts = 0;
        int maxAttempts = initialFreeSheepCount * 3; // Prevent infinite loop

        while (spawned < initialFreeSheepCount && attempts < maxAttempts)
        {
            attempts++;
            if (SpawnRandomSheep())
            {
                spawned++;
            }
        }
        
        if (spawned < initialFreeSheepCount)
        {
             Debug.LogWarning($"SheepSpawner: Only spawned {spawned}/{initialFreeSheepCount} free sheep after {attempts} attempts. Check ground size/layers.");
        }
        else
        {
             Debug.Log($"SheepSpawner: Successfully spawned {spawned} free sheep.");
        }
    }

    private void SpawnEnemies()
    {
        if (enemyPrefab == null)
        {
            Debug.LogWarning("SheepSpawner: Enemy Prefab is not assigned.");
            return;
        }
        
        Debug.Log($"SheepSpawner: Attempting to spawn {enemyCount} enemies...");

        for (int i = 0; i < enemyCount; i++)
        {
            try
            {
                Vector3 spawnPos = GetRandomPositionOnTerrain();
                // If we couldn't find a spot (e.g. no player), skip or stick to zero
                if (spawnPos == Vector3.zero && _playerTransform == null) continue;

                GameObject enemyObj = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
                
                enemyObj.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

                EnemyAlphaSheepController enemyController = enemyObj.GetComponent<EnemyAlphaSheepController>();
                
                if (enemyController != null && herdSheepPrefab != null)
                {
                    int herdSize = Random.Range(minHerdSize, maxHerdSize + 1);
                    SpawnEnemyHerd(enemyController, herdSize);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"SheepSpawner: Error spawning enemy {i}: {e}");
            }
        }
    }

    private void SpawnEnemyHerd(EnemyAlphaSheepController leader, int count)
    {
        for (int i = 0; i < count; i++)
        {
            // Spawn around the leader within a small radius
            Vector2 randomCircle = Random.insideUnitCircle * 8.0f;
            Vector3 offset = new Vector3(randomCircle.x, 0, randomCircle.y);
            Vector3 followerPos = leader.transform.position + offset;
            
            followerPos.y = GetGroundHeight(followerPos) + spawnHeightOffset;

            GameObject sheepObj = Instantiate(herdSheepPrefab, followerPos, Quaternion.identity);
            
            sheepObj.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

            FollowerSheepController follower = sheepObj.GetComponent<FollowerSheepController>();
            if (follower != null)
            {
                follower.JoinLeader(leader);
            }
        }
    }

    private IEnumerator SpawnRandomSheepRoutine()
    {
        if (herdSheepPrefab == null)
        {
            Debug.LogWarning("SheepSpawner: Herd Sheep Prefab is not assigned. Random spawning disabled.");
            yield break;
        }

        while (true)
        {
            yield return new WaitForSeconds(randomSpawnInterval);
            SpawnRandomSheep();
        }
    }

    private bool SpawnRandomSheep()
    {
        Vector3 spawnPos = GetRandomPositionOnTerrain();
        if (spawnPos != Vector3.zero)
        {
            GameObject sheepObj = Instantiate(herdSheepPrefab, spawnPos, Quaternion.identity);
            sheepObj.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            return true;
        }
        else
        {
            Debug.LogWarning("SheepSpawner: Could not spawn random sheep (no valid position found). Check Raycast/Ground settings.");
            return false;
        }
    }

    private Vector3 GetRandomPositionOnTerrain()
    {
        // Origin for spawning: Prioritize Player, then Spawner itself
        Vector3 origin = transform.position;
        if (_playerTransform != null)
        {
            origin = _playerTransform.position;
        }

        // Random point within min/max distance
        // Get random direction
        Vector2 randomCircle = Random.insideUnitCircle.normalized;
        float distance = Random.Range(minSpawnDistance, maxSpawnDistance);
        
        Vector3 randomPos = origin + new Vector3(randomCircle.x * distance, 0, randomCircle.y * distance);

        // Clamp to Terrain Bounds IF we are using Terrain
        if (terrain != null)
        {
            TerrainData terrainData = terrain.terrainData;
            Vector3 terrainPos = terrain.GetPosition();

            float minX = terrainPos.x;
            float maxX = terrainPos.x + terrainData.size.x;
            float minZ = terrainPos.z;
            float maxZ = terrainPos.z + terrainData.size.z;

            randomPos.x = Mathf.Clamp(randomPos.x, minX, maxX);
            randomPos.z = Mathf.Clamp(randomPos.z, minZ, maxZ);
        }

        // Sample Height
        float worldY = GetGroundHeight(randomPos); 
        // If GetGroundHeight returns float.MinValue, it means no ground found.
        if (worldY == float.MinValue) 
        {
            // Debug.LogWarning($"SheepSpawner: Could not find ground at {randomPos.x}, {randomPos.z}");
            return Vector3.zero;
        }

        Vector3 finalPos = new Vector3(randomPos.x, worldY + spawnHeightOffset, randomPos.z);
        // Debug.Log($"Found valid spawn position: {finalPos}");
        return finalPos;
    }

    private float GetGroundHeight(Vector3 pos)
    {
        // 1. Try Terrain
        if (terrain != null)
        {
            return terrain.SampleHeight(pos) + terrain.GetPosition().y;
        }

        // 2. Try Raycast
        // Cast from high up down to find ground
        RaycastHit hit;
        
        // Debug.Log($"Raycasting from {pos.x}, 500, {pos.z} against {groundLayer.value}");
        
        if (Physics.Raycast(new Vector3(pos.x, 500f, pos.z), Vector3.down, out hit, 1000f, groundLayer))
        {
            // Debug.Log($"Raycast Hit: {hit.collider.name} at {hit.point}");
            return hit.point.y;
        }
        
        Debug.LogWarning($"SheepSpawner: Raycast failed to find ground at ({pos.x}, {pos.z}). Ensure your Ground has a Collider and is on a layer included in the 'Ground Layer' mask.");
        
        // No ground found
        return float.MinValue;
    }
}
