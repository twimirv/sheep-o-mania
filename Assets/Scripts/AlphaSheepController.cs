using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class AlphaSheepController : MonoBehaviour, ISheepLeader
{
    // ISheepLeader Implementation
    public Vector3 Velocity => _velocity;
    public bool IsPlayer => true;
    // LastJumpTime is already public property in this class
    public void UnregisterFollower(FollowerSheepController follower) { 
        if(HerdManager.Instance != null) HerdManager.Instance.UnregisterFollower(follower);
        _followerCount--; 
    }
    [Header("Movement Settings")]
    public float moveSpeed = 6.0f;
    public float rotationSpeed = 720.0f;
    public float gravity = 12f;
    public float jumpHeight = 1.0f;
    
    [Header("Smoothing")]
    [Tooltip("Time it takes to reach target speed")]
    public float speedSmoothTime = 0.1f;
    [Tooltip("Time it takes to reach target rotation")]
    public float turnSmoothTime = 0.1f;

    [Header("Herd Settings")]
    public float followerDelayStep = 0.5f; // Seconds delay per follower

    [Header("Shield Settings")]
    [Tooltip("Multiplier for movement speed when shield is active (0.2 = 80% reduction)")]
    public float shieldSpeedMultiplier = 0.2f;

    [Header("Speed Boost Settings")]
    [Tooltip("Speed multiplier during boost")]
    public float speedBoostMultiplier = 3.0f; // 300%
    [Tooltip("Duration of the speed boost in seconds")]
    public float speedBoostDuration = 3.0f; 
    [Tooltip("Particle System to play during speed boost")]
    public ParticleSystem runningFlames;

    // Herd Logic
    public float LastJumpTime { get; private set; } = -100f; // Kept for interface compatibility
    
    private bool _isBoosting = false;

    private System.Collections.IEnumerator SpeedBoostRoutine()
    {
        _isBoosting = true;
        if (runningFlames != null) runningFlames.Play();
        
        yield return new WaitForSeconds(speedBoostDuration);
        
        if (runningFlames != null) runningFlames.Stop();
        _isBoosting = false;
    }
    
    private CharacterController _characterController;
    private PlayerInput _playerInput;
    private InputAction _moveAction;
    private InputAction _jumpAction;
    private InputAction _dashAction;
    private InputAction _quickTurnAction;
    private InputAction _shieldAction;
    private Vector3 _velocity;
    
    // State
    private bool _canMove = true;
    
    private float _speedSmoothVelocity;
    private float _turnSmoothVelocity;
    private float _currentSpeed;
    
    private int _followerCount = 0;
    public int FollowerCount => _followerCount;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _playerInput = GetComponent<PlayerInput>();
        
        // Prevent climbing on other sheep
        _characterController.stepOffset = 0.1f; 

        _moveAction = _playerInput.actions["Move"];
        _jumpAction = _playerInput.actions["Jump"];
        // We repurposed Crouch (Actions[4] usually, or by name)
        // Interact now maps to B/E, used for Shield
        _shieldAction = _playerInput.actions["Interact"]; 
        // Quick Turn moved to Attack (X / Left Click)
        _quickTurnAction = _playerInput.actions["Attack"]; 
        // Dash remains on Crouch (now Y / Q)
        _dashAction = _playerInput.actions["Crouch"];
        
        // Ensure particles stay in world space
        if (runningFlames != null)
        {
            var main = runningFlames.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
        }
    }

    private void Update()
    {
        // Concussion Effect (Visual Only, movement allowed)
        if (_isConcussed)
        {
            HandleConcussion();
        }

        if (_canMove)
        {
            HandleMovement();
        }
    }

    private void HandleMovement()
    {
        // ... existing input reading ...
        Vector2 input = _moveAction.ReadValue<Vector2>();
        Vector3 move = new Vector3(input.x, 0, input.y);

        // ... existing dash/quickturn/shield logic ...
        
        // Dash Input
        if (_dashAction.triggered)
        {
            CommandDash();
            // return; // Don't skip movement frame
        }

        if (_quickTurnAction.triggered)
        {
            CommandQuickTurn();
        }

        if (_shieldAction != null)
        {
             // ... shield logic (Start/Update/Dismiss) ...
            if (_shieldAction.WasPressedThisFrame())
            {
                if (HerdManager.Instance != null)
                {
                    // Use Euler Angles to get a robust horizontal forward, even if looking down/up
                    Vector3 flatForward = Quaternion.Euler(0, transform.eulerAngles.y, 0) * Vector3.forward;
                    if (flatForward.sqrMagnitude < 0.001f) flatForward = transform.forward; // Fallback
                    
                    HerdManager.Instance.StartShield(transform.position, flatForward);
                }
                _isShieldActive = true;
            }

            if (_shieldAction.IsPressed())
            {
                if (_isShieldActive && HerdManager.Instance != null)
                {
                    HerdManager.Instance.UpdateShield(transform.position, transform.forward);
                }
            }
            
            if (_shieldAction.WasReleasedThisFrame())
            {
                _isShieldActive = false;
                if (HerdManager.Instance != null)
                {
                    HerdManager.Instance.DismissShield();
                }
            }
        }

        if (move.magnitude > 1.0f) move.Normalize();

        float finalMoveSpeed = moveSpeed;
        if (_isShieldActive)
        {
            finalMoveSpeed *= shieldSpeedMultiplier;
        }

        // Constant movement: Target speed is always finalMoveSpeed, ignoring input magnitude
        float targetSpeed = finalMoveSpeed;
        
        // Apply Speed Boost
        if (_isBoosting)
        {
            targetSpeed *= speedBoostMultiplier;
        }

        _currentSpeed = Mathf.SmoothDamp(_currentSpeed, targetSpeed, ref _speedSmoothVelocity, speedSmoothTime);

        if (move.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(move.x, move.z) * Mathf.Rad2Deg;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref _turnSmoothVelocity, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);
        }
        
        Vector3 moveDir = Quaternion.Euler(0f, transform.eulerAngles.y, 0f) * Vector3.forward;
        
        // Move constantly
        _characterController.Move(moveDir.normalized * _currentSpeed * Time.deltaTime);
        
        // ... gravity ...

        if (_characterController.isGrounded && _velocity.y < 0)
        {
            _velocity.y = -2f;
        }

        // Jump replaced with Speed Boost
        if (_jumpAction.triggered && !_isBoosting)
        {
            StartCoroutine(SpeedBoostRoutine());
        }

        _velocity.y -= gravity * Time.deltaTime;
        _characterController.Move(_velocity * Time.deltaTime);
    }

    public int RegisterFollower(FollowerSheepController follower)
    {
        _followerCount++;
        return _followerCount;
    }

    private void CommandDash()
    {
        // Command Dash
        Vector3 dashDir = transform.forward;
        if (HerdManager.Instance != null)
        {
            HerdManager.Instance.TriggerDash(dashDir);
        }
    }

    private void CommandQuickTurn()
    {
        if (HerdManager.Instance != null)
        {
            HerdManager.Instance.TriggerQuickTurn(transform.position, transform.forward);
        }
    }

    private bool _isShieldActive = false;
    
    // --- Concussion Logic ---
    [Header("Concussion Settings")]
    public float concussionDuration = 2.0f;
    public float knockbackDuration = 0.5f; // Added adjustable duration
    public ParticleSystem concussionParticles;
    public float blinkInterval = 0.2f;

    private bool _isConcussed = false;
    private float _concussionRecoveryTime;
    private float _nextBlinkTime;
    private bool _blinkState;
    private Renderer _renderer;

    private void HandleConcussion()
    {
        if (Time.time > _concussionRecoveryTime)
        {
            EndConcussion();
            return;
        }

        // Blinking Effect
        if (Time.time > _nextBlinkTime)
        {
            _blinkState = !_blinkState;
            if (_renderer == null) _renderer = GetComponentInChildren<Renderer>();
            
            if (_renderer != null)
            {
                _renderer.enabled = _blinkState; 
            }
            _nextBlinkTime = Time.time + blinkInterval;
        }
    }

    private void EndConcussion()
    {
        _isConcussed = false;
        if (_renderer != null) _renderer.enabled = true;
        if (concussionParticles != null) concussionParticles.Stop();
    }

    public System.Collections.Generic.List<FollowerSheepController> Concuss()
    {
        if (_isConcussed) return new System.Collections.Generic.List<FollowerSheepController>();

        _isConcussed = true;
        _concussionRecoveryTime = Time.time + concussionDuration;
        
        if (concussionParticles != null) concussionParticles.Play();
        
        // Blink Immediately
        _blinkState = false;
        _nextBlinkTime = Time.time + blinkInterval;
        if (_renderer == null) _renderer = GetComponentInChildren<Renderer>();
        if (_renderer != null) _renderer.enabled = false;

        // Drop 50% of Herd
        int dropCount = Mathf.FloorToInt(_followerCount * 0.5f);
        if (dropCount > 0 && HerdManager.Instance != null)
        {
            // _followerCount will be decremented as they are removed in HerdManager -> Unregister
            // Wait, HerdManager.DropFollowers calls LeaveLeader which calls UnregisterFollower on US.
            // So our local _followerCount will update automatically.
            return HerdManager.Instance.DropFollowers(dropCount);
        }

        return new System.Collections.Generic.List<FollowerSheepController>();
    }

    public void ApplyKnockback(Vector3 force)
    {
        StartCoroutine(KnockbackRoutine(force));
    }

    private System.Collections.IEnumerator KnockbackRoutine(Vector3 force)
    {
        _canMove = false;
        float duration = knockbackDuration;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            _characterController.Move(force * Time.deltaTime * (1f - (elapsed / duration))); // Decaying force
            elapsed += Time.deltaTime;
            // Also apply gravity
             _characterController.Move(Vector3.down * gravity * Time.deltaTime);
            yield return null;
        }

        _canMove = true;
    }
}
