using Mirror;
using System.Collections;
using System.Collections.Generic;
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
    public void ServerSetOwnerID(PlayerBall oldBallScript, uint netID) {
        if (oldBallScript.playerOwnerNetID != 0) {
            playerOwnerNetID = oldBallScript.playerOwnerNetID;
        }
        else {
            playerOwnerNetID = netID;
        }
    }

    [Server]
    void OnCollisionEnter2D(Collision2D col) {
        if (col.transform.GetComponent<PlayerBall>() == null) { return; }

        // Reflect the current ball's velocity
        Vector2.Reflect(ballRB2D.velocity, col.GetContact(0).normal);

        // Reflect the other ball's velocity
        //otherBall.ServerSetVelocity(Vector2.Reflect(otherBall.ballRB2D.velocity, -col.contacts[0].normal));
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
