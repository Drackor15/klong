using UnityEngine;
using Mirror;

public class PlayerGoal : NetworkBehaviour {
    [SerializeField, ReadOnly, SyncVar]
    public uint playerOwnerNetID;

    [Server]
    public void ServerSetOwnerID(uint netID) {
        playerOwnerNetID = netID;
    }
}
