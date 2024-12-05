using Mirror;
using Mirror.Examples;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerBall : NetworkBehaviour {
    [SerializeField]
    public Rigidbody2D ballRB2D;
    [SerializeField, ReadOnly, SyncVar]
    public uint playerOwnerNetID;
    [SerializeField, SyncVar, Min(1)]
    [Tooltip("The default speed balls should spawn in at when released by players.")]
    protected float defaultSpeed;

    [Server]
    public void ServerSetOwnerID(uint netID) {
        if (playerOwnerNetID == 0) {
            playerOwnerNetID = netID;
        }
    }

    [Server]
    public PlayerController GetPlayerController(uint netID) {
        if (NetworkServer.spawned.TryGetValue(netID, out NetworkIdentity networkIdentity)) {
            return networkIdentity.gameObject.GetComponent<PlayerController>();
        }
        return null;
    }

    private void FixedUpdate() {
        if (isServer) {
            ServerClampBallVelocity();
        }
    }

    [Server]
    void OnCollisionEnter2D(Collision2D col) {
        OnGoalCollide(col);
        OnBallCollide(col);
    }

    [Server]
    private void OnBallCollide(Collision2D col) {
        if (col.transform.GetComponent<PlayerBall>() == null) { return; }

        Vector2.Reflect(ballRB2D.velocity, col.GetContact(0).normal);
    }

    [Server]
    private void OnGoalCollide(Collision2D col) {
        // This may change if we have powers that change ball dmg...
        if (!col.transform.name.Contains("Goal")) { return; }

        uint goalOwnerNetID = col.transform.GetComponent<PlayerGoal>().playerOwnerNetID;
        if (playerOwnerNetID == goalOwnerNetID) {
            GetPlayerController(goalOwnerNetID).ServerAddHP(-5);
        }
        else {
            GetPlayerController(goalOwnerNetID).ServerAddHP(-10);
        }
        GetPlayerController(playerOwnerNetID).ServerSetIsHoldingBall(true);
        NetworkServer.UnSpawn(gameObject);
        PrefabPool.singleton.Return(PrefabPool.singleton.GetPooledPrefab("Ball"), gameObject);

        // The notes below should probably be handled in the playercontroller script
        // Then Check if player is dead. If so, do death stuff and let server and other clients know.
        // server should have some trigger after a player has died, to check if 1 player remains. If so, then do end game stuff.
    }

    [Server]
    private void ServerClampBallVelocity() {
        if (ballRB2D.velocity.magnitude == 0f) {
            ballRB2D.velocity = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized * defaultSpeed;
        }
        else {
            ballRB2D.velocity = Vector2.ClampMagnitude(ballRB2D.velocity, defaultSpeed);
        }
    }

    [Server]
    public void ServerSetVelocity(Vector2 arrowVector) {
        // Cases that we need to modify this for:
        // Release from start or respawning ball: vel = default * arrowVector
        // Release from catching an existing ball: vel = prevVel * arrowVector (retain the magnitude, but not the direction of the prevVel)
        // Collides with Paddle + PowerFast is used: vel = prevVel * %spedUp * arrowVector
        // Collides with Paddle + PowerSlow is used: vel = prevVel * %slowDown * arrowVector
        ballRB2D.velocity = defaultSpeed * arrowVector;
        RpcSetVelocity(ballRB2D.velocity);
    }

    [ClientRpc]
    private void RpcSetVelocity(Vector2 velocity) {
        ballRB2D.velocity = velocity;
    }
}
