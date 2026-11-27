using UnityEngine;
using UnityEngine.InputSystem;

public class CameraFollow : MonoBehaviour
{
    private static readonly string[] CompassDirections = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

    [SerializeField] private Transform target;
    [SerializeField] private float smoothSpeed = 10f;

    [Header("Zoom Settings")]
    [SerializeField] private float zoomSpeed = 1.5f;
    [SerializeField] private float minZoom = 4f;
    [SerializeField] private float maxZoom = 60f;

    [Header("Orbit Settings")]
    [SerializeField] private float orbitSensitivity = 0.15f;
    [SerializeField] private float minPitch = 10f;
    [SerializeField] private float maxPitch = 75f;

    [Header("Dynamic Zoom Based on Pitch")]
    [SerializeField] private bool dynamicZoomOnRotation = true;
    [SerializeField] private float maxDistanceMultiplier = 1.5f;

    [Header("Ground Clearance")]
    [SerializeField] private float foregroundClearance = 1.5f;

    private Camera cam;
    private float targetZoom;
    private float yaw;
    private float pitch;
    private float startingZoom;
    private float startingYaw;
    private float startingPitch;
    private float rightButtonMovement;
    private Vector3 startingCameraLocalPosition;
    private GUIStyle compassLabelStyle;
    private GUIStyle compassHeadingStyle;

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    private void Start()
    {
        cam = GetComponentInChildren<Camera>();
        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = Mathf.DeltaAngle(0f, angles.x);
        startingYaw = yaw;
        startingPitch = pitch;

        if (cam != null)
        {
            targetZoom = cam.orthographicSize;
            startingZoom = targetZoom;
            startingCameraLocalPosition = cam.transform.localPosition;
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        if (Mouse.current != null)
        {
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                rightButtonMovement = 0f;
            }

            if (Mouse.current.rightButton.isPressed || Mouse.current.rightButton.wasReleasedThisFrame)
            {
                Vector2 dragDelta = Mouse.current.delta.ReadValue();
                rightButtonMovement += dragDelta.magnitude;

                if (dragDelta.sqrMagnitude > 0f)
                {
                    yaw += dragDelta.x * orbitSensitivity;
                    pitch = Mathf.Clamp(pitch - dragDelta.y * orbitSensitivity, minPitch, maxPitch);
                    transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
                }
            }

            if (Mouse.current.rightButton.wasReleasedThisFrame && rightButtonMovement < 5f)
            {
                ResetView();
            }

            if (cam != null)
            {
                float scrollValue = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(scrollValue) > 0.001f)
                {
                    targetZoom -= Mathf.Sign(scrollValue) * zoomSpeed;
                    targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
                }
            }
        }

        if (cam != null && cam.orthographic)
        {
            float zoomWithPitch = targetZoom;
            if (dynamicZoomOnRotation)
            {
                float pitchT = Mathf.InverseLerp(maxPitch, minPitch, pitch);
                zoomWithPitch = Mathf.Lerp(targetZoom, targetZoom * maxDistanceMultiplier, pitchT);
            }

            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, zoomWithPitch, smoothSpeed * Time.deltaTime);

            // Move back along the viewing direction so the orthographic foreground starts above the ground.
            cam.transform.localPosition = startingCameraLocalPosition;
            Vector3 nearBottom = cam.transform.position
                + cam.transform.forward * cam.nearClipPlane
                - cam.transform.up * cam.orthographicSize;
            float downwardSlope = -cam.transform.forward.y;
            if (downwardSlope > 0.001f)
            {
                float lift = target.position.y + foregroundClearance - nearBottom.y;
                if (lift > 0f)
                {
                    cam.transform.position -= cam.transform.forward * (lift / downwardSlope);
                }
            }
        }
    }

    private void ResetView()
    {
        yaw = startingYaw;
        pitch = startingPitch;
        targetZoom = startingZoom;
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void OnGUI()
    {
        if (target == null || Event.current.type != EventType.Repaint) return;

        if (compassLabelStyle == null)
        {
            compassLabelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            compassHeadingStyle = new GUIStyle(compassLabelStyle) { fontSize = 12 };
        }

        float width = Mathf.Min(360f, Screen.width - 24f);
        if (width <= 0f) return;

        Rect bounds = new Rect((Screen.width - width) * 0.5f, 12f, width, 54f);
        GUI.BeginGroup(bounds);
        GUI.Box(new Rect(0f, 0f, width, bounds.height), GUIContent.none);

        float heading = Mathf.Repeat(transform.eulerAngles.y, 360f);
        for (int i = 0; i < CompassDirections.Length; i++)
        {
            float delta = Mathf.DeltaAngle(heading, i * 45f);
            if (Mathf.Abs(delta) > 90f) continue;

            float x = width * (0.5f + delta / 180f);
            GUI.Label(new Rect(x - 20f, 3f, 40f, 22f), CompassDirections[i], compassLabelStyle);
        }

        GUI.Label(new Rect(width * 0.5f - 20f, 22f, 40f, 15f), "^", compassHeadingStyle);
        GUI.Label(new Rect(width * 0.5f - 25f, 36f, 50f, 15f),
            (Mathf.RoundToInt(heading) % 360).ToString("000"), compassHeadingStyle);
        GUI.EndGroup();
    }
}