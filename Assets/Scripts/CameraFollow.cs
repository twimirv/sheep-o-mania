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

    private void LateUpdate()
    {
        if (target == null)
        {
            Debug.LogWarning("CameraFollow: Target not assigned!");
            return;
        }

        Vector3 desiredPosition = target.position + offset;
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;

        transform.LookAt(target);
    }
}
