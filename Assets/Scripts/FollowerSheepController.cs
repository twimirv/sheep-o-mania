using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FollowerSheepController : MonoBehaviour
{
    private enum State { Roaming, Following }
    
    [Header("Settings")]
    public float joinDistance = 4.0f; // Increased slightly for giant lambs
    public float roamRadius = 15f;
    public float stopDistance = 3.5f; // Don't crowd too close
    public float moveSpeed = 5.5f; // Slightly slower than Alpha
    public float rotationSpeed = 360.0f;
    public float gravity = 9.81f;
    public float jumpHeight = 1.0f;

    [Header("Smoothness Controller")]
    public float movementSmoothTime = 0.1f; // Defaulted to faster response
    private Vector3 _smoothDampVelocityRef; // Helper for SmoothDamp
    private Vector3 _currentMoveVelocity; // The persistent smoothed velocity vector
    
    private State _state = State.Roaming;
    private AlphaSheepController _alphaSheep;
    private float _timeDelay = 0f;
    
    private Vector3 _startPosition;
    private float _roamTargetTime;
    private Vector3 _roamTarget;
    
    private CharacterController _characterController;
    private Vector3 _velocity;
    private float _lastJumpTime = -1f;

    private void Start()
    {
        _characterController = GetComponent<CharacterController>();
        _startPosition = transform.position;
        PickNewRoamTarget();
    }

    private void Update()
    {
        if (_state == State.Roaming)
        {
            HandleRoaming();
            CheckForAlpha();
        }
        else if (_state == State.Following)
        {
            HandleFollowing();
        }
        
        ApplyGravity();
    }

    private void CheckForAlpha()
    {
        if (_alphaSheep == null)
        {
            _alphaSheep = FindObjectOfType<AlphaSheepController>();
        }

        if (_alphaSheep != null)
        {
            float dist = Vector3.Distance(transform.position, _alphaSheep.transform.position);
            // Check distance, accounting for scale approx logic if needed
            if (dist <= joinDistance)
            {
                JoinHerd();
            }
        }
    }

    private void JoinHerd()
    {
        _state = State.Following;
        int orderIndex = _alphaSheep.RegisterFollower();
        _timeDelay = orderIndex * 0.15f; // Random small delay for jumps
        Debug.Log($"Sheep joined herd (Flocking Mode) at index {orderIndex}");
    }

    private void HandleFollowing()
    {
        if (_alphaSheep == null) return;

        // 1. Calculate Target Direction & Speed
        Vector3 targetPosition = _alphaSheep.transform.position;
        Vector3 directionToAlpha = targetPosition - transform.position;
        directionToAlpha.y = 0; // Ignore height
        
        float distance = directionToAlpha.magnitude;
        Vector3 targetVelocity = Vector3.zero;

        if (distance > stopDistance)
        {
             targetVelocity = directionToAlpha.normalized * moveSpeed;
        }

        // 2. Smoothly interpolate velocity
        if (movementSmoothTime > 0)
        {
            _currentMoveVelocity = Vector3.SmoothDamp(_currentMoveVelocity, targetVelocity, ref _smoothDampVelocityRef, movementSmoothTime);
        }
        else
        {
            _currentMoveVelocity = targetVelocity;
        }
        
        // 3. Move
        _characterController.Move(_currentMoveVelocity * Time.deltaTime);
        
        // 4. Rotate to face movement direction
        if (_currentMoveVelocity.sqrMagnitude > 0.1f)
        {
             Quaternion targetRot = Quaternion.LookRotation(_currentMoveVelocity);
             transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }

        // 5. Idle Rotation (look at leader when stopped)
        // Only if we are effectively stopped but still close to leader
        if (distance <= stopDistance && directionToAlpha.sqrMagnitude > 0.1f && _currentMoveVelocity.magnitude < 0.5f)
        {
             Quaternion targetRot = Quaternion.LookRotation(directionToAlpha);
             transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * 0.5f * Time.deltaTime);
        }

        // 6. Jump Copying
        if (_alphaSheep.LastJumpTime > _lastJumpTime && Time.time >= _alphaSheep.LastJumpTime + _timeDelay)
        {
             if (_characterController.isGrounded)
             {
                 _velocity.y = Mathf.Sqrt(jumpHeight * -2f * -gravity);
                 _lastJumpTime = Time.time;
             }
        }
    }

    private void HandleRoaming()
    {
        if (Time.time > _roamTargetTime)
        {
            PickNewRoamTarget();
        }
        
        Vector3 direction = _roamTarget - transform.position;
        direction.y = 0;
        
        if (direction.magnitude > 0.5f)
        {
            Vector3 moveDir = direction.normalized;
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * 0.5f * Time.deltaTime);
            _characterController.Move(moveDir * (moveSpeed * 0.5f) * Time.deltaTime);
        }
    }
    
    private void ApplyGravity()
    {
        if (_characterController.isGrounded && _velocity.y < 0)
        {
            _velocity.y = -2f;
        }

        _velocity.y -= gravity * Time.deltaTime;
        _characterController.Move(_velocity * Time.deltaTime);
    }

    private void PickNewRoamTarget()
    {
        Vector2 randomCircle = Random.insideUnitCircle * roamRadius;
        _roamTarget = _startPosition + new Vector3(randomCircle.x, 0, randomCircle.y);
        _roamTargetTime = Time.time + Random.Range(2.0f, 6.0f);
    }
}
