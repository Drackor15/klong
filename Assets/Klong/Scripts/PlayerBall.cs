using Mirror;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerBall : NetworkBehaviour {
    [SerializeField]
    protected Rigidbody2D ballRB2D;
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
    public void ServerSetVelocity(Vector2 arrowVector) {
        ballRB2D.velocity = defaultSpeed * arrowVector;
        RpcSetVelocity(ballRB2D.velocity);
    }

    [ClientRpc]
    private void RpcSetVelocity(Vector2 velocity) {
        ballRB2D.velocity = velocity;
    }
}
