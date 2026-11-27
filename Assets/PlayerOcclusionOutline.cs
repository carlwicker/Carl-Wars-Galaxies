using Unity.Netcode;
using UnityEngine;

public sealed class PlayerOcclusionOutline : NetworkBehaviour
{
    [SerializeField] private Renderer outlineRenderer;
    [SerializeField] private Renderer playerRenderer;
    [SerializeField] private LayerMask obstructionMask = Physics.DefaultRaycastLayers;

    private readonly RaycastHit[] raycastHits = new RaycastHit[32];
    private Camera playerCamera;
    private VehicleInteraction vehicleInteraction;
    
    private void Awake()
    {
        SetOutlineVisible(false);
        vehicleInteraction = GetComponent<VehicleInteraction>();

        if (outlineRenderer == null || playerRenderer == null)
        {
            Debug.LogError($"{nameof(PlayerOcclusionOutline)} on {name} is missing a player or outline renderer.", this);
        }
    }

    private void LateUpdate()
    {
        if (!IsOwner)
        {
            SetOutlineVisible(false);
            return;
        }

        // Don't show outline when riding a vehicle
        if (vehicleInteraction != null && vehicleInteraction.IsRidingVehicle)
        {
            SetOutlineVisible(false);
            return;
        }

        if (playerCamera == null || !playerCamera.isActiveAndEnabled)
        {
            playerCamera = Camera.main;
        }

        if (playerCamera == null || playerRenderer == null || outlineRenderer == null)
        {
            SetOutlineVisible(false);
            return;
        }

        Vector3 origin = playerCamera.transform.position;
        Vector3 direction = playerRenderer.bounds.center - origin;
        float distance = direction.magnitude;
        if (distance <= Mathf.Epsilon)
        {
            SetOutlineVisible(false);
            return;
        }

        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction / distance,
            raycastHits,
            distance,
            obstructionMask,
            QueryTriggerInteraction.Ignore);

        bool isOccluded = false;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = raycastHits[i].collider;
            if (hitCollider == null)
            {
                continue;
            }

            Transform hitTransform = hitCollider.transform;
            if (hitTransform != transform && !hitTransform.IsChildOf(transform))
            {
                isOccluded = true;
                break;
            }
        }

        SetOutlineVisible(isOccluded);
    }

    public override void OnNetworkDespawn()
    {
        SetOutlineVisible(false);
    }

    private void SetOutlineVisible(bool visible)
    {
        if (outlineRenderer != null && outlineRenderer.enabled != visible)
        {
            outlineRenderer.enabled = visible;
        }
    }
}
