using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FollowerSheepController : MonoBehaviour
{
    private enum State { Roaming, Following, Dashing, QuickTurning, Shielding }
    
    // Settings now come from HerdManager
    // [Header("Settings")]
    // public float joinDistance = 4.0f; 
    // public float roamRadius = 15f;
    // public float stopDistance = 3.5f; 
    // public float moveSpeed = 5.5f; 
    // public float rotationSpeed = 360.0f;
    // public float gravity = 9.81f;
    // public float jumpHeight = 1.0f;
    // [Header("Smoothness Controller")]
    // public float movementSmoothTime = 0.1f; 

    private Vector3 _smoothDampVelocityRef; // Helper for SmoothDamp
    private Vector3 _currentMoveVelocity; // The persistent smoothed velocity vector
    private Vector3 _externalSlideVelocity;
    
    private State _state = State.Roaming;
    private ISheepLeader _leader;
    private float _timeDelay = 0f;
    private float _nextDashTime = 0f;
    private float _nextQuickTurnTime = 0f;
    
    private Vector3 _startPosition;
    private float _roamTargetTime;
    private Vector3 _roamTarget;
    
    private CharacterController _characterController;
    private Vector3 _velocity;
    private float _lastJumpTime = -1f;

    public ISheepLeader GetLeader() => _leader;
    public bool IsShielding => _state == State.Shielding;

    private void Start()
    {
        _characterController = GetComponent<CharacterController>();
        // Prevent climbing on other sheep
        _characterController.stepOffset = 0.1f;
        
        _startPosition = transform.position;
        PickNewRoamTarget();
        
        if (HerdManager.Instance == null)
        {
            Debug.LogError("HerdManager not found in scene! FollowerSheepController requires HerdManager.");
        }
    }

    private void OnDestroy()
    {
        if (_leader != null)
        {
            _leader.UnregisterFollower(this);
        }
    }

    private void Update()
    {
        if (HerdManager.Instance == null) return;

        if (_state == State.Roaming)
        {
            HandleRoaming();
            CheckForLeader();
        }
        else if (_state == State.Following)
        {
            HandleFollowing();
        }
        // Dashing is handled by coroutine moving the character, but we might want gravity
        
        if (_state != State.Dashing && _state != State.QuickTurning && _state != State.Shielding)
        {
            ApplyGravity();
        }
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        // Check if we hit another sheep (Follower or Alpha)
        // And if we are somewhat "on top" of it (hit normal pointing up)
        
        if (_state == State.Dashing || _state == State.QuickTurning)
        {
            var enemyAlpha = hit.collider.GetComponent<EnemyAlphaSheepController>();
            if (enemyAlpha != null)
            {
                enemyAlpha.ApplyForce(transform.forward * 20.0f); // Arbitrary strong force
            }
        }

        if (hit.normal.y > 0.5f)
        {
            bool isSheep = hit.collider.GetComponent<FollowerSheepController>() != null || hit.collider.GetComponent<AlphaSheepController>() != null;

            if (isSheep && hit.gameObject != gameObject)
            {
                // Calculate slide direction (away from the sheep below)
                // Project on XZ plane
                Vector3 slideDir = new Vector3(hit.normal.x, 0, hit.normal.z).normalized;
                
                // If perfectly flat/vertical hit, try to slide away from center of other sheep
                if (slideDir.sqrMagnitude < 0.01f)
                {
                    slideDir = (transform.position - hit.collider.transform.position).normalized;
                    slideDir.y = 0;
                }
                
                if (slideDir.sqrMagnitude < 0.01f)
                {
                     slideDir = new Vector3(Random.Range(-1f,1f), 0, Random.Range(-1f,1f)).normalized;
                }
                
                // Apply strong slide
                _externalSlideVelocity = slideDir * 10.0f;
            }
        }
    }

    private void CheckForLeader()
    {
        // Detect Player Alpha
        if (_leader == null)
        {
            var alpha = FindObjectOfType<AlphaSheepController>();
            if (alpha != null)
            {
                float dist = Vector3.Distance(transform.position, alpha.transform.position);
                if (dist <= HerdManager.Instance.joinDistance)
                {
                    JoinLeader(alpha);
                }
            }
        }
    }

    [Header("Visual Effects")]
    public ParticleSystem joinParticles;

    public void JoinLeader(ISheepLeader newLeader)
    {
        if (_leader != null) return;

        _leader = newLeader;
        _state = State.Following;
        int orderIndex = _leader.RegisterFollower(this);
        
        _timeDelay = orderIndex * 0.15f; 
        
        // Ignore Collision with Leader
        if (_leader is MonoBehaviour leaderMono)
        {
            var leaderCollider = leaderMono.GetComponent<Collider>();
            if (leaderCollider != null)
            {
                Physics.IgnoreCollision(_characterController, leaderCollider, true);
            }
        }
        
        // Only Register with HerdManager if it's the Player
        if (_leader.IsPlayer && HerdManager.Instance != null)
        {
            HerdManager.Instance.RegisterFollower(this);
            
            // Play Effects
            if (joinParticles != null) 
            {
                joinParticles.Play();
            }
            HerdManager.Instance.PlayJoinSound();
        }
        
        Debug.Log($"Sheep joined leader {newLeader}");
    }
    
    public void LeaveLeader()
    {
         if (_leader != null)
         {
             // Restore Collision with Leader
             if (_leader is MonoBehaviour leaderMono)
             {
                 var leaderCollider = leaderMono.GetComponent<Collider>();
                 if (leaderCollider != null)
                 {
                     Physics.IgnoreCollision(_characterController, leaderCollider, false);
                 }
             }

             if (_leader.IsPlayer && HerdManager.Instance != null)
             {
                 HerdManager.Instance.UnregisterFollower(this);
             }
             _leader.UnregisterFollower(this);
             _leader = null;
         }
         _state = State.Roaming;
         PickNewRoamTarget();
    }

    // --- Following State Logic ---
    // All skills (Jump, Dash, etc.) initiated by Alpha are handled here
    // or through HerdManager events for registered followers only.
    private void HandleFollowing()
    {
        if (_leader == null) return;

        // 1. Calculate Target Direction & Speed
        Vector3 targetPosition = _leader.transform.position;
        Vector3 directionToAlpha = targetPosition - transform.position;
        directionToAlpha.y = 0; // Ignore height
        
        float distance = directionToAlpha.magnitude;
        Vector3 targetVelocity = Vector3.zero;

        if (distance > HerdManager.Instance.stopDistance)
        {
             targetVelocity = directionToAlpha.normalized * HerdManager.Instance.moveSpeed;
        }

        // 2. Smoothly interpolate velocity
        if (HerdManager.Instance.movementSmoothTime > 0)
        {
            _currentMoveVelocity = Vector3.SmoothDamp(_currentMoveVelocity, targetVelocity, ref _smoothDampVelocityRef, HerdManager.Instance.movementSmoothTime);
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
             transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, HerdManager.Instance.rotationSpeed * Time.deltaTime);
        }

        // 5. Idle Rotation (look at leader when stopped)
        // Only if we are effectively stopped but still close to leader
        if (distance <= HerdManager.Instance.stopDistance && directionToAlpha.sqrMagnitude > 0.1f && _currentMoveVelocity.magnitude < 0.5f)
        {
             Quaternion targetRot = Quaternion.LookRotation(directionToAlpha);
             transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, HerdManager.Instance.rotationSpeed * 0.5f * Time.deltaTime);
        }

        // 6. Jump Copying
        if (_leader.LastJumpTime > _lastJumpTime && Time.time >= _leader.LastJumpTime + _timeDelay)
        {
             if (_characterController.isGrounded)
             {
                 _velocity.y = Mathf.Sqrt(HerdManager.Instance.jumpHeight * -2f * -HerdManager.Instance.gravity);
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
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, HerdManager.Instance.rotationSpeed * 0.5f * Time.deltaTime);
            _characterController.Move(moveDir * (HerdManager.Instance.moveSpeed * 0.5f) * Time.deltaTime);
        }
    }
    
    private void ApplyGravity()
    {
        if (_characterController.isGrounded && _velocity.y < 0)
        {
            _velocity.y = -2f;
        }

        _velocity.y -= HerdManager.Instance.gravity * Time.deltaTime;
        _characterController.Move((_velocity + _externalSlideVelocity) * Time.deltaTime);

        // Damp external slide velocity
        _externalSlideVelocity = Vector3.Lerp(_externalSlideVelocity, Vector3.zero, 5f * Time.deltaTime);
    }

    public void RoamTo(Vector3 target)
    {
        _roamTarget = target;
        _roamTargetTime = Time.time + 3.0f; // Force move here for a bit
    }

    private void PickNewRoamTarget()
    {
        Vector2 randomCircle = Random.insideUnitCircle * HerdManager.Instance.roamRadius;
        _roamTarget = _startPosition + new Vector3(randomCircle.x, 0, randomCircle.y);
        _roamTargetTime = Time.time + Random.Range(2.0f, 6.0f);
    }

    public void Dash(Vector3 direction)
    {
        if (Time.time >= _nextDashTime && _state != State.Dashing)
        {
            StartCoroutine(DashRoutine(direction));
        }
    }

    private System.Collections.IEnumerator DashRoutine(Vector3 direction)
    {
        _state = State.Dashing;
        
        bool collisionIgnored = false;
        Collider alphaCollider = null;

        if (_leader != null && _leader is MonoBehaviour alphaMono)
        {
            alphaCollider = alphaMono.GetComponent<Collider>();
            if (alphaCollider != null)
            {
                Physics.IgnoreCollision(_characterController, alphaCollider, true);
                collisionIgnored = true;
            }
        }
        
        float traversedDistance = 0f;
        while (traversedDistance < HerdManager.Instance.dashDistance)
        {
            float step = HerdManager.Instance.dashSpeed * Time.deltaTime;

            // default to horizontal
            Vector3 moveDir = direction;

            // Raycast down to align with terrain
            // Start slightly up to avoid starting in ground, cast down distinct distance
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 2.0f))
            {
                // Project our horizontal dash direction onto the ground plane
                moveDir = Vector3.ProjectOnPlane(direction, hit.normal).normalized;
            }

            _characterController.Move(moveDir * step);
            
            // Apply extra gravity snap to keep them grounded during dash causing them not to fly off slopes
            if (!_characterController.isGrounded)
            {
                _characterController.Move(Vector3.down * 10f * Time.deltaTime);
            }

            traversedDistance += step;
            
            // Rotate towards dash direction
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, HerdManager.Instance.rotationSpeed * Time.deltaTime);
            
            yield return null;
        }

        if (collisionIgnored && alphaCollider != null)
        {
             Physics.IgnoreCollision(_characterController, alphaCollider, false);
        }
        
        // Wait before returning to follow state
        yield return new WaitForSeconds(HerdManager.Instance.dashReturnDelay);
        
        _nextDashTime = Time.time + HerdManager.Instance.dashCooldown;
        _state = State.Following;
    }

    public void QuickTurn(Vector3 centerPoint, Vector3 targetPosition, bool clockwise)
    {
        if (Time.time >= _nextQuickTurnTime && _state != State.Dashing && _state != State.QuickTurning && _state != State.Shielding)
        {
            StartCoroutine(QuickTurnRoutine(centerPoint, targetPosition, clockwise));
        }
    }

    private System.Collections.IEnumerator QuickTurnRoutine(Vector3 centerPoint, Vector3 targetPosition, bool clockwise)
    {
        _state = State.QuickTurning;

        Vector3 startPos = transform.position;
        
        // Calculate start angle relative to center
        Vector3 startOffset = startPos - centerPoint;
        float startAngle = Mathf.Atan2(startOffset.z, startOffset.x) * Mathf.Rad2Deg;
        float startDist = startOffset.magnitude;

        // Calculate end angle and distance relative to center
        Vector3 endOffset = targetPosition - centerPoint;
        float endAngle = Mathf.Atan2(endOffset.z, endOffset.x) * Mathf.Rad2Deg;
        float endDist = endOffset.magnitude;

        // Enforce Direction
        // Clockwise (Math Decreasing Angle): Delta is negative
        // Counter-Clockwise (Math Increasing Angle): Delta is positive
        
        float deltaAngle = 0f;
        
        if (clockwise)
        {
            // We want to decrease angle. 
            // Calculate how much we need to subtract from Start to get to End (modulo 360)
            // wrapped difference: (start - end) repeated 360
            float diff = Mathf.Repeat(startAngle - endAngle, 360f);
            deltaAngle = -diff; 
        }
        else
        {
            // Counter-Clockwise
            float diff = Mathf.Repeat(endAngle - startAngle, 360f);
            deltaAngle = diff;
        }

        float duration = HerdManager.Instance.quickTurnDuration;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            // Cubic ease out for a "whip" effect
             t = t * t * (3f - 2f * t);

            float currentAngleDeg = Mathf.Lerp(startAngle, startAngle + deltaAngle, t);
            float currentDist = Mathf.Lerp(startDist, endDist, t);
            
            float currentAngleRad = currentAngleDeg * Mathf.Deg2Rad;

            Vector3 nextPos = centerPoint + new Vector3(Mathf.Cos(currentAngleRad) * currentDist, 0, Mathf.Sin(currentAngleRad) * currentDist);
            
            // Raycast down to align with terrain
            // Start slightly up to avoid starting in ground, cast down distinct distance
            if (Physics.Raycast(new Vector3(nextPos.x, startPos.y + 2.0f, nextPos.z), Vector3.down, out RaycastHit hit, 10.0f))
            {
                 nextPos.y = hit.point.y;
            }
            else
            {
                 nextPos.y = startPos.y; // Fallback to start height if no ground found
            }

            Vector3 diff = nextPos - transform.position;
            _characterController.Move(diff);
            
            // Face tangent
            float facingAngleRad = (currentAngleDeg + 90f) * Mathf.Deg2Rad;
            if (clockwise) facingAngleRad = (currentAngleDeg - 90f) * Mathf.Deg2Rad;

            Vector3 facingDir = new Vector3(Mathf.Cos(facingAngleRad), 0, Mathf.Sin(facingAngleRad));
            transform.rotation = Quaternion.LookRotation(facingDir);

            yield return null;
        }
        
        // Snap/State reset
        _nextQuickTurnTime = Time.time + HerdManager.Instance.quickTurnCooldown;
        _state = State.Following;
    }
    private Vector3 _shieldTarget;
    private Vector3 _shieldCenter;

    public void EnterShieldFormation(Vector3 targetPos, Vector3 centerPos)
    {
        _shieldTarget = targetPos;
        _shieldCenter = centerPos;

        if (_state != State.Shielding)
        {
            StopAllCoroutines(); 
            StartCoroutine(ShieldRoutine());
        }
    }

    public void ExitShieldFormation()
    {
        if (_state == State.Shielding)
        {
            StopAllCoroutines();
            _state = State.Following;
        }
    }

    private System.Collections.IEnumerator ShieldRoutine()
    {
        _state = State.Shielding;
        
        // Loop while shielding
        // We combine "Moving to" and "Holding" into one loop that constantly adjusts
        while (_state == State.Shielding)
        {
            Vector3 targetPos = _shieldTarget;

            // DYNAMIC LOOK DIRECTION: Always look away from center
            Vector3 lookDir = (transform.position - _shieldCenter);
            lookDir.y = 0;
            
            // If we are at the center (unlikely given radius), keep current facing
            if (lookDir.sqrMagnitude < 0.001f) 
            {
                 // Keep looking where we are looking, don't snap to North or Alpha's forward
                 lookDir = transform.forward;
                 lookDir.y = 0;
            }
            
            lookDir.Normalize();
            
            // ... rest of logic uses lookDir ...

            float dist = Vector3.Distance(transform.position, targetPos);
            
            // Movement Logic
            if (dist > 0.1f)
            {
                Vector3 dir = (targetPos - transform.position).normalized;
                float step = HerdManager.Instance.dashSpeed * Time.deltaTime; 
                
                if (dist < step)
                {
                    _characterController.Move(targetPos - transform.position);
                }
                else
                {
                    _characterController.Move(dir * step);
                }
                
                // Rotate towards movement while moving significantly
                if (dir.sqrMagnitude > 0.01f)
                {
                    dir.y = 0;
                    Quaternion moveRot = Quaternion.LookRotation(dir);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, moveRot, HerdManager.Instance.rotationSpeed * Time.deltaTime);
                }
            }
            else
            {
                // In Position: Rotate to Face Outward
                Quaternion targetRot = Quaternion.LookRotation(lookDir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, HerdManager.Instance.rotationSpeed * Time.deltaTime);
            }

            // Gravity
            _velocity.y -= HerdManager.Instance.gravity * Time.deltaTime;
            if (_characterController.isGrounded && _velocity.y < 0) _velocity.y = -2f;
            _characterController.Move(_velocity * Time.deltaTime);

            yield return null;
        }
    }
}
