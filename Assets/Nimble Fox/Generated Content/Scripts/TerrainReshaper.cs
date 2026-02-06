using UnityEngine;

public class TerrainReshaper : MonoBehaviour
{
    [SerializeField] private Terrain targetTerrain;

    [Header("Flattening & Base Shape")]
    [SerializeField] [Range(0f, 1f)] private float flattenStrength = 0.85f;
    [SerializeField] private float baseHeight01 = 0.02f;
    [SerializeField] private float gentleNoiseAmplitude01 = 0.01f;
    [SerializeField] private float gentleNoiseScale = 2.5f;
    [SerializeField] private int smoothingIterations = 3;

    [Header("Cliff Features")]
    [SerializeField] private int cliffCount = 3;
    [SerializeField] [Range(0f, 1f)] private float cliffCenterMin01 = 0.1f;
    [SerializeField] [Range(0f, 1f)] private float cliffCenterMax01 = 0.9f;
    [SerializeField] [Range(0f, 1f)] private float cliffRadius01 = 0.08f;
    [SerializeField] private float cliffHeightDelta01 = 0.08f;
    [SerializeField] [Range(0f, 1f)] private float cliffFalloff01 = 0.35f;

    [Header("Randomness & Workflow")]
    [SerializeField] private int randomSeed = 12345;
    [SerializeField] private bool autoApplyInEditMode = true;

    // Called when script is loaded or a value is changed in the Inspector
    private void OnValidate()
    {
        // Clamp manually where attributes are not present or to enforce relationships
        baseHeight01 = Clamp01(baseHeight01);
        gentleNoiseAmplitude01 = Mathf.Max(0f, gentleNoiseAmplitude01);
        cliffHeightDelta01 = Mathf.Max(0f, cliffHeightDelta01);

        // Ensure center min <= max
        if (cliffCenterMax01 < cliffCenterMin01)
        {
            float tmp = cliffCenterMin01;
            cliffCenterMin01 = cliffCenterMax01;
            cliffCenterMax01 = tmp;
        }

        // Keep radius and falloff in [0,1]
        cliffRadius01 = Clamp01(cliffRadius01);
        cliffFalloff01 = Clamp01(cliffFalloff01);

        if (autoApplyInEditMode && !Application.isPlaying && targetTerrain != null)
        {
            ApplyToTerrain(targetTerrain);
        }
    }

    // Set sensible defaults when the component is first added
    private void Reset()
    {
        targetTerrain = GetComponent<Terrain>();

        flattenStrength = 0.9f;
        baseHeight01 = 0.015f;
        gentleNoiseAmplitude01 = 0.008f;
        gentleNoiseScale = 2.0f;
        smoothingIterations = 4;

        cliffCount = 3;
        cliffCenterMin01 = 0.15f;
        cliffCenterMax01 = 0.85f;
        cliffRadius01 = 0.08f;
        cliffHeightDelta01 = 0.09f;
        cliffFalloff01 = 0.3f;

        randomSeed = 12345;
        autoApplyInEditMode = true;
    }

    /// <summary>
    /// Public entry point to reshape the terrain.
    /// </summary>
    public void Apply()
    {
        if (targetTerrain == null)
        {
            Debug.LogWarning($"{nameof(TerrainReshaper)}: No targetTerrain assigned.");
            return;
        }

        ApplyToTerrain(targetTerrain);
    }

    /// <summary>
    /// Full pipeline: flatten & add gentle noise, smooth, then add cliffs.
    /// </summary>
    private void ApplyToTerrain(Terrain terrain)
    {
        if (terrain == null || terrain.terrainData == null)
        {
            Debug.LogWarning($"{nameof(TerrainReshaper)}: Invalid terrain or terrain data.");
            return;
        }

        TerrainData data = terrain.terrainData;
        int res = data.heightmapResolution;

        float[,] heights = data.GetHeights(0, 0, res, res);

        System.Random rng = new System.Random(randomSeed);

        heights = FlattenAndAddGentleNoise(heights, res, rng);
        heights = SmoothHeights(heights, smoothingIterations);
        heights = AddCliffs(heights, res, rng);

        data.SetHeights(0, 0, heights);
    }

    /// <summary>
    /// Strongly flattens existing terrain toward a base height and overlays gentle Perlin noise.
    /// Produces broad, traversable fields with subtle rolling variation.
    /// </summary>
    private float[,] FlattenAndAddGentleNoise(float[,] heights, int heightmapResolution, System.Random rng)
    {
        int res = heightmapResolution;

        float noiseScale = Mathf.Max(0.0001f, gentleNoiseScale);
        float amplitude = gentleNoiseAmplitude01;

        for (int z = 0; z < res; z++)
        {
            float nz = (float)z / (res - 1) * noiseScale;

            for (int x = 0; x < res; x++)
            {
                float nx = (float)x / (res - 1) * noiseScale;

                float original = heights[z, x];

                // Flatten towards base height
                float flattened = Mathf.Lerp(original, baseHeight01, flattenStrength);

                // Gentle bi-directional Perlin noise (-1..1)
                float noise = Mathf.PerlinNoise(nx, nz) * 2f - 1f;
                float offset = noise * amplitude;

                float combined = flattened + offset;
                heights[z, x] = Clamp01(combined);
            }
        }

        return heights;
    }

    /// <summary>
    /// Simple iterative box-blur smoothing to further reduce harsh slopes.
    /// </summary>
    private float[,] SmoothHeights(float[,] heights, int iterations)
    {
        int resZ = heights.GetLength(0);
        int resX = heights.GetLength(1);

        float[,] src = heights;
        float[,] dst = new float[resZ, resX];

        for (int iter = 0; iter < Mathf.Max(0, iterations); iter++)
        {
            for (int z = 0; z < resZ; z++)
            {
                int z0 = Mathf.Max(z - 1, 0);
                int z1 = z;
                int z2 = Mathf.Min(z + 1, resZ - 1);

                for (int x = 0; x < resX; x++)
                {
                    int x0 = Mathf.Max(x - 1, 0);
                    int x1 = x;
                    int x2 = Mathf.Min(x + 1, resX - 1);

                    float sum =
                        src[z0, x0] + src[z0, x1] + src[z0, x2] +
                        src[z1, x0] + src[z1, x1] + src[z1, x2] +
                        src[z2, x0] + src[z2, x1] + src[z2, x2];

                    dst[z, x] = sum / 9f;
                }
            }

            // Swap references for next iteration
            var tmp = src;
            src = dst;
            dst = tmp;
        }

        // If no iterations, src is the original heights
        return src;
    }

    /// <summary>
    /// Adds a small number of localized cliff formations by raising plateaus with sharp surrounding slopes.
    /// </summary>
    private float[,] AddCliffs(float[,] heights, int heightmapResolution, System.Random rng)
    {
        int res = heightmapResolution;
        int count = Mathf.Max(0, cliffCount);

        if (count == 0 || cliffRadius01 <= 0f || cliffHeightDelta01 <= 0f)
            return heights;

        float minCenter = Mathf.Clamp01(cliffCenterMin01);
        float maxCenter = Mathf.Clamp01(cliffCenterMax01);
        float radius01 = Mathf.Max(0.0001f, cliffRadius01);
        float falloffInner = Mathf.Clamp01(cliffFalloff01);

        // Pre-generate cliff centers
        Vector2[] centers = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            float cx = Mathf.Lerp(minCenter, maxCenter, (float)rng.NextDouble());
            float cz = Mathf.Lerp(minCenter, maxCenter, (float)rng.NextDouble());
            centers[i] = new Vector2(cx, cz);
        }

        for (int z = 0; z < res; z++)
        {
            float vz = (float)z / (res - 1);

            for (int x = 0; x < res; x++)
            {
                float vx = (float)x / (res - 1);

                float heightOffset = 0f;

                // Accumulate contribution from nearby cliffs
                for (int i = 0; i < count; i++)
                {
                    Vector2 c = centers[i];
                    float dx = vx - c.x;
                    float dz = vz - c.y;
                    float dist = Mathf.Sqrt(dx * dx + dz * dz);

                    float distNorm = dist / radius01;
                    if (distNorm >= 1f)
                        continue;

                    // Inner plateau region (flat top)
                    float t;
                    if (distNorm <= falloffInner)
                    {
                        t = 1f;
                    }
                    else
                    {
                        // Smooth step from 1 at inner radius to 0 at outer radius
                        float raw = (distNorm - falloffInner) / Mathf.Max(0.0001f, 1f - falloffInner);
                        t = 1f - SmoothStep01(raw);
                    }

                    heightOffset += cliffHeightDelta01 * t;
                }

                if (heightOffset > 0f)
                {
                    float newH = heights[z, x] + heightOffset;
                    heights[z, x] = Clamp01(newH);
                }
            }
        }

        return heights;
    }

    private static float SmoothStep01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private static float Clamp01(float v)
    {
        return Mathf.Clamp01(v);
    }
}