using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.Composites;
using System.Linq;
using static UnityEngine.Rendering.VirtualTexturing.Debugging;

public class PlayerController : NetworkBehaviour
{
    protected Rigidbody2D paddleRB2D;
    protected PlayerInputActions playerInputActions;
    protected float moveDirection;
    protected Vector2 initPosition;
    [SyncVar]
    protected GameObject playerBall;

    [SerializeField]
    protected float moveSpeed;
    [SerializeField]
    protected Transform arrowAnchorTransform;
    [SerializeField]
    protected Transform arrowTransform;
    [SerializeField]
    [Tooltip("The size (in Deg) in which the paddle arrow can move around in")]
    [Range(10, 160)]
    protected int arrowArcSize = 160;
    [SerializeField]
    protected GameObject playerBallPrefab;
    [SerializeField]
    [Tooltip("How long (in sec) the ball should be held at the start of a game.")]
    protected float initHoldBallDuration;
    protected float holdBallTimer;
    protected bool isHoldingBall;

    protected float paddleRotationOffset;

    public override void OnStartAuthority() {
        playerInputActions = new PlayerInputActions();
        playerInputActions.Player.Move.Enable();
        playerInputActions.Player.Look.Enable();
        playerInputActions.Player.Fire.Enable();

        playerInputActions.Player.Move.performed += ctx => CmdOnMove(ctx.ReadValue<float>());
        playerInputActions.Player.Move.canceled += ctx => CmdOnMoveCanceled(ctx);
        //Debug.Log("OnStartAuthority: " + netId);
        //playerInputActions.Player.Look.performed += ctx => CmdLook(ctx);
    }

    public override void OnStartServer() {
        base.OnStartServer();

        //AlignPaddle();
        initPosition = transform.position;
        //initPosition = transform.position;
        //Debug.Log("OnStartServer: " + netId);
        //SpawnBall();
        //isHoldingBall = true;
    }

    public override void OnStartClient() {
        base.OnStartClient();

        paddleRB2D = GetComponent<Rigidbody2D>();
        //Debug.Log("OnStartClient: " + netId);
    }

    private void FixedUpdate() {
        if (isServer) {
            ServerMovePaddle();
            //CmdMovePaddle();
        }
        //HoldBall(playerBall, initHoldBallDuration);
    }

    private void OnDisable() {
        playerInputActions?.Disable();
    }

    #region Events
    [Command]
    private void CmdOnMove(float val) {
        moveDirection = val;
        //Debug.Log("CmdOnMove: " + netId);
    }

    [Command]
    private void CmdOnMoveCanceled(InputAction.CallbackContext context) {
        moveDirection = 0;
        //Debug.Log("CmdOnMoveCanceled: " + netId);
    }

    /// <summary>
    /// Paddle Arrow follows Client's mouse cursor. Compensates for any
    /// paddle rotations that may have occured at the start of Client authority.
    /// For example, when <see cref="AlignPaddle"/> acts upon the paddle.
    /// </summary>
    /// <param name="context"></param>
    //private void OnLook(InputAction.CallbackContext context) {
    //    Vector2 mousePosition = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
    //    Vector2 diff = mousePosition - (Vector2)arrowTransform.position;

    //    // Calculate angle in degrees and normalize to the range 0-360
    //    float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
    //    angle = (angle + 360) % 360;

    //    // Normalize paddle rotation offset to the range 0-360
    //    float normalizedOffset = (paddleRotationOffset + 360) % 360;

    //    // Check if angle falls within the arc size, using the 0-360 system
    //    float minAngle = (normalizedOffset - arrowArcSize / 2 + 360) % 360;
    //    float maxAngle = (normalizedOffset + arrowArcSize / 2) % 360;

    //    // Handle wraparound in the arc range
    //    if (minAngle < maxAngle) {
    //        if (angle >= minAngle && angle <= maxAngle) {
    //            arrowTransform.rotation = Quaternion.Euler(0, 0, angle - 90);
    //        }
    //    }
    //    else {
    //        // Arc crosses 360/0 boundary
    //        if (angle >= minAngle || angle <= maxAngle) {
    //            arrowTransform.rotation = Quaternion.Euler(0, 0, angle - 90);
    //        }
    //    }
    //}
    #endregion

    #region Methods
    /// <summary>
    /// Orients Client Paddles so they face the Origin.
    /// </summary>
    //private void AlignPaddle() {
    //    // Calculate direction from paddle's position to the origin (0, 0)
    //    Vector2 directionToOrigin = (Vector2.zero - (Vector2)transform.position).normalized;

    //    // Calculate direction from paddle to arrow anchor in local space
    //    Vector2 localArrowDirection = (arrowAnchorTransform.position - transform.position).normalized;

    //    // Calculate the angle between the paddle’s local arrow direction and the direction to the origin
    //    float angleToFaceOrigin = Vector2.SignedAngle(localArrowDirection, directionToOrigin);
    //    paddleRotationOffset = angleToFaceOrigin;

    //    // Apply rotation to the paddle to align the arrow anchor with the origin
    //    transform.rotation = Quaternion.Euler(0, 0, transform.rotation.eulerAngles.z + angleToFaceOrigin);
    //}

    //[Command]
    //private void SpawnBall() {
    //    GameObject tmpObj = Instantiate(playerBallPrefab, transform.position * 0.95f, new Quaternion(0, 0, 0, 0));
    //    NetworkServer.Spawn(tmpObj, connectionToClient);
    //    playerBall = tmpObj;
    //    RpcUpdatePlayerBall(playerBall);
    //}

    //[ClientRpc]
    //private void RpcUpdatePlayerBall(GameObject ball) {
    //    playerBall = ball;
    //}

    // This works - kinda. But it looks like changes to the ball are going to have to be made as Command/ClientRpc combos so that all clients see. So it's a good thing we made playerBall a SyncVar.
    // Also, the changes made to the ball are pretty jittery and don't look good, so we're going to have to smooth this math, do different math, or do something else.
    // Check out 'constraints'. Some ones to consider: FixedJoint2D, SpringJoint2D.
    //private void HoldBall(GameObject ball, float holdDuration) {
    //    if (!isHoldingBall) { return; }
    //    if (ball == null) {
    //        Debug.LogError("Method HoldBall: " + ball + " was null!");
    //        return;
    //    }

    //    // Set the ball's position directly or via velocity
    //    ball.GetComponent<Rigidbody2D>().MovePosition((Vector2)transform.position * 0.95f);

    //    // Increment the timer
    //    holdBallTimer += Time.fixedDeltaTime;
    //    Debug.Log(holdBallTimer.ToString());
    //    // Stop holding the ball after the specified duration
    //    if (holdBallTimer >= holdDuration) {
    //        isHoldingBall = false; // Stop holding
    //        // holdBallTimer = 0;
    //        // FireBall();
    //    }
    //}

    [Command]
    private void CmdMovePaddle() {
        ServerMovePaddle();
    }

    /// <summary>
    /// Applies velocity to the paddle, which is dependant on local y-axis, movement input, and spawn position
    /// relative to the origin (spawn position implies certain rotations, which will change where the local y-axis points).
    /// </summary>
    [Server]
    private void ServerMovePaddle() {
        //Debug.Log("ServerMovePaddle: " + netId);
        if (initPosition.x < 0) {
            paddleRB2D.velocity = moveDirection * moveSpeed * transform.up + (Vector3)GetCorrectiveVelocity();
        }
        else {
            paddleRB2D.velocity = moveDirection * moveSpeed * -transform.up + (Vector3)GetCorrectiveVelocity();
        }
        //Debug.Log("moveDir: " + moveDirection + " netId: " + netId);
        //Debug.Log("Vel: " + paddleRB2D.velocity + " netId: " + netId);
        RpcUpdatePaddleVelocity(paddleRB2D.velocity);
    }

    [ClientRpc]
    private void RpcUpdatePaddleVelocity(Vector2 vel) {
        paddleRB2D.velocity = vel;
    }

    /// <summary>
    /// Helper functin for <see cref="ServerMovePaddle"/>. Uses Vector Projection to determine the
    /// corrective force necessary to keep the paddle on its axis.
    /// Checkout (https://www.geeksforgeeks.org/vector-projection-formula/) & (https://www.youtube.com/watch?v=Rw70zkvqEiE)
    /// for more info on Vector Projection.
    /// </summary>
    /// <returns></returns>
    [Server]
    private Vector2 GetCorrectiveVelocity() {
        // Calculate offset and correction
        Vector2 localOffset = transform.position - (Vector3)initPosition;
        Vector2 constrainedOffset = Vector2.Dot(localOffset, transform.up) * transform.up; // Produces vector parallel to transform.up. This vect is a component of localOffset. localOffset = parallel + perpendicular
        Vector2 correction = ((Vector2)initPosition + constrainedOffset) - (Vector2)transform.position; // Finds vector equal in magnitude to the vector perpendicular to transform.up, but goes in opposite direction (-perp = -localOffset + parallel)

        // Calculate corrective velocity
        Vector2 correctiveVelocity = correction / Time.fixedDeltaTime; // that vector gives us the exact strength and direction needed to return to the axis

        return correctiveVelocity;
    }
    #endregion
}
