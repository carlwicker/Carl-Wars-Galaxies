using UnityEngine;

public sealed class EngineCubeSpawner : MonoBehaviour
{
    private void Start()
    {
        // Engine cubes disabled for now
        // SpawnEngineCubes();
    }

    private void SpawnEngineCubes()
    {
        // Spawn multiple engine cubes at different positions
        Vector3[] spawnPositions = new Vector3[]
        {
            new Vector3(5f, 1f, 0f),
            new Vector3(-5f, 1f, 0f),
            new Vector3(0f, 1f, 5f),
            new Vector3(0f, 1f, -5f),
            new Vector3(10f, 1f, 0f),
            new Vector3(-10f, 1f, 0f),
            new Vector3(0f, 1f, 10f),
            new Vector3(0f, 1f, -10f)
        };

        for (int i = 0; i < spawnPositions.Length; i++)
        {
            CreateEngineCube(spawnPositions[i], i);
        }
    }

    private void CreateEngineCube(Vector3 position, int index)
    {
        // Create a cube
        GameObject cubeObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cubeObject.name = "Engine Cube";
        cubeObject.transform.position = position;
        cubeObject.transform.localScale = new Vector3(1f, 1f, 1f);

        // Remove the default collider
        Collider defaultCollider = cubeObject.GetComponent<Collider>();
        if (defaultCollider != null)
        {
            Destroy(defaultCollider);
        }

        // Add both trigger and non-trigger colliders
        // Trigger collider for detection
        BoxCollider triggerCollider = cubeObject.AddComponent<BoxCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.size = Vector3.one;

        // Non-trigger collider for interaction
        BoxCollider interactionCollider = cubeObject.AddComponent<BoxCollider>();
        interactionCollider.isTrigger = false;
        interactionCollider.size = Vector3.one;

        // Add Rigidbody to make it properly interactable
        Rigidbody rb = cubeObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // Set material color to indicate it's an engine (orange)
        Renderer renderer = cubeObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            
            if (shader != null)
            {
                Material engineMaterial = new Material(shader);
                engineMaterial.color = new Color(1f, 0.65f, 0f, 1f); // Bright orange color
                renderer.sharedMaterial = engineMaterial;
            }
        }

        // Add WorldPickup component
        WorldPickup pickup = cubeObject.AddComponent<WorldPickup>();
        pickup.Initialize($"engine{index}", "engine_cube", "Engine Cube", 1);

        Debug.Log($"Spawned Engine Cube at {position}");
    }
}

