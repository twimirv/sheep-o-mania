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
    public float concussionDuration = 3.0f;
    public float tackleThreshold = 10.0f; // Force required to stun

    // ISheepLeader Implementation
    public Vector3 Velocity => _velocity;
    public bool IsPlayer => false;
    public float LastJumpTime => -100f; // Enemies don't jump yet

    private CharacterController _characterController;
    private Vector3 _velocity;
    private Vector3 _roamTarget;
    private float _roamTargetTime;
    private Vector3 _startPosition;
    
    private List<FollowerSheepController> _myHerd = new List<FollowerSheepController>();
    private float _nextRecruitTime;
    private bool _isConcussed = false;
    private float _concussionRecoveryTime;

    private void Start()
    {
        var renderer = GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = Color.red;
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
        if (_isConcussed)
        {
            HandleConcussion();
        }
        else
        {
            HandleRoaming();
            HandleRecruitment();
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
                 // We need FollowerSheepController to expose a public method to Join a leader
                 // For now, let's assume we can call something or set it logic
                 // Since FollowerSheepController.CheckForAlpha is internal logic, we might need to expose "JoinLeader(ISheepLeader)"
                 
                 // Implementation in Follower will follow
                 sheep.JoinLeader(this);
            }
        }
    }
    
    private void HandleConcussion()
    {
        if (Time.time > _concussionRecoveryTime)
        {
            _isConcussed = false;
            // Recovered
        }
        // Maybe spin stars or shake?
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
                _myHerd[i].LeaveLeader();
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
