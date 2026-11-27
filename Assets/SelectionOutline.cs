using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class SelectionOutline : MonoBehaviour
{
    private readonly List<GameObject> outlineObjects = new List<GameObject>();
    private Material outlineMaterial;
    private Transform selectedTarget;

    public Transform Target => selectedTarget;

    public void SetTarget(Transform target)
    {
        if (selectedTarget == target) return;

        ClearOutlineObjects();
        selectedTarget = target;
        if (selectedTarget == null) return;

        if (outlineMaterial == null)
        {
            Material template = Resources.Load<Material>("SelectionOutline");
            if (template == null)
            {
                Debug.LogError("Could not load the SelectionOutline material from Resources.", this);
                selectedTarget = null;
                return;
            }

            outlineMaterial = new Material(template)
            {
                name = "Runtime Selection Outline"
            };
        }

        MeshRenderer[] renderers = selectedTarget.GetComponentsInChildren<MeshRenderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer sourceRenderer = renderers[i];
            MeshFilter sourceFilter = sourceRenderer.GetComponent<MeshFilter>();
            if (!sourceRenderer.enabled || sourceFilter == null || sourceFilter.sharedMesh == null
                || sourceRenderer.GetComponent<TMP_Text>() != null)
            {
                continue;
            }

            GameObject outlineObject = new GameObject("Selection Outline");
            outlineObject.layer = sourceRenderer.gameObject.layer;
            outlineObject.transform.SetParent(sourceRenderer.transform, false);
            MeshFilter outlineFilter = outlineObject.AddComponent<MeshFilter>();
            outlineFilter.sharedMesh = sourceFilter.sharedMesh;
            MeshRenderer outlineRenderer = outlineObject.AddComponent<MeshRenderer>();
            outlineRenderer.sharedMaterial = outlineMaterial;
            outlineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            outlineRenderer.receiveShadows = false;
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            properties.SetVector("_BoundsCenter", sourceFilter.sharedMesh.bounds.center);
            outlineRenderer.SetPropertyBlock(properties);
            outlineObjects.Add(outlineObject);
        }
    }

    private void LateUpdate()
    {
        if (outlineObjects.Count > 0 && (selectedTarget == null || !selectedTarget.gameObject.activeInHierarchy))
        {
            ClearOutlineObjects();
            selectedTarget = null;
        }
    }

    private void OnDisable()
    {
        ClearOutlineObjects();
        selectedTarget = null;
    }

    private void OnDestroy()
    {
        ClearOutlineObjects();
        if (outlineMaterial != null) Destroy(outlineMaterial);
    }

    private void ClearOutlineObjects()
    {
        for (int i = 0; i < outlineObjects.Count; i++)
        {
            if (outlineObjects[i] != null) Destroy(outlineObjects[i]);
        }

        outlineObjects.Clear();
    }
}
