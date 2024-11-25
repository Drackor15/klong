using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;
using UnityEngine.InputSystem.Controls;
using System.Security.Cryptography;

public class PlayerController : NetworkBehaviour
{
    protected PlayerInputActions playerInputActions;
    protected float moveDirection;
    protected Vector2 initPosition;
    Quaternion targetArrowRotation;
    [SyncVar]
    protected GameObject playerBall;

    [SerializeField]
    protected Rigidbody2D paddleRB2D;
    [SerializeField]
    protected float moveSpeed;
    [SerializeField]
    protected SpriteRenderer ballDisplay;
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
    protected bool hasFired;

    protected float paddleRotationOffset;

    public override void OnStartAuthority() {
        playerInputActions = new PlayerInputActions();
        playerInputActions.Player.Move.Enable();
        ToggleGamepadActive();
        playerInputActions.Player.Fire.Enable();

        playerInputActions.Player.Move.performed += ctx => CmdOnMove(ctx.ReadValue<float>());
        playerInputActions.Player.Move.canceled += ctx => CmdOnMoveCanceled();
        playerInputActions.Player.Look.performed += ctx => CmdOnLook(Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue()), false);
        playerInputActions.Player.StickLook.performed += ctx => {
            if (ctx.control != null && ctx.control is StickControl stick) {
                CmdOnLook(stick.value, true);
            }
        };
        playerInputActions.Player.Fire.performed += ctx => CmdOnFire();
    }

    public override void OnStartServer() {
        base.OnStartServer();

        ServerAlignPaddle();
        initPosition = transform.position;
        isHoldingBall = true;
    }

    private void FixedUpdate() {
        if (isServer) {
            ServerMovePaddle();
            HoldBall(playerBallPrefab, initHoldBallDuration);
        }
    }

    private void Update() {
        ToggleGamepadActive();

        // Interpolate rotation
        arrowTransform.rotation = Quaternion.Slerp(
            arrowTransform.rotation,
            targetArrowRotation,
            Time.deltaTime * 10
        );
    }

    private void OnDisable() {
        playerInputActions?.Disable();
    }
    #region Events
    [Command]
    private void CmdOnMove(float val) {
        moveDirection = val;
    }

    [Command]
    private void CmdOnMoveCanceled() {
        moveDirection = 0;
    }

    /// <summary>
    /// Determines angle needed for Paddle Arrow to point at inputPosition.
    /// Compensates for any paddle rotations that may have occured at the start of the Server.
    /// For example, when <see cref="ServerAlignPaddle"/> acts upon the paddle.
    /// </summary>
    /// <param name="context"></param>
    [Command]
    private void CmdOnLook(Vector2 inputPosition, bool isGamepad) {
        Vector2 diff;

        if (isGamepad) {
            diff = inputPosition;
        }
        else {
            diff = inputPosition - (Vector2)arrowTransform.position;
        }

        // Calculate angle in degrees and normalize to the range 0-360
        float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
        angle = (angle + 360) % 360;

        // Normalize paddle rotation offset to the range 0-360
        float normalizedOffset = (paddleRotationOffset + 360) % 360;

        // Check if angle falls within the arc size, using the 0-360 system
        float minAngle = (normalizedOffset - arrowArcSize / 2 + 360) % 360;
        float maxAngle = (normalizedOffset + arrowArcSize / 2) % 360;

        // Handle wraparound in the arc range
        if (minAngle < maxAngle) {
            if (angle >= minAngle && angle <= maxAngle) {
                RpcUpdateArrowRotation(angle);
            }
        }
        else {
            // Arc crosses 360/0 boundary
            if (angle >= minAngle || angle <= maxAngle) {
                RpcUpdateArrowRotation(angle);
            }
        }
    }

    [ClientRpc]
    private void RpcUpdateArrowRotation(float angle) {
        Quaternion newRotation = Quaternion.Euler(0, 0, angle - 90);
        targetArrowRotation = newRotation;
    }

    [Command]
    private void CmdOnFire() {
        hasFired = true;
    }
    #endregion

    #region Methods
    /// <summary>
    /// Toggles between accepting gamepad input or
    /// accepting mouse input to rotate paddle arrows.
    /// Priority is given to gamepads.
    /// </summary>
    [Client]
    private void ToggleGamepadActive() {
        if (playerInputActions == null) { return; }

        if (Gamepad.current != null) {
            playerInputActions.Player.Look.Disable();
            playerInputActions.Player.StickLook.Enable();
        }
        else if (!playerInputActions.Player.Look.enabled) {
            playerInputActions.Player.StickLook.Disable();
            playerInputActions.Player.Look.Enable();
        }
    }

    /// <summary>
    /// Orients Paddles so they face the Origin.
    /// </summary>
    [Server]
    private void ServerAlignPaddle() {
        // Calculate direction from paddle's position to the origin (0, 0)
        Vector2 directionToOrigin = (Vector2.zero - (Vector2)transform.position).normalized;

        // Calculate direction from paddle to arrow anchor in local space
        Vector2 localArrowDirection = (arrowAnchorTransform.position - transform.position).normalized;

        // Calculate the angle between the paddle’s local arrow direction and the direction to the origin
        float angleToFaceOrigin = Vector2.SignedAngle(localArrowDirection, directionToOrigin);
        paddleRotationOffset = angleToFaceOrigin;

        // Apply rotation to the paddle to align the arrow anchor with the origin
        transform.rotation = Quaternion.Euler(0, 0, transform.rotation.eulerAngles.z + angleToFaceOrigin);
        RpcUpdatePaddleRotation(transform.rotation);
    }

    [ClientRpc]
    private void RpcUpdatePaddleRotation(Quaternion rotation) {
        transform.rotation = rotation;
    }

    /// <summary>
    /// Applies velocity to the paddle, which is dependant on local y-axis, movement input, and spawn position
    /// relative to the origin (spawn position implies certain rotations, which will change where the local y-axis points).
    /// </summary>
    [Server]
    private void ServerMovePaddle() {
        if (initPosition.x < 0) {
            paddleRB2D.velocity = moveDirection * moveSpeed * transform.up + (Vector3)GetCorrectiveVelocity();
        }
        else {
            paddleRB2D.velocity = moveDirection * moveSpeed * -transform.up + (Vector3)GetCorrectiveVelocity();
        }
        RpcUpdatePaddleVelocity(paddleRB2D.velocity);
    }

    [ClientRpc]
    private void RpcUpdatePaddleVelocity(Vector2 vel) {
        paddleRB2D.velocity = vel;
    }

    /// <summary>
    /// Helper function for <see cref="ServerMovePaddle"/>. Uses Vector Projection to determine the
    /// corrective force necessary to keep the paddle on its axis.
    /// Checkout (https://www.geeksforgeeks.org/vector-projection-formula/) & (https://www.youtube.com/watch?v=Rw70zkvqEiE)
    /// for more info on Vector Projection.
    /// </summary>
    /// <returns>The velocity needed to return the paddle to its artificial axis.</returns>
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

    /// <summary>
    /// Displays a ball sprite near the paddle for a given duration of time.
    /// Fires the ball once time is up or fire button is pressed.
    /// </summary>
    /// <param name="ball">Generally expects a prefab or destroyed ball</param>
    /// <param name="holdDuration">The duration of time which the ball is 'held'</param>
    [Server]
    private void HoldBall(GameObject ball, float holdDuration) {
        if (!isHoldingBall) { return; }
        if (ball == null) {
            Debug.LogError("In Method HoldBall: " + ball + " was null!");
            return;
        }
        if (ball.GetComponent<SpriteRenderer>() == null) {
            Debug.LogError("In Method HoldBall: No Sprite Render detected for " + ball + "!");
        }

        RpcDisplayHeldBall(true, ball.GetComponent<SpriteRenderer>().color);

        // Increment the timer
        holdBallTimer += Time.fixedDeltaTime;

        // Stop holding the ball after the specified duration
        if (hasFired || holdBallTimer >= holdDuration) {
            ServerFireBall(ball);
        }
    }

    [ClientRpc]
    private void RpcDisplayHeldBall(bool display, Color ballColor) {
        if (ballDisplay.enabled == display) { return; }
        if (ballColor != Color.clear) { ballDisplay.color = ballColor; }
        ballDisplay.enabled = display;
    }

    /// <summary>
    /// Helper function for <see cref="HoldBall(GameObject, float)"/>.
    /// Instantiates and spawns a ball with an applied velocity.
    /// </summary>
    /// <param name="ball"></param>
    [Server]
    private void ServerFireBall(GameObject ball) {
        GameObject tmpObj = Instantiate(ball, ballDisplay.transform.position, new Quaternion(0, 0, 0, 0));
        PlayerBall oldBallScript = ball.GetComponent<PlayerBall>();
        PlayerBall newBallScript = tmpObj.GetComponent<PlayerBall>();
        newBallScript.ServerSetOwnerID(oldBallScript, netId);

        hasFired = false;
        isHoldingBall = false;
        holdBallTimer = 0;
        RpcDisplayHeldBall(false, Color.clear); // can't have optional parameters for Rpc's >:(

        NetworkServer.Spawn(tmpObj, connectionToClient);
        newBallScript.ServerSetVelocity(GetArrowVector());
    }

    [Server]
    void OnCollisionEnter2D(Collision2D col) {
        OnBallCollision(col);
    }

    /// <summary>
    /// Applies velocity parallel to player arrow if ball collides near paddle's inner face.
    /// Otherwise a basic reflection is applied.
    /// </summary>
    /// <param name="col"></param>
    [Server]
    private void OnBallCollision(Collision2D col) {
        var ball = col.transform.GetComponent<PlayerBall>();
        if (ball == null) { return; }

        ContactPoint2D contact = col.contacts[0];
        Vector2 contactPoint = contact.point;
        Vector2 paddlePosition = transform.position;

        if (IsClosestToOrigin(contactPoint, paddlePosition)) {
            ball.ServerSetVelocity(GetArrowVector());
        }
        else {
            Vector2.Reflect(ball.ballRB2D.velocity, contact.normal);
        }
    }

    /// <summary>
    /// </summary>
    /// <returns>Vector2 of the direction in which the player arrow faces</returns>
    [Client]
    public Vector2 GetArrowVector() {
        return arrowTransform.up;
    }

    /// <summary>
    /// </summary>
    /// <param name="contactPoint"></param>
    /// <param name="paddlePosition"></param>
    /// <returns><see langword="true"/> if the first vector is closer to the origin, <see langword="false"/> otherwise</returns>
    [Server]
    private bool IsClosestToOrigin(Vector2 contactPoint, Vector2 paddlePosition) {
        return contactPoint.magnitude < paddlePosition.magnitude;
    }
    #endregion
}
