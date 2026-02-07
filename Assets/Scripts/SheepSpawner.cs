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
    public int initialFreeSheepCount = 20;

    [Tooltip("Time in seconds between spawning random herd sheep")]
    public float randomSpawnInterval = 1.0f; // Reduced from 5.0f
    
    [Tooltip("Maximum distance from the player to spawn sheep")]
    public float maxSpawnDistance = 100f;

    [Header("Terrain Settings")]
    public Terrain terrain;
    public float spawnHeightOffset = 1.0f; 

    private Transform _playerTransform;

    private void Start()
    {
        if (terrain == null)
        {
            terrain = Terrain.activeTerrain;
        }

        if (terrain == null)
        {
            Debug.LogError("SheepSpawner: No valid Terrain found!");
            return;
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

        SpawnEnemies();
        SpawnInitialFreeSheep(); // Spawn initial batch
        StartCoroutine(SpawnRandomSheepRoutine());
    }

    private void SpawnInitialFreeSheep()
    {
        if (herdSheepPrefab == null) return;
        
        for (int i = 0; i < initialFreeSheepCount; i++)
        {
            SpawnRandomSheep();
        }
    }

    private void SpawnEnemies()
    {
        if (enemyPrefab == null)
        {
            Debug.LogWarning("SheepSpawner: Enemy Prefab is not assigned.");
            return;
        }

        for (int i = 0; i < enemyCount; i++)
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
    }

    private void SpawnEnemyHerd(EnemyAlphaSheepController leader, int count)
    {
        for (int i = 0; i < count; i++)
        {
            // Spawn around the leader within a small radius
            Vector2 randomCircle = Random.insideUnitCircle * 8.0f;
            Vector3 offset = new Vector3(randomCircle.x, 0, randomCircle.y);
            Vector3 followerPos = leader.transform.position + offset;
            
            followerPos.y = terrain.SampleHeight(followerPos) + terrain.GetPosition().y + spawnHeightOffset;

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

    private void SpawnRandomSheep()
    {
        Vector3 spawnPos = GetRandomPositionOnTerrain();
        if (spawnPos != Vector3.zero)
        {
            GameObject sheepObj = Instantiate(herdSheepPrefab, spawnPos, Quaternion.identity);
            sheepObj.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
        }
    }

    private Vector3 GetRandomPositionOnTerrain()
    {
        if (terrain == null) return Vector3.zero;

        // Origin for spawning
        Vector3 origin = Vector3.zero;
        
        if (_playerTransform != null)
        {
            origin = _playerTransform.position;
        }
        else
        {
            // Fallback to center of terrain if no player found (or logic to find player again)
            TerrainData td = terrain.terrainData;
            origin = terrain.GetPosition() + new Vector3(td.size.x * 0.5f, 0, td.size.z * 0.5f);
        }

        // Random point within distance
        Vector2 randomCircle = Random.insideUnitCircle * maxSpawnDistance;
        Vector3 randomPos = origin + new Vector3(randomCircle.x, 0, randomCircle.y);

        // Clamp to Terrain Bounds
        TerrainData terrainData = terrain.terrainData;
        Vector3 terrainPos = terrain.GetPosition();

        float minX = terrainPos.x;
        float maxX = terrainPos.x + terrainData.size.x;
        float minZ = terrainPos.z;
        float maxZ = terrainPos.z + terrainData.size.z;

        randomPos.x = Mathf.Clamp(randomPos.x, minX, maxX);
        randomPos.z = Mathf.Clamp(randomPos.z, minZ, maxZ);

        // Sample Height
        float worldY = terrain.SampleHeight(randomPos) + terrainPos.y;

        return new Vector3(randomPos.x, worldY + spawnHeightOffset, randomPos.z);
    }
}
