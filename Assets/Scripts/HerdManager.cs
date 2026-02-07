using UnityEngine;

public class HerdManager : MonoBehaviour
{
    public static HerdManager Instance { get; private set; }

    [Header("Settings")]
    public float joinDistance = 4.0f; // Increased slightly for giant lambs
    public float roamRadius = 15f;
    public float stopDistance = 3.5f; // Don't crowd too close
    public float moveSpeed = 5.5f; // Slightly slower than Alpha
    public float rotationSpeed = 360.0f;
    public float gravity = 9.81f;
    public float jumpHeight = 1.0f;

    [Header("Dash Settings")]
    public float dashSpeed = 15.0f;
    public float dashDistance = 7.5f;
    public float dashReturnDelay = 0.5f;
    public float dashCooldown = 2.0f;

    [Header("Smoothness Controller")]
    public float movementSmoothTime = 0.1f; // Defaulted to faster response

    [Header("Audio")]
    public AudioClip[] joinSounds;
    private AudioSource _audioSource;

    private System.Collections.Generic.List<FollowerSheepController> _followers = new System.Collections.Generic.List<FollowerSheepController>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    public void PlayJoinSound()
    {
        if (joinSounds != null && joinSounds.Length > 0 && _audioSource != null)
        {
            AudioClip clip = joinSounds[Random.Range(0, joinSounds.Length)];
            _audioSource.PlayOneShot(clip);
        }
    }

    public void RegisterFollower(FollowerSheepController sheep)
    {
        if (!_followers.Contains(sheep))
        {
            _followers.Add(sheep);
        }
    }

    public void UnregisterFollower(FollowerSheepController sheep)
    {
        if (_followers.Contains(sheep))
        {
            _followers.Remove(sheep);
        }
    }

    public System.Collections.Generic.List<FollowerSheepController> DropFollowers(int count)
    {
        var dropped = new System.Collections.Generic.List<FollowerSheepController>();
        int currentCount = _followers.Count;
        int toDrop = Mathf.Min(count, currentCount);

        for (int i = 0; i < toDrop; i++)
        {
            // Remove from end (most recent additions usually at end)
            int index = _followers.Count - 1;
            var sheep = _followers[index];
            _followers.RemoveAt(index);
            dropped.Add(sheep);
            
            // Notify sheep they left
            if (sheep != null) sheep.LeaveLeader(); 
        }
        return dropped;
    }

    public void TriggerDash(Vector3 direction)
    {
        foreach (var sheep in _followers)
        {
            if (sheep != null)
            {
                sheep.Dash(direction);
            }
        }
    }

    [Header("Quick Turn Settings")]
    public float quickTurnDuration = 0.5f;
    public float quickTurnCooldown = 2.0f;
    public float formationSpacing = 1.5f;
    public float formationDistance = 5.0f;
    public bool forceClockwiseTurn = true;

    public void TriggerQuickTurn(Vector3 centerPoint)
    {
        TriggerQuickTurn(centerPoint, Vector3.forward);
    }


    // Overloading or changing signature to include Forward direction
    public void TriggerQuickTurn(Vector3 centerPoint, Vector3 forwardDir)
    {
        var validFollowers = new System.Collections.Generic.List<FollowerSheepController>();
        foreach (var sheep in _followers)
        {
            if (sheep != null) validFollowers.Add(sheep);
        }

        if (validFollowers.Count == 0) return;

        if (validFollowers.Count == 0) return;

        // Rotate each sheep 180 degrees around the center point
        // This places them "in front" if they started "behind", while maintaining their formation shape.
        foreach (var sheep in validFollowers)
        {
             Vector3 startDisplayOffset = sheep.transform.position - centerPoint;
             // Rotate 180 degrees around Y axis
             Vector3 endOffset = Quaternion.Euler(0, 180f, 0) * startDisplayOffset;
             
             // Apply offset to move them further ahead of the Alpha
             Vector3 targetPos = centerPoint + endOffset + (forwardDir * formationDistance);

             sheep.QuickTurn(centerPoint, targetPos, forceClockwiseTurn);
        }
    }

    [Header("Shield Formation Settings")]
    public float shieldRadius = 3.0f;

    private System.Collections.Generic.List<FollowerSheepController> _shieldSortedFollowers = new System.Collections.Generic.List<FollowerSheepController>();
    private Vector3 _shieldReferenceForward;

    public void StartShield(Vector3 centerPoint, Vector3 forwardDir)
    {
        _shieldSortedFollowers.Clear();
        foreach (var sheep in _followers)
        {
            if (sheep != null) _shieldSortedFollowers.Add(sheep);
        }

        if (_shieldSortedFollowers.Count == 0) return;

        // Capture the forward direction at start to lock the formation orientation
        // We use the Alpha's current forward to orient the formation initially.
        // It remains locked to this orientation during the Hold (UpdateShield ignores new forward).
        _shieldReferenceForward = forwardDir;
        _shieldReferenceForward.y = 0;
        
        // Ensure valid vector
        if (_shieldReferenceForward.sqrMagnitude < 0.001f) _shieldReferenceForward = Vector3.forward;
        
        _shieldReferenceForward.Normalize();

        // Sort by angle relative to INITIAL forward direction to minimize crossing
        _shieldSortedFollowers.Sort((a, b) => 
        {
            Vector3 dirA = (a.transform.position - centerPoint).normalized;
            Vector3 dirB = (b.transform.position - centerPoint).normalized;
            float angleA = Vector3.SignedAngle(_shieldReferenceForward, dirA, Vector3.up);
            float angleB = Vector3.SignedAngle(_shieldReferenceForward, dirB, Vector3.up);
            return angleA.CompareTo(angleB);
        });

        // Initial Update
        UpdateShieldPositions(centerPoint);
    }

    public void UpdateShield(Vector3 centerPoint, Vector3 forwardDir)
    {
        // forwardDir is ignored here to prevent rotation when Alpha turns
        // We use _shieldReferenceForward captured in StartShield
        
        if (_shieldSortedFollowers.Count == 0 || _shieldSortedFollowers.Count != _followers.Count) 
        {
            if (_shieldSortedFollowers.Count == 0) StartShield(centerPoint, forwardDir);
            else UpdateShieldPositions(centerPoint);
        }
        else
        {
             UpdateShieldPositions(centerPoint);
        }
    }

    private void UpdateShieldPositions(Vector3 centerPoint)
    {
        int count = _shieldSortedFollowers.Count;
        if (count == 0) return;

        float angleStep = 360f / count;

        for (int i = 0; i < count; i++)
        {
             // Calculate slot angle relative to fixed reference
             float angleDeg = i * angleStep;
             
             // Map i=0 to -180 + offset (back left logic from before, or just standard circle)
             // Let's align index 0 to -180 degrees relative to reference forward
             float targetAngle = -180f + (i * angleStep) + (angleStep * 0.5f); 
             
             // Apply rotation to FIXED Reference Forward
             Vector3 dir = Quaternion.Euler(0, targetAngle, 0) * _shieldReferenceForward;
             
             Vector3 targetPos = centerPoint + dir * shieldRadius;
             
             // Look away from center dynamically handled by Follower
             _shieldSortedFollowers[i].EnterShieldFormation(targetPos, centerPoint);
        }
    }

    public void DismissShield()
    {
        _shieldSortedFollowers.Clear();
        foreach (var sheep in _followers)
        {
            if (sheep != null)
            {
                sheep.ExitShieldFormation();
            }
        }
    }
}
