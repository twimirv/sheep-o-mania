using UnityEngine;

public interface ISheepLeader
{
    Transform transform { get; }
    Vector3 Velocity { get; }
    int RegisterFollower(FollowerSheepController follower);
    void UnregisterFollower(FollowerSheepController follower);
    bool IsPlayer { get; } 
    float LastJumpTime { get; } 
    // IsPlayer helps followers decide priority or specific behaviors (like HUD indicators)
}
