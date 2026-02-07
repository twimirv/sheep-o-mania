using UnityEngine;

public class SheepWiggle : MonoBehaviour
{
    [Header("Wiggle Settings")]
    public float wiggleSpeed = 5.0f;
    public float wiggleAmount = 3.0f; // Degrees
    public float scaleSpeed = 3.0f;
    public float scaleAmount = 0.05f;

    public float stillWiggleMultiplier = 0.9f; // 90% when still
    public float movementThreshold = 0.1f;

    private Quaternion _initialRotation;
    private Vector3 _initialScale;
    private float _randomOffset;
    private CharacterController _characterController;

    private void Start()
    {
        _characterController = GetComponent<CharacterController>();
        
        // Cache initial local values
        // Note: For rotation, we might want to wiggle relative to current rotation if moving?
        // Actually, if we modify transform.localRotation, it might fight with CharacterController rotation logic if applied to the root.
        // It fits better if applied to the *Visual Mesh* child.
        // But if we attach to root, we must wiggle the child.
        
        // Let's assume this script is attached to the Visual Model (the child), OR we find the child.
        // SceneSetupHelper attaches scripts to the root. 
        // IF attached to root, we should wiggle the visual child.
        
        _initialScale = transform.localScale; 
        _randomOffset = Random.Range(0f, 100f);
    }

    private void Update()
    {
        // Check movement
        float currentWiggleAmount = wiggleAmount;
        if (_characterController != null)
        {
             // Use velocity sqrMagnitude for efficiency
             if (_characterController.velocity.sqrMagnitude < (movementThreshold * movementThreshold))
             {
                 currentWiggleAmount *= stillWiggleMultiplier;
             }
        }

        // 1. Scale "Breathing" (Pulse)
        // Helps them look alive even when standing still
        float scaleFactor = 1.0f + Mathf.Sin((Time.time + _randomOffset) * scaleSpeed) * scaleAmount;
        // We only scale Y slightly for breathing effect
        // transform.localScale = new Vector3(_initialScale.x, _initialScale.y * scaleFactor, _initialScale.z); 
        // Be careful not to override 3x global scale if attached to root.
        
        // Let's try applying this to the visual child transform if possible.
        // If this script is on the root, we need to find the child.
        
        if (transform.childCount > 0)
        {
            Transform visual = transform.GetChild(0); // Assuming first child is the model
            
            // Wiggle Rotation (Z-axis sway)
            // We use localRotation so it sways relative to which way the sheep is facing
            float sway = Mathf.Sin((Time.time + _randomOffset) * wiggleSpeed) * currentWiggleAmount;
            // Maintain the 90 degree Y offset we set in setup!
            visual.localRotation = Quaternion.Euler(0, 90f, sway); 
             
            // Breathing
            visual.localScale = new Vector3(3f, 3f * scaleFactor, 3f);
        }
    }
}
