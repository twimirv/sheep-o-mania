using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("The Transform to follow")]
    public Transform target;

    [Header("Camera Settings")]
    [Tooltip("Offset from the target")]
    public Vector3 offset = new Vector3(0, 10, -10);
    [Tooltip("Approximate time to reach the target")]
    public float smoothTime = 0.125f;

    [Header("Zoom Settings")]
    public float zoomSpeed = 0.5f; 
    public float minZoom = 5.0f; 
    public float maxZoom = 40.0f;
    [Tooltip("Current Zoom Distance")]
    public float currentZoom = 10.0f;

    [Header("Isometric Settings")]
    public bool enableIsometric = true;
    public float pitch = 45f;
    public float yaw = 45f;

    private Vector3 _currentVelocity;

    private void Start()
    {
        // Auto-find target if not assigned
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
            else
            {
                Debug.LogWarning("CameraFollow: No target assigned and no object with tag 'Player' found!");
            }
        }

        // Don't overwrite currentZoom with offset.magnitude
        // This ensures the camera starts at the configured currentZoom distance
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            Debug.LogWarning("CameraFollow: Target not assigned!");
            return;
        }

        // Zoom Logic
        if (UnityEngine.InputSystem.Mouse.current != null)
        {
            float scroll = UnityEngine.InputSystem.Mouse.current.scroll.ReadValue().y;
            if (scroll != 0)
            {
                // Scroll up (positive) = zoom in (decrease distance)
                // Scroll down (negative) = zoom out (increase distance)
                float zoomChange = -scroll * zoomSpeed * 0.01f; 
                currentZoom += zoomChange;
            }
        }
        
        // Clamp and apply zoom
        currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);

        if (enableIsometric)
        {
            Quaternion isoRotation = Quaternion.Euler(pitch, yaw, 0);
            Vector3 isoDirection = isoRotation * Vector3.back;
            offset = isoDirection * currentZoom;
            
            transform.rotation = isoRotation;
        }
        else
        {
            offset = offset.normalized * currentZoom;
        }

        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _currentVelocity, smoothTime);

        if (!enableIsometric)
        {
            transform.LookAt(target);
        }
    }
}
