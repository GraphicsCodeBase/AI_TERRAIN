using UnityEngine;

public class CameraPositioner : MonoBehaviour
{
    public int terrainWidth = 100;
    public int terrainHeight = 100;
    public float heightAboveTerrain = 40f;
    public float forwardOffset = 30f;
    public float tiltAngle = 45f;

    void Start()
    {
        // Center of terrain
        Vector3 center = new Vector3(terrainWidth / 2f, 0f, terrainHeight / 2f);

        // Place camera above and slightly in front of the center
        Vector3 cameraPos = center + new Vector3(0f, heightAboveTerrain, forwardOffset);  // Positive Z = in front of terrain

        transform.position = cameraPos;

        // Look at the center of the terrain (downwards)
        transform.LookAt(center);
    }
}
