using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("The Transform to follow")]
    public Transform target;

    [Header("Camera Settings")]
    [Tooltip("Offset from the target")]
    public Vector3 offset = new Vector3(0, 10, -10);
    [Tooltip("How smoothly the camera catches up to the target")]
    public float smoothSpeed = 0.125f;

    [Header("Zoom Settings")]
    public float zoomSpeed = 0.5f; 
    public float minZoom = 5.0f; 
    public float maxZoom = 40.0f;

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
                
                float currentDist = offset.magnitude;
                float targetDist = Mathf.Clamp(currentDist + zoomChange, minZoom, maxZoom);
                
                offset = offset.normalized * targetDist;
            }
        }

        Vector3 desiredPosition = target.position + offset;
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;

        transform.LookAt(target);
    }
}
