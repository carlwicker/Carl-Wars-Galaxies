using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField, Min(1f)] private float vehicleSpeedMultiplier = 5f;
    [SerializeField] private float rotationSpeed = 15f;
    [SerializeField] private float gravity = -9.81f;

    private CharacterController controller;
    private PlayerChat playerChat;
    private VehicleInteraction vehicleInteraction;
    private CameraFollow cameraFollow;
    private Vector3 velocity;
    private bool missingVehicleReported;
    private readonly Matrix4x4 isoMatrix = Matrix4x4.Rotate(Quaternion.Euler(0, 45f, 0));

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerChat = GetComponent<PlayerChat>();
        vehicleInteraction = GetComponent<VehicleInteraction>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            cameraFollow = FindAnyObjectByType<CameraFollow>();
            if (cameraFollow != null)
            {
                cameraFollow.SetTarget(transform);
            }
        }
    }
    private void Update()
    {
        if (!IsOwner) return;

        if (playerChat == null || !playerChat.IsChatActive)
        {
            Vehicle vehicle = null;
            if (vehicleInteraction != null && vehicleInteraction.IsRidingVehicle)
            {
                if (!vehicleInteraction.TryGetRiddenVehicle(out vehicle))
                {
                    if (!missingVehicleReported)
                        Debug.LogError("Cannot move: the mounted vehicle is missing.", this);
                    missingVehicleReported = true;
                    return;
                }
                vehicle.RefreshRiderCollision();
            }
            missingVehicleReported = false;
            HandleMovement(vehicle);
            ApplyGravity(vehicle);
        }
    }

    private void HandleMovement(Vehicle vehicle)
    {
        if (vehicle != null && !vehicle.IsOn) return;
        if (Keyboard.current == null) return;

        float x = 0f;
        float z = 0f;

        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) x -= 1f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x += 1f;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) z -= 1f;
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) z += 1f;

        Vector3 input = new Vector3(x, 0f, z).normalized;

        if (input.sqrMagnitude >= 0.01f)
        {
            Vector3 moveDirection = isoMatrix.MultiplyVector(input);
            if (cameraFollow != null)
            {
                Vector3 cameraForward = cameraFollow.transform.forward;
                cameraForward.y = 0f;

                if (cameraForward.sqrMagnitude > 0.0001f)
                {
                    cameraForward.Normalize();
                    Vector3 cameraRight = Vector3.Cross(Vector3.up, cameraForward);
                    moveDirection = cameraRight * input.x + cameraForward * input.z;
                }
            }

            moveDirection.Normalize();
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            Quaternion rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            float speedMultiplier = vehicle != null ? vehicleSpeedMultiplier * vehicle.EngineSpeedMultiplier : 1f;
            Vector3 movement = moveDirection * moveSpeed * speedMultiplier * Time.deltaTime;
            if (vehicle != null) transform.rotation = vehicle.ConstrainRiderRotation(controller, rotation);
            Move(movement, vehicle);
            if (vehicle == null) transform.rotation = rotation;
        }
    }

    private void ApplyGravity(Vehicle vehicle)
    {
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        velocity.y += gravity * Time.deltaTime;
        Vector3 movement = velocity * Time.deltaTime;
        if (!Move(movement, vehicle) && velocity.y < 0f) velocity.y = -2f;
    }

    private bool Move(Vector3 movement, Vehicle vehicle)
    {
        if (vehicle == null)
        {
            controller.Move(movement);
            return true;
        }

        Vector3 constrainedMovement = vehicle.ConstrainRiderMovement(controller, movement);
        Vector3 previousPosition = transform.position;
        controller.Move(constrainedMovement);
        if (!vehicle.IsRiderPositionClear(controller, previousPosition))
        {
            controller.enabled = false;
            transform.position = previousPosition;
            controller.enabled = true;
            vehicle.RefreshRiderCollision();
            return false;
        }
        return (constrainedMovement - movement).sqrMagnitude < 0.000001f;
    }
}