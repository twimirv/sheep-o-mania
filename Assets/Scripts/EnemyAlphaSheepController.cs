using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(CharacterController))]
public class EnemyAlphaSheepController : MonoBehaviour, ISheepLeader
{
    [Header("Movement Settings")]
    public float roamSpeed = 3.0f;
    public float roamRadius = 20f;
    public float rotationSpeed = 120.0f;
    public float gravity = 9.81f;

    [Header("Recruitment")]
    public float recruitmentRadius = 5.0f;
    public float recruitmentInterval = 1.0f;

    [Header("Concussion")]
    public float concussionDuration = 5.0f;
    public float tackleThreshold = 10.0f; 
    public ParticleSystem concussionParticles; 
    public float blinkInterval = 0.2f;

    [Header("Aggression")]
    public float dashAttackDistance = 8.0f;
    public float dashAttackCooldown = 3.0f;
    public float dashSpeed = 15.0f;
    public float dashDuration = 0.5f;

    // ISheepLeader Implementation
    public Vector3 Velocity => _velocity;
    public bool IsPlayer => false;
    public float LastJumpTime => -100f; 

    private CharacterController _characterController;
    private Vector3 _velocity;
    private Vector3 _roamTarget;
    private float _roamTargetTime;
    private Vector3 _startPosition;
    
    private List<FollowerSheepController> _myHerd = new List<FollowerSheepController>();
    public int HerdCount => _myHerd.Count;
    private float _nextRecruitTime;
    private bool _isConcussed = false;
    private float _concussionRecoveryTime;
    
    private Renderer _renderer;
    private Color _originalColor;
    private bool _blinkState;
    private float _nextBlinkTime;

    private float _nextDashAttackTime;
    private bool _isDashing = false;

    private void Start()
    {
        _renderer = GetComponentInChildren<Renderer>();
        if (_renderer != null)
        {
            _originalColor = Color.red; 
            _renderer.material.color = _originalColor;
        }

        if (concussionParticles != null)
        {
            concussionParticles.Stop();
        }
    }

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _startPosition = transform.position;
        PickNewRoamTarget();
    }

    private void Update()
    {
        if (_isDashing) return; // Dash handled in Coroutine

        if (_isConcussed)
        {
            HandleConcussion();
        }
        else
        {
            HandleRoaming();
            HandleRecruitment();
            HandleAggression();
        }
        
        ApplyGravity();
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
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            
            _characterController.Move(moveDir * roamSpeed * Time.deltaTime);
        }
    }

    private void HandleRecruitment()
    {
        if (Time.time < _nextRecruitTime) return;
        _nextRecruitTime = Time.time + recruitmentInterval;

        Collider[] colliders = Physics.OverlapSphere(transform.position, recruitmentRadius);
        foreach (var col in colliders)
        {
            var sheep = col.GetComponent<FollowerSheepController>();
            // If sheep exists and has NO leader, recruit it
            if (sheep != null && sheep.GetLeader() == null) 
            {
                 sheep.JoinLeader(this);
            }
        }
    }

    private void HandleAggression()
    {
        if (Time.time < _nextDashAttackTime) return;

        // Check distance to player
        // Note: In a real game, caching the player reference is better, but finding one is okay if only one player.
        var player = FindObjectOfType<AlphaSheepController>();
        if (player != null)
        {
            float dist = Vector3.Distance(transform.position, player.transform.position);
            if (dist < dashAttackDistance)
            {
                StartCoroutine(DashAttack(player.transform.position));
            }
        }
    }

    private System.Collections.IEnumerator DashAttack(Vector3 targetPos)
    {
        _isDashing = true;
        _nextDashAttackTime = Time.time + dashAttackCooldown;
        
        Vector3 dashDir = (targetPos - transform.position).normalized;
        dashDir.y = 0; // Flat dash

        float elapsed = 0f;
        while (elapsed < dashDuration)
        {
            // Move
            _characterController.Move(dashDir * dashSpeed * Time.deltaTime);
            transform.forward = dashDir;
            elapsed += Time.deltaTime;
            
            // Gravity during dash
            if (!_characterController.isGrounded)
            {
                _characterController.Move(Vector3.down * gravity * Time.deltaTime);
            }
            
            // Check for Shield Collision OR Player Collision
            // We do a small overlap check in front
            Collider[] hits = Physics.OverlapSphere(transform.position + transform.forward * 1.0f, 1.0f);
            foreach (var hit in hits)
            {
                var sheep = hit.GetComponent<FollowerSheepController>();
                // Shield Check
                if (sheep != null && sheep.GetLeader() != null && sheep.GetLeader().IsPlayer && sheep.IsShielding)
                {
                    // Hit a shielding sheep! Stop!
                    _isDashing = false;
                    _characterController.Move(-transform.forward * 2.0f);
                    yield break; // Exit dash routine
                }

                // Alpha Player Collision Check
                var alpha = hit.GetComponent<AlphaSheepController>();
                if (alpha != null)
                {
                    // Hit the Player!
                    // Call Concuss on Player to trigger effects and get dropped sheep
                    var dropped = alpha.Concuss();
                        
                    // 2. Steal up to 3
                    int stolenCount = 0;
                    int maxSteal = 3;
                    
                    foreach (var victim in dropped)
                    {
                        if (victim == null) continue;

                        if (stolenCount < maxSteal)
                        {
                            victim.JoinLeader(this);
                            stolenCount++;
                        }
                        else
                        {
                            // 3. Despawn the rest with smoke
                            StartCoroutine(DespawnWithSmoke(victim));
                        }
                    }

                    // Apply knockback to alpha? (Optional, maybe for feel)
                    // alpha.ApplyKnockback(transform.forward * 10f);

                    _isDashing = false;
                    _characterController.Move(-transform.forward * 2.0f); // Bounce back
                    yield break;
                }
            }

            yield return null;
        }
        
        _isDashing = false;
    }

    private System.Collections.IEnumerator DespawnWithSmoke(FollowerSheepController sheep)
    {
        // Play Smoke Function
        if (concussionParticles != null) // Reuse if suitable, otherwise instantiate new
        {
             // Instantiate a copy at position
             var smoke = Instantiate(concussionParticles, sheep.transform.position, Quaternion.identity);
             smoke.Play();
             Destroy(smoke.gameObject, 2.0f);
        }
        
        Destroy(sheep.gameObject);
        yield return null;
    }

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
            if (_renderer != null)
            {
                _renderer.enabled = _blinkState; // Simple blink by toggling renderer
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
        _roamTargetTime = Time.time + Random.Range(3.0f, 8.0f);
    }

    // Public Interaction
    public void ApplyForce(Vector3 force)
    {
        if (force.magnitude >= tackleThreshold)
        {
             Concuss();
        }
    }

    private void Concuss()
    {
        if (_isConcussed) return;
        
        _isConcussed = true;
        _concussionRecoveryTime = Time.time + concussionDuration;
        
        if (concussionParticles != null) concussionParticles.Play();
        
        // Drop Herd
        DropHerd();
        
        Debug.Log("Enemy Alpha Concussed! Dropped Herd.");
    }


    private void DropHerd()
    {
        // Iterate backwards to safely remove
        for (int i = _myHerd.Count - 1; i >= 0; i--)
        {
            if (_myHerd[i] != null)
            {
                FollowerSheepController follower = _myHerd[i];
                follower.LeaveLeader(); // This unregisters logic
                
                // Force Flee: Set a roam target away from me
                Vector3 fleeDir = (follower.transform.position - transform.position).normalized;
                if (fleeDir == Vector3.zero) fleeDir = Random.insideUnitSphere;
                fleeDir.y = 0;
                
                follower.RoamTo(follower.transform.position + fleeDir * 15.0f);
            }
        }
        _myHerd.Clear();
    }

    // ISheepLeader Methods
    public int RegisterFollower(FollowerSheepController follower)
    {
        if (!_myHerd.Contains(follower))
        {
            _myHerd.Add(follower);
        }
        return _myHerd.Count;
    }

    public void UnregisterFollower(FollowerSheepController follower)
    {
        if (_myHerd.Contains(follower))
        {
            _myHerd.Remove(follower);
        }
    }
}
