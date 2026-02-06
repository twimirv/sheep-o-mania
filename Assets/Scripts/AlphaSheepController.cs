using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class AlphaSheepController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 6.0f;
    public float rotationSpeed = 720.0f;
    public float gravity = 9.81f;
    public float jumpHeight = 1.0f;

    [Header("Herd Settings")]
    public float followerDelayStep = 0.5f; // Seconds delay per follower

    // Herd Logic
    public float LastJumpTime { get; private set; } = -100f;

    private CharacterController _characterController;
    private PlayerInput _playerInput;
    private InputAction _moveAction;
    private InputAction _jumpAction;
    private Vector3 _velocity;
    
    private int _followerCount = 0;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _playerInput = GetComponent<PlayerInput>();

        _moveAction = _playerInput.actions["Move"];
        _jumpAction = _playerInput.actions["Jump"];
    }

    private void Update()
    {
        HandleMovement();
    }

    private void HandleMovement()
    {
        Vector2 input = _moveAction.ReadValue<Vector2>();
        Vector3 move = new Vector3(input.x, 0, input.y);

        if (move.magnitude > 1.0f) move.Normalize();

        if (move.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(move.x, move.z) * Mathf.Rad2Deg;
            float angle = Mathf.MoveTowardsAngle(transform.eulerAngles.y, targetAngle, rotationSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);

            _characterController.Move(move * moveSpeed * Time.deltaTime);
        }

        if (_characterController.isGrounded && _velocity.y < 0)
        {
            _velocity.y = -2f;
        }

        if (_characterController.isGrounded && _jumpAction.triggered)
        {
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * -gravity);
            LastJumpTime = Time.time;
        }

        _velocity.y -= gravity * Time.deltaTime;
        _characterController.Move(_velocity * Time.deltaTime);
    }

    public int RegisterFollower()
    {
        _followerCount++;
        return _followerCount;
    }
}
