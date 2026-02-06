using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class ShepardController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Move speed in units per second")]
    public float moveSpeed = 5.0f;
    [Tooltip("Rotation speed in degrees per second")]
    public float rotationSpeed = 720.0f;
    [Tooltip("Gravity force applied to the character")]
    public float gravity = 9.81f;
    [Tooltip("Jump Height")]
    public float jumpHeight = 1.5f;

    private CharacterController _characterController;
    private PlayerInput _playerInput;
    private InputAction _moveAction;
    private InputAction _jumpAction;
    private Vector2 _moveInput;
    private Vector3 _velocity;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _playerInput = GetComponent<PlayerInput>();
        
        // Setup input actions
        // Assuming the default "Player" map and "Move" action names from the standard Unity template
        // Adjust these strings if your Input Action Asset uses different names
        _moveAction = _playerInput.actions["Move"];
        _jumpAction = _playerInput.actions["Jump"];
    }

    private void Update()
    {
        HandleMovement();
        ApplyGravity();
    }

    private void HandleMovement()
    {
        // Read input value
        _moveInput = _moveAction.ReadValue<Vector2>();

        // Convert 2D input to 3D movement (on the XZ plane)
        Vector3 move = new Vector3(_moveInput.x, 0, _moveInput.y);

        // Normalize direction so diagonal movement isn't faster
        if (move.magnitude > 1.0f)
            move.Normalize();

        if (move.magnitude >= 0.1f)
        {
            // Calculate target angle
            float targetAngle = Mathf.Atan2(move.x, move.z) * Mathf.Rad2Deg;
            
            // Smooth rotation
            float angle = Mathf.MoveTowardsAngle(transform.eulerAngles.y, targetAngle, rotationSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);

            // Move in the direction we are facing (or input direction)
            // Here we move in world space direction based on input
            _characterController.Move(move * moveSpeed * Time.deltaTime);
        }
    }

    private void ApplyGravity()
    {
        if (_characterController.isGrounded && _velocity.y < 0)
        {
            _velocity.y = -2f; // Small downward force to keep grounded
        }

        // Jump
        if (_characterController.isGrounded && _jumpAction.triggered)
        {
            // v = sqrt(h * -2 * g)
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * -gravity);
        }

        _velocity.y -= gravity * Time.deltaTime;
        _characterController.Move(_velocity * Time.deltaTime);
    }
}
