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

    void OnCollisionEnter2D(Collision2D col) {
        if (!isServer) { return; }
        OnGoalCollide(col);
        OnBallCollide(col);
        OnWallCollide(col);
        OnPaddleCollide(col);
    }

    [Server]
    private void OnWallCollide(Collision2D col) {
        if (!col.transform.name.Contains("Wall")) { return; }

        ServerSetVelocity(Vector2.Reflect(ballRB2D.velocity, col.GetContact(0).normal).normalized);
    }

    [Server]
    private void OnBallCollide(Collision2D col) {
        if (col.transform.GetComponent<PlayerBall>() == null) { return; }

        ServerSetVelocity(Vector2.Reflect(ballRB2D.velocity, col.GetContact(0).normal).normalized);
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

    /// <summary>
    /// Applies velocity parallel to player arrow if ball collides near paddle's inner face.
    /// Otherwise a basic reflection is applied.
    /// </summary>
    /// <param name="col"></param>
    [Server]
    private void OnPaddleCollide(Collision2D col) {
        var paddle = col.transform.GetComponent<PlayerController>();
        if (paddle == null) { return; }

        ContactPoint2D contact = col.GetContact(0);
        Vector2 contactPoint = contact.point;
        Vector2 paddlePosition = col.transform.position;

        if (paddle.IsClosestToOrigin(contactPoint, paddlePosition)) {
            ServerSetVelocity(paddle.GetArrowVector());
        }
        else {
            ServerSetVelocity(Vector2.Reflect(ballRB2D.velocity, contact.normal).normalized);
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
