using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter), typeof(MeshCollider))]
public sealed class UnevenGround : MonoBehaviour
{
    [SerializeField, Range(16, 256)] private int gridResolution = 256;
    [SerializeField] private float flatAreaRadius = 0.36f;
    [SerializeField] private float transitionWidth = 0.28f;
    [SerializeField] private float heightAmplitude = 0.65f;
    [SerializeField] private float noiseScale = 1.75f;
    [SerializeField] private float noiseOffset = 3.94f;

    private Mesh generatedMesh;

    private void Awake()
    {
        GenerateGroundMesh();
    }

    private void OnDestroy()
    {
        if (generatedMesh == null) return;

        if (Application.isPlaying)
        {
            Destroy(generatedMesh);
        }
        else
        {
            DestroyImmediate(generatedMesh);
        }
    }

    private void GenerateGroundMesh()
    {
        int resolution = Mathf.Clamp(gridResolution, 16, 256);
        int verticesPerSide = resolution + 1;
        int vertexCount = verticesPerSide * verticesPerSide;
        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount];
        int[] triangles = new int[resolution * resolution * 6];

        for (int z = 0; z < verticesPerSide; z++)
        {
            for (int x = 0; x < verticesPerSide; x++)
            {
                int index = z * verticesPerSide + x;
                float localX = -5f + 10f * x / resolution;
                float localZ = -5f + 10f * z / resolution;
                float radius = new Vector2(localX, localZ).magnitude;
                float transition = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(flatAreaRadius, flatAreaRadius + transitionWidth, radius));
                float broadNoise = Mathf.PerlinNoise(
                    (localX + noiseOffset) * noiseScale,
                    (localZ + noiseOffset) * noiseScale);
                float detailNoise = Mathf.PerlinNoise(
                    (localX - noiseOffset) * noiseScale * 2f,
                    (localZ + noiseOffset) * noiseScale * 2f);
                float noise = broadNoise * 0.75f + detailNoise * 0.25f;

                vertices[index] = new Vector3(
                    localX,
                    (noise - 0.5f) * 2f * heightAmplitude * transition,
                    localZ);
                uvs[index] = new Vector2((float)x / resolution, (float)z / resolution);
            }
        }

        int triangleIndex = 0;
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int bottomLeft = z * verticesPerSide + x;
                int topLeft = bottomLeft + verticesPerSide;
                int bottomRight = bottomLeft + 1;
                int topRight = topLeft + 1;

                triangles[triangleIndex++] = bottomLeft;
                triangles[triangleIndex++] = topLeft;
                triangles[triangleIndex++] = bottomRight;
                triangles[triangleIndex++] = bottomRight;
                triangles[triangleIndex++] = topLeft;
                triangles[triangleIndex++] = topRight;
            }
        }

        generatedMesh = new Mesh { name = "Generated Uneven Ground" };
        if (vertexCount > 65535)
        {
            generatedMesh.indexFormat = IndexFormat.UInt32;
        }

        generatedMesh.vertices = vertices;
        generatedMesh.uv = uvs;
        generatedMesh.triangles = triangles;
        generatedMesh.RecalculateNormals();
        generatedMesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = generatedMesh;
        MeshCollider groundCollider = GetComponent<MeshCollider>();
        groundCollider.sharedMesh = null;
        groundCollider.sharedMesh = generatedMesh;
    }
}
