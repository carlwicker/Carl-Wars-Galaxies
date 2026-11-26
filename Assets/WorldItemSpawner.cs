using UnityEngine;

public sealed class WorldItemSpawner : MonoBehaviour
{
    private static readonly Vector2[] Positions =
    {
        new Vector2(-3f, 4f), new Vector2(3f, 4f),
        new Vector2(-11f, -4f), new Vector2(11f, -4f),
        new Vector2(-15f, 12f), new Vector2(15f, 12f),
        new Vector2(-16f, 25f), new Vector2(16f, 25f),
        new Vector2(-10f, 30f), new Vector2(10f, 30f),
        new Vector2(-4f, -12f), new Vector2(4f, -12f)
    };

    private void Start()
    {
        for (int i = 0; i < Positions.Length; i++)
        {
            Vector3 origin = new Vector3(Positions[i].x, 100f, Positions[i].y);
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 200f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            bool foundGround = false;
            Vector3 ground = Vector3.zero;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.GetComponent<UnevenGround>() != null)
                {
                    ground = hit.point;
                    foundGround = true;
                    break;
                }
            }
            if (!foundGround)
            {
                Debug.LogError($"Could not find ground for vehicle part {i}.", this);
                continue;
            }

            bool engine = i % 2 == 0;
            string itemId = engine ? "engine_cube" : "power_cube";
            string itemName = engine ? "Engine Cube" : "Power Cube";
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = $"Pickup - {itemName}";
            cube.transform.position = ground + Vector3.up * 0.38f;
            cube.transform.localScale = Vector3.one * 0.76f;
            cube.GetComponent<Renderer>().material.color = engine
                ? new Color(1f, 0.45f, 0.12f)
                : new Color(0.2f, 0.8f, 1f);
            cube.AddComponent<WorldPickup>().Initialize($"vehicle-part-{i:00}", itemId, itemName, 1);
        }
    }
}
