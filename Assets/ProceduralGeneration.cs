using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // Required for UI components
using System.Linq;  //Vector2.Distance requires this for Zip extension method
using TMPro; // If you're using TextMeshPro


[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ProceduralGeneration : MonoBehaviour
{
    // Weights for each biome type
    private float perlinScaleMultiplier = 1f;
    public float[] biomeFrequencies = new float[4]; // 0 = Desert, 1 = Forest, 2 = Mountain, 3 = Ocean
    float desertMountainMinDist = 15f; // tweak as needed (prevents desert and mountain biomes from being too close)

    [Header("Erosion Settings")]
    public float minTerrainHeight = 0.5f; // The lowest y value terrain can erode to

    [Header("Height Constraints")]
    public float maxTerrainHeight = 20f; // The highest y value terrain can reach

    [Header("Season Settings")]
    public Slider seasonSlider;
    public Toggle enableSeasonsToggle;
    public TMP_Text seasonLabelText;

    private bool autoSeasonEnabled = false;
    private float seasonProgress = 0f; // 0 to 365
    private float seasonSpeed = 30f; // how fast to progress each frame

    // Terrain dimensions and scale
    public int width = 1000;
    public int height = 1000;
    public float scale = 10f;

    //UI Sliders for controlling parameters
    public Slider perlinScaleSlider;
    public Slider desertFreqSlider;
    public Slider forestFreqSlider;
    public Slider mountainFreqSlider;
    public Slider oceanFreqSlider;

    private Mesh mesh;
    private Vector3[] vertices;
    private Color[] colorLayer;
    private int[] triangles;

    private List<Vector2> voronoiCenters; // Centers for Voronoi cells
    private List<BiomeType> voronoiBiomes; // Biome types for each Voronoi center

    void Start()
    {
        InitializeVoronoiCenters(); // Initialize with default weights (can be overwritten below)

        // Set initial slider values
        if (perlinScaleSlider != null)
        {
            perlinScaleSlider.value = perlinScaleSlider.maxValue; // Start at max value
            perlinScaleMultiplier = perlinScaleSlider.value;
            perlinScaleSlider.onValueChanged.AddListener(OnPerlinScaleChanged);
        }

        if (desertFreqSlider != null)
        {
            desertFreqSlider.value = Random.Range(desertFreqSlider.minValue, desertFreqSlider.maxValue);
            biomeFrequencies[0] = desertFreqSlider.value;
            desertFreqSlider.onValueChanged.AddListener((v) => OnBiomeFreqChanged(0, v));
        }

        if (forestFreqSlider != null)
        {
            forestFreqSlider.value = Random.Range(forestFreqSlider.minValue, forestFreqSlider.maxValue);
            biomeFrequencies[1] = forestFreqSlider.value;
            forestFreqSlider.onValueChanged.AddListener((v) => OnBiomeFreqChanged(1, v));
        }

        if (mountainFreqSlider != null)
        {
            mountainFreqSlider.value = Random.Range(mountainFreqSlider.minValue, mountainFreqSlider.maxValue);
            biomeFrequencies[2] = mountainFreqSlider.value;
            mountainFreqSlider.onValueChanged.AddListener((v) => OnBiomeFreqChanged(2, v));
        }

        if (oceanFreqSlider != null)
        {
            oceanFreqSlider.value = Random.Range(oceanFreqSlider.minValue, oceanFreqSlider.maxValue);
            biomeFrequencies[3] = oceanFreqSlider.value;
            oceanFreqSlider.onValueChanged.AddListener((v) => OnBiomeFreqChanged(3, v));
        }

        if (enableSeasonsToggle != null)
        {
            autoSeasonEnabled = enableSeasonsToggle.isOn;
            enableSeasonsToggle.onValueChanged.AddListener((isOn) => autoSeasonEnabled = isOn);
        }

        if (seasonSlider != null)
        {
            seasonSlider.maxValue = 365f;
            seasonSlider.onValueChanged.AddListener((v) => {
                if (!autoSeasonEnabled)
                {
                    seasonProgress = v;
                    ApplySeasonalColors();
                }
            });
        }

        // Generate Voronoi based on randomized weights
        InitializeVoronoiCenters();
        GenerateTerrain();

        // Set initial season value and apply colors
        seasonProgress = 0f;
        if (seasonSlider != null)
            seasonSlider.SetValueWithoutNotify(seasonProgress);

        ApplySeasonalColors();
    }

    void Update()
    {
        if (autoSeasonEnabled)
        {
            seasonProgress = (seasonProgress + Time.deltaTime * seasonSpeed) % 365f;

            // Only update slider if auto is on
            if (seasonSlider != null)
                seasonSlider.SetValueWithoutNotify(seasonProgress);

            ApplySeasonalColors();
        }
    }



    void OnPerlinScaleChanged(float value)
    {
        perlinScaleMultiplier = value;
        InitializeVoronoiCenters();
        GenerateTerrain();
    }

    void OnBiomeFreqChanged(int biomeIndex, float value)
    {
        biomeFrequencies[biomeIndex] = value;
        InitializeVoronoiCenters();
        GenerateTerrain();
    }

    Vector2 GetValidPositionForBiome(BiomeType type, float minDist)
    {
        Vector2 pos;
        int tries = 0;
        do
        {
            pos = new Vector2(Random.Range(0, width), Random.Range(0, height));
            tries++;

            if (type == BiomeType.Desert || type == BiomeType.Mountain || type == BiomeType.Ocean)
            {
                foreach (var (centerPos, centerType) in voronoiCenters.Zip(voronoiBiomes, (p, t) => (p, t)))
                {
                    bool clash =
                        (type == BiomeType.Desert && centerType == BiomeType.Mountain) ||
                        (type == BiomeType.Mountain && centerType == BiomeType.Desert) ||
                        (type == BiomeType.Ocean && centerType == BiomeType.Desert) ||
                        (type == BiomeType.Desert && centerType == BiomeType.Ocean);

                    if (clash && Vector2.Distance(pos, centerPos) < minDist)
                    {
                        pos = Vector2.negativeInfinity;
                        break;
                    }
                }
            }
        }
        while (pos == Vector2.negativeInfinity && tries < 100);

        return pos;
    }

    void InitializeVoronoiCenters()
    {
        voronoiCenters = new List<Vector2>();
        voronoiBiomes = new List<BiomeType>();

        int totalCenters = 40; // Increased to account for ocean
        float totalWeight = biomeFrequencies.Sum();

        int numDesert = Mathf.RoundToInt(totalCenters * (biomeFrequencies[0] / totalWeight));
        int numForest = Mathf.RoundToInt(totalCenters * (biomeFrequencies[1] / totalWeight));
        int numMountain = Mathf.RoundToInt(totalCenters * (biomeFrequencies[2] / totalWeight));
        int numOcean = totalCenters - numDesert - numForest - numMountain;

        for (int i = 0; i < numDesert; i++)
        {
            Vector2 pos = GetValidPositionForBiome(BiomeType.Desert, desertMountainMinDist);
            AddVoronoiCenter(BiomeType.Desert, pos);
        }

        for (int i = 0; i < numMountain; i++)
        {
            Vector2 pos = GetValidPositionForBiome(BiomeType.Mountain, desertMountainMinDist);
            AddVoronoiCenter(BiomeType.Mountain, pos);
        }

        for (int i = 0; i < numForest; i++)
        {
            Vector2 pos = new Vector2(Random.Range(0, width), Random.Range(0, height));
            AddVoronoiCenter(BiomeType.Forest, pos);
        }

        for (int i = 0; i < numOcean; i++)
        {
            Vector2 pos = GetValidPositionForBiome(BiomeType.Ocean, desertMountainMinDist);
            AddVoronoiCenter(BiomeType.Ocean, pos);
        }
    }

    void AddVoronoiCenter(BiomeType type, Vector2 pos)
    {
        voronoiCenters.Add(pos);
        voronoiBiomes.Add(type);
    }

    public void GenerateTerrain()
    {
        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;

        vertices = new Vector3[(width + 1) * (height + 1)];
        colorLayer = new Color[(width + 1) * (height + 1)];
        triangles = new int[width * height * 6];

        for (int z = 0, i = 0; z <= height; z++)
        {
            for (int x = 0; x <= width; x++, i++)
            {
                float perlin = Mathf.PerlinNoise(x * 0.1f, z * 0.1f);
                BiomeType biome = GetBiomeForPoint(x, z);

                float heightModifier = 1f;
                Color color = Color.white;

                switch (biome)
                {
                    case BiomeType.Desert:
                        heightModifier = 0.3f/* * biomeFrequencies[0]*/; // Desert
                        color = Color.yellow;
                        break;
                    case BiomeType.Forest:
                        heightModifier = 0.6f /** biomeFrequencies[1]*/; // Forest
                        color = Color.green;
                        break;
                    case BiomeType.Mountain:
                        heightModifier = 1.5f/* * biomeFrequencies[2]*/; // Mountain
                        color = Color.gray;
                        break;
                    case BiomeType.Ocean:
                        heightModifier = 0.1f/* * biomeFrequencies[3]*/; // Ocean
                        color = Color.blue;
                        break;
                }

                float y = perlin * scale * heightModifier * perlinScaleMultiplier;
                if (biome == BiomeType.Ocean)
                    y = Mathf.Min(y, 1.5f); // Cap ocean height
                vertices[i] = new Vector3(x - width / 2f, y, z - height / 2f);
                colorLayer[i] = color;
            }
        }

        int vert = 0;
        int tris = 0;
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                triangles[tris + 0] = vert + 0;
                triangles[tris + 1] = vert + width + 1;
                triangles[tris + 2] = vert + 1;
                triangles[tris + 3] = vert + 1;
                triangles[tris + 4] = vert + width + 1;
                triangles[tris + 5] = vert + width + 2;

                vert++;
                tris += 6;
            }
            vert++;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.colors = colorLayer;
        mesh.RecalculateNormals();
    }

    enum BiomeType { Desert, Forest, Mountain, Ocean }

    BiomeType GetBiomeForPoint(int x, int z)
    {
        float minDist = float.MaxValue;
        int closestCenterIndex = 0;

        for (int i = 0; i < voronoiCenters.Count; i++)
        {
            float dist = Vector2.Distance(new Vector2(x, z), voronoiCenters[i]);
            if (dist < minDist)
            {
                minDist = dist;
                closestCenterIndex = i;
            }
        }

        return voronoiBiomes[closestCenterIndex]; // Use pre-assigned biome
    }

    IEnumerator RainfallErosionCoroutine(int steps)
    {
        int dropletsPerFrame = 150;

        for (int step = 0; step < steps; step++)
        {
            for (int i = 0; i < dropletsPerFrame; i++)
            {
                int dropX = Random.Range(1, width - 1);
                int dropZ = Random.Range(1, height - 1);
                SimulateDroplet(dropX, dropZ, 30);
            }

            mesh.vertices = vertices;
            mesh.colors = colorLayer;
            mesh.RecalculateNormals();
            yield return null;
        }
    }

    public void StartErosionSimulation()
    {
        StartCoroutine(RainfallErosionCoroutine(100)); // Run for 100 frames
        SmoothTerrain(); // Smooth terrain after erosion
    }

    void SimulateDroplet(int startX, int startZ, int maxSteps)
    {
        float posX = startX;
        float posZ = startZ;
        float dirX = 0, dirZ = 0;
        float speed = 1f;
        float water = 1f;
        float sediment = 0f;

        for (int step = 0; step < maxSteps; step++)
        {
            int mapX = (int)posX;
            int mapZ = (int)posZ;

            if (mapX < 1 || mapX >= width - 1 || mapZ < 1 || mapZ >= height - 1)
                break;

            int index = mapZ * (width + 1) + mapX;

            // Compute height and gradient from neighboring heights
            Vector3 hL = vertices[index - 1];
            Vector3 hR = vertices[index + 1];
            Vector3 hD = vertices[index + (width + 1)];
            Vector3 hU = vertices[index - (width + 1)];
            float currentHeight = vertices[index].y;
            float gradientX = (hR.y - hL.y) * 0.5f;
            float gradientZ = (hD.y - hU.y) * 0.5f;

            Vector2 grad = new Vector2(gradientX, gradientZ);
            Vector2 norm = grad.normalized;

            // Update velocity
            dirX = dirX * 0.9f - norm.x * 0.1f;
            dirZ = dirZ * 0.9f - norm.y * 0.1f;
            Vector2 dir = new Vector2(dirX, dirZ);
            float len = dir.magnitude;

            if (len == 0)
                break;

            dirX /= len;
            dirZ /= len;

            posX += dirX;
            posZ += dirZ;

            float newHeight = BilinearHeight(posX, posZ);
            float deltaHeight = currentHeight - newHeight;

            // Sediment capacity
            float sedimentCapacity = Mathf.Max(-deltaHeight * speed * water * 4f, 0.01f);

            if (sediment > sedimentCapacity || deltaHeight > 0)
            {
                // Deposit excess sediment
                float amountToDeposit = deltaHeight > 0
                    ? Mathf.Min(deltaHeight, sediment)
                    : (sediment - sedimentCapacity) * 0.001f;

                sediment -= amountToDeposit;
                DepositBilinear(posX, posZ, amountToDeposit);
            }
            else
            {
                // Erode
                float amountToErode = (sedimentCapacity - sediment) * 0.001f;
                ErodeAround(mapX, mapZ, amountToErode);
                sediment += amountToErode;
            }

            speed = Mathf.Sqrt(speed * speed + deltaHeight * 0.5f);
            water *= 0.9f;

            if (water < 0.01f)
                break;
        }
    }

    //Helper method to deposit sediment at a point using bilinear interpolation
    float BilinearHeight(float fx, float fz)
    {
        int x = Mathf.FloorToInt(fx);
        int z = Mathf.FloorToInt(fz);
        float tx = fx - x;
        float tz = fz - z;

        int idx = z * (width + 1) + x;
        float h00 = vertices[idx].y;
        float h10 = vertices[idx + 1].y;
        float h01 = vertices[idx + (width + 1)].y;
        float h11 = vertices[idx + (width + 1) + 1].y;

        return Mathf.Lerp(
            Mathf.Lerp(h00, h10, tx),
            Mathf.Lerp(h01, h11, tx),
            tz
        );
    }

    //Helper method to erode terrain around a point
    void DepositBilinear(float fx, float fz, float amount)
    {
        int centerX = Mathf.FloorToInt(fx);
        int centerZ = Mathf.FloorToInt(fz);
        int radius = 2; // spread radius

        for (int dz = -radius; dz <= radius; dz++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                int x = centerX + dx;
                int z = centerZ + dz;

                if (x < 0 || x > width || z < 0 || z > height)
                    continue;

                int index = z * (width + 1) + x;

                // Distance-based weight (simple linear falloff)
                float dist = Mathf.Sqrt(dx * dx + dz * dz);
                if (dist > radius) continue;
                float weight = 1f - dist / radius;

                vertices[index].y = Mathf.Min(maxTerrainHeight, vertices[index].y + amount * weight * 0.2f);
            }
        }
    }

    // Helper method to erode terrain around a point
    void ErodeAround(int cx, int cz, float amount)
    {
        for (int dz = -1; dz <= 1; dz++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int x = cx + dx;
                int z = cz + dz;
                if (x < 0 || x > width || z < 0 || z > height)
                    continue;

                int index = z * (width + 1) + x;
                vertices[index].y = Mathf.Max(minTerrainHeight, vertices[index].y - amount * 0.25f);
            }
        }
    }

    void SmoothTerrain()
    {
        Vector3[] smoothedVertices = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
            smoothedVertices[i] = vertices[i];

        for (int z = 1; z < height; z++)
        {
            for (int x = 1; x < width; x++)
            {
                int i = z * (width + 1) + x;
                float avgHeight = (
                    vertices[i].y +
                    vertices[i - 1].y +
                    vertices[i + 1].y +
                    vertices[i - (width + 1)].y +
                    vertices[i + (width + 1)].y
                ) / 5f;

                smoothedVertices[i].y = Mathf.Lerp(vertices[i].y, avgHeight, 0.5f);
            }
        }
        vertices = smoothedVertices;
    }

    void ApplySeasonalColors()
    {
        float t = seasonProgress / 365f; // normalized season (0 to 1)

        for (int i = 0; i < vertices.Length; i++)
        {
            BiomeType biome = GetBiomeForPoint((int)(vertices[i].x + width / 2f), (int)(vertices[i].z + height / 2f));

            Color baseColor = Color.white;

            switch (biome)
            {
                case BiomeType.Forest:
                    baseColor = GetForestSeasonColor(t);
                    break;
                case BiomeType.Mountain:
                    baseColor = GetMountainSeasonColor(t);
                    break;
                case BiomeType.Ocean:
                    baseColor = GetOceanSeasonColor(t);
                    break;
                case BiomeType.Desert:
                    baseColor = Color.yellow;
                    break;
            }

            colorLayer[i] = baseColor;
        }

        mesh.colors = colorLayer;

        // Determine season label based on seasonProgress
        string seasonName = "";

        if (t < 0.25f)
            seasonName = "Spring";
        else if (t < 0.5f)
            seasonName = "Summer";
        else if (t < 0.75f)
            seasonName = "Autumn";
        else
            seasonName = "Winter";

        if (seasonLabelText != null)
            seasonLabelText.text = $"Season: {seasonName}";

    }

    Color GetForestSeasonColor(float t)
    {
        Color pink = new Color(1f, 0.71f, 0.76f);   // Spring
        Color green = Color.green;                 // Summer
        Color orange = new Color(1f, 0.65f, 0f);    // Autumn
        Color gray = Color.gray;                   // Winter

        if (t < 0.25f)
            return Color.Lerp(gray, pink, t * 4f);              // Winter to Spring (loop wrap)
        else if (t < 0.5f)
            return Color.Lerp(pink, green, (t - 0.25f) * 4f);    // Spring to Summer
        else if (t < 0.75f)
            return Color.Lerp(green, orange, (t - 0.5f) * 4f);   // Summer to Autumn
        else
            return Color.Lerp(orange, gray, (t - 0.75f) * 4f);   // Autumn to Winter
    }

    Color GetMountainSeasonColor(float t)
    {
        Color green = Color.green;
        Color white = Color.white;

        if (t < 0.75f)
            return Color.Lerp(white, green, t * (4f / 3f)); // Winter to green until Autumn
        else
            return Color.Lerp(green, white, (t - 0.75f) * 4f); // Autumn to Winter snow
    }

    Color GetOceanSeasonColor(float t)
    {
        Color blue = new Color(0f, 0.4f, 1f);
        Color lightBlue = new Color(0.7f, 0.9f, 1f); // icy color

        if (t < 0.75f)
            return blue; // Spring to Autumn
        else
            return Color.Lerp(blue, lightBlue, (t - 0.75f) * 4f); // Gradual freeze to Winter
    }

}
