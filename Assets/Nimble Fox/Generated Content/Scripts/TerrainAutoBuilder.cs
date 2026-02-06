using UnityEngine;

[ExecuteAlways]
public class TerrainAutoBuilder : MonoBehaviour
{
    [Header("Terrain Identity")]
    [SerializeField] private string terrainName = "MainTerrain";
    [SerializeField] private Vector3 terrainPosition = new Vector3(-500f, 0f, -500f);
    [SerializeField] private Vector3 terrainSize = new Vector3(1000f, 1000f, 1000f);

    [Header("Terrain Resolution")]
    [SerializeField] private int heightmapResolution = 513;
    [SerializeField] private int detailResolution = 1024;

    [Header("Height Noise")]
    [SerializeField] private float heightScale = 25f;
    [SerializeField] private float noiseScale = 0.0025f;
    [SerializeField] private int noiseOctaves = 4;
    [SerializeField] private float noisePersistence = 0.5f;
    [SerializeField] private float noiseLacunarity = 2.0f;
    [SerializeField] private int seed = 12345;

    [Header("Grass Settings")]
    [Tooltip("Maximum slope angle (degrees) where grass is still allowed at full density.")]
    [SerializeField] private float grassMaxSteepness = 30f;
    [Tooltip("Approximate visual size of individual grass patches.")]
    [SerializeField] private float grassPatchScale = 1.0f;
    [Tooltip("Global grass density multiplier in [0, 1].")]
    [Range(0f, 1f)]
    [SerializeField] private float grassDensity = 0.45f;
    [SerializeField] private Color grassHealthyColor = new Color(0.35f, 0.55f, 0.25f);
    [SerializeField] private Color grassDryColor = new Color(0.6f, 0.6f, 0.3f);

    // ----------------------------------------------------------------------
    // Unity Messages
    // ----------------------------------------------------------------------

    private void Reset()
    {
        // Default values tuned to match the design plan.
        terrainName = "MainTerrain";
        terrainPosition = new Vector3(-500f, 0f, -500f);
        terrainSize = new Vector3(1000f, 100f, 1000f); // Y=100 gives gentle hills without extreme cliffs

        heightmapResolution = 513;
        detailResolution = 1024;

        heightScale = 25f;
        noiseScale = 0.0035f;
        noiseOctaves = 4;
        noisePersistence = 0.45f;
        noiseLacunarity = 2.0f;
        seed = 12345;

        grassMaxSteepness = 30f;
        grassPatchScale = 1.0f;
        grassDensity = 0.45f;
        grassHealthyColor = new Color(0.35f, 0.55f, 0.25f);
        grassDryColor = new Color(0.6f, 0.6f, 0.3f);
    }

    // ----------------------------------------------------------------------
    // Public API
    // ----------------------------------------------------------------------

    /// <summary>
    /// Creates (or finds) the terrain and applies heightmap, textures, and grass details.
    /// </summary>
    public void CreateOrUpdateTerrain()
    {
        Terrain terrain = GetOrCreateTerrain();
        if (terrain == null)
            return;

        ApplyTerrainData(terrain);
        ApplyGrassDetails(terrain);
    }

    // ----------------------------------------------------------------------
    // Terrain Creation & Core Data
    // ----------------------------------------------------------------------

    /// <summary>
    /// Gets an existing terrain with the configured name, or creates a new one.
    /// </summary>
    private Terrain GetOrCreateTerrain()
    {
        Terrain terrain = null;

        // Try to find existing terrain GameObject by name
        if (!string.IsNullOrEmpty(terrainName))
        {
            GameObject existing = GameObject.Find(terrainName);
            if (existing != null)
            {
                terrain = existing.GetComponent<Terrain>();
            }
        }

        // If none found, also check active terrains
        if (terrain == null)
        {
            foreach (var t in Terrain.activeTerrains)
            {
                if (t != null && t.gameObject.name == terrainName)
                {
                    terrain = t;
                    break;
                }
            }
        }

        // Create new terrain GameObject if still not found
        if (terrain == null)
        {
            TerrainData data = new TerrainData();
            data.heightmapResolution = Mathf.Clamp(heightmapResolution, 33, 4097);
            data.size = new Vector3(
                Mathf.Max(1f, terrainSize.x),
                Mathf.Max(1f, terrainSize.y),
                Mathf.Max(1f, terrainSize.z));

            GameObject terrainGO = Terrain.CreateTerrainGameObject(data);
            terrainGO.name = terrainName;
            terrainGO.transform.position = terrainPosition;

            terrain = terrainGO.GetComponent<Terrain>();
        }
        else
        {
            // Ensure terrain is at the desired position
            terrain.transform.position = terrainPosition;
        }

        return terrain;
    }

    /// <summary>
    /// Builds and assigns TerrainData: heights, basic terrain layer, resolution, etc.
    /// </summary>
    private void ApplyTerrainData(Terrain terrain)
    {
        if (terrain == null)
            return;

        TerrainData data = terrain.terrainData;

        // Ensure we have a TerrainData object
        if (data == null)
        {
            data = new TerrainData();
            terrain.terrainData = data;
        }

        // Configure resolutions and size
        heightmapResolution = Mathf.Clamp(heightmapResolution, 33, 4097);
        data.heightmapResolution = heightmapResolution;
        data.size = new Vector3(
            Mathf.Max(1f, terrainSize.x),
            Mathf.Max(1f, terrainSize.y),
            Mathf.Max(1f, terrainSize.z));

        int res = data.heightmapResolution;
        float[,] heights = GenerateHeights(res);

        data.SetHeights(0, 0, heights);

        // Apply at least one terrain layer so it renders with a material
        ApplyTerrainLayer(data);

        // Detail resolution (for grass)
        int clampedDetailRes = Mathf.Clamp(detailResolution, 32, 4096);
        int resolutionPerPatch = 8; // smaller patches = better looking distribution
        data.SetDetailResolution(clampedDetailRes, resolutionPerPatch);
    }

    /// <summary>
    /// Generates a heightmap with gentle rolling hills using fractal noise.
    /// </summary>
    private float[,] GenerateHeights(int resolution)
    {
        float[,] heights = new float[resolution, resolution];

        if (noiseScale <= 0f)
            noiseScale = 0.001f;

        // Random offsets per octave to vary the noise pattern
        System.Random prng = new System.Random(seed);
        Vector2[] octaveOffsets = new Vector2[Mathf.Max(1, noiseOctaves)];
        for (int i = 0; i < octaveOffsets.Length; i++)
        {
            float offsetX = prng.Next(-100000, 100000);
            float offsetY = prng.Next(-100000, 100000);
            octaveOffsets[i] = new Vector2(offsetX, offsetY);
        }

        int size = resolution;
        float maxHeight = 0f;
        float minHeight = 1f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float amplitude = 1f;
                float frequency = 1f;
                float noiseHeight = 0f;

                for (int o = 0; o < octaveOffsets.Length; o++)
                {
                    float sampleX = (x / (float)size) / noiseScale * frequency + octaveOffsets[o].x;
                    float sampleY = (y / (float)size) / noiseScale * frequency + octaveOffsets[o].y;

                    float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2f - 1f;
                    noiseHeight += perlinValue * amplitude;

                    amplitude *= noisePersistence;
                    frequency *= noiseLacunarity;
                }

                // Normalize to 0..1 range
                maxHeight = Mathf.Max(maxHeight, noiseHeight);
                minHeight = Mathf.Min(minHeight, noiseHeight);
                heights[y, x] = noiseHeight;
            }
        }

        // Remap the noise from [minHeight, maxHeight] into [0, 1]
        float range = Mathf.Max(0.0001f, maxHeight - minHeight);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float normalized = (heights[y, x] - minHeight) / range;
                // Slight smoothing curve for more natural hills
                normalized = Mathf.SmoothStep(0f, 1f, normalized);
                // Scale by heightScale relative to terrain Y size
                heights[y, x] = normalized * (heightScale / Mathf.Max(1f, terrainSize.y));
            }
        }

        return heights;
    }

    /// <summary>
    /// Ensures there is at least one TerrainLayer so the terrain has a visible material.
    /// No external textures are assumed; the user can assign textures later in the editor.
    /// </summary>
    private void ApplyTerrainLayer(TerrainData data)
    {
        if (data == null)
            return;

        TerrainLayer[] layers = data.terrainLayers;

        if (layers == null || layers.Length == 0)
        {
            TerrainLayer layer = new TerrainLayer
            {
                tileSize = new Vector2(15f, 15f)
                // diffuseTexture left null intentionally to avoid assuming assets.
            };

#if UNITY_EDITOR
            // When running in the editor, TerrainLayers must be assets; but creating that
            // is beyond this script's scope. We simply assign the layer to the data.
#endif

            data.terrainLayers = new TerrainLayer[] { layer };
        }
    }

    // ----------------------------------------------------------------------
    // Grass Details
    // ----------------------------------------------------------------------

    /// <summary>
    /// Configures grass detail prototypes and populates the first detail layer.
    /// </summary>
    private void ApplyGrassDetails(Terrain terrain)
    {
        if (terrain == null || terrain.terrainData == null)
            return;

        TerrainData data = terrain.terrainData;

        // Ensure we have at least one DetailPrototype.
        DetailPrototype[] prototypes = data.detailPrototypes;
        if (prototypes == null || prototypes.Length == 0)
        {
            DetailPrototype grassPrototype = new DetailPrototype
            {
                usePrototypeMesh = false,
                prototypeTexture = null, // User can assign a grass texture in the inspector.
                healthyColor = grassHealthyColor,
                dryColor = grassDryColor,
                minWidth = grassPatchScale * 0.8f,
                maxWidth = grassPatchScale * 1.2f,
                minHeight = grassPatchScale * 0.8f,
                maxHeight = grassPatchScale * 1.4f,
                renderMode = DetailRenderMode.GrassBillboard,
                noiseSpread = 0.5f
            };

            prototypes = new[] { grassPrototype };
            data.detailPrototypes = prototypes;
        }
        else
        {
            // Update colors and scale of the first prototype to match current settings
            prototypes[0].healthyColor = grassHealthyColor;
            prototypes[0].dryColor = grassDryColor;
            prototypes[0].minWidth = grassPatchScale * 0.8f;
            prototypes[0].maxWidth = grassPatchScale * 1.2f;
            prototypes[0].minHeight = grassPatchScale * 0.8f;
            prototypes[0].maxHeight = grassPatchScale * 1.4f;
            data.detailPrototypes = prototypes;
        }

        int detailWidth = data.detailWidth;
        int detailHeight = data.detailHeight;

        if (detailWidth <= 0 || detailHeight <= 0)
            return;

        int[,] layer = GenerateGrassDetailLayer(terrain, detailWidth, detailHeight);
        data.SetDetailLayer(0, 0, 0, layer);
    }

    /// <summary>
    /// Generates a grass detail density map where flatter / lower areas are denser and
    /// steeper slopes are sparser.
    /// </summary>
    private int[,] GenerateGrassDetailLayer(Terrain terrain, int detailWidth, int detailHeight)
    {
        int[,] details = new int[detailWidth, detailHeight];

        TerrainData data = terrain.terrainData;
        System.Random prng = new System.Random(seed + 999);

        // Use a small noise to make grass patchy and natural
        float patchNoiseScale = 0.15f;

        for (int y = 0; y < detailHeight; y++)
        {
            for (int x = 0; x < detailWidth; x++)
            {
                float u = (x + 0.5f) / detailWidth;
                float v = (y + 0.5f) / detailHeight;

                float steepness01 = GetSteepness01(terrain, u, v);
                float flatness = 1f - steepness01; // 1 on flat ground, 0 on very steep

                // Grass more likely in flatter areas
                float baseDensity = grassDensity * flatness;

                // Additional patchiness noise
                float nx = u / patchNoiseScale;
                float ny = v / patchNoiseScale;
                float noise = Mathf.PerlinNoise(nx + seed * 0.123f, ny + seed * 0.321f);
                // Push noise toward patchy extremes
                noise = Mathf.Pow(noise, 1.5f);

                float finalDensity = baseDensity * noise;
                finalDensity = Mathf.Clamp01(finalDensity);

                // Decide presence using a random threshold for natural look
                double rand = prng.NextDouble();
                int count = (rand < finalDensity) ? 1 : 0;

                details[y, x] = count;
            }
        }

        return details;
    }

    /// <summary>
    /// Returns terrain steepness at normalized coordinates (u,v) mapped into [0,1],
    /// where 0 is flat and 1 is at or beyond grassMaxSteepness.
    /// </summary>
    private float GetSteepness01(Terrain terrain, float u, float v)
    {
        if (terrain == null || terrain.terrainData == null)
            return 0f;

        float angle = terrain.terrainData.GetSteepness(u, v);

        if (grassMaxSteepness <= 0f)
            return Mathf.Clamp01(angle / 90f); // fallback normalization

        return Mathf.Clamp01(angle / grassMaxSteepness);
    }
}