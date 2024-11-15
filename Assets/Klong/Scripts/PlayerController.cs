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

    [SerializeField]
    protected float moveSpeed;
    [SerializeField]
    [SyncVar]
    protected Transform paddleTransform;
    [SerializeField]
    protected Transform arrowAnchorTransform;
    [SerializeField]
    protected Transform arrowTransform;
    [SerializeField]
    [Tooltip("The size (in Deg) in which the paddle arrow can move around in")]
    protected int arrowArcSize = 160;

    public override void OnStartAuthority() {
        paddleRB2D = GetComponent<Rigidbody2D>();

        playerInputActions = new PlayerInputActions();
        playerInputActions.Player.Move.Enable();
        playerInputActions.Player.Look.Enable();
        playerInputActions.Player.Fire.Enable();

        playerInputActions.Player.Move.performed += OnMove;
        playerInputActions.Player.Move.canceled += OnMoveCanceled;

        playerInputActions.Player.Look.performed += OnLook;
    }

    private void OnMove(InputAction.CallbackContext context) {
        Debug.Log(context);
        moveDirection = context.ReadValue<float>();
    }

    private void OnMoveCanceled(InputAction.CallbackContext context) {
        Debug.Log(context);
        moveDirection = 0;
    }

    [Command]
    private void OnLook(InputAction.CallbackContext context) {
        Vector2 mousePosition = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        Vector2 diff = mousePosition - (Vector2)arrowTransform.position;

        float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
        if (Mathf.Abs(angle) <= arrowArcSize/2) {
            arrowTransform.rotation = Quaternion.Euler(0, 0, angle - 90);
        }
    }

    public override void OnStartServer() {
        base.OnStartServer();

        if (isServer) {
            AlignPaddle();
        }
    }

    private void AlignPaddle() {
        // Calculate direction from paddle's position to the origin (0, 0)
        Vector2 directionToOrigin = (Vector2.zero - (Vector2)paddleTransform.position).normalized;

        // Calculate direction from paddle to arrow anchor in local space
        Vector2 localArrowDirection = (arrowAnchorTransform.position - paddleTransform.position).normalized;

        // Calculate the angle between the paddle’s local arrow direction and the direction to the origin
        float angleToFaceOrigin = Vector2.SignedAngle(localArrowDirection, directionToOrigin);
        
        // Apply rotation to the paddle to align the arrow anchor with the origin
        paddleTransform.rotation = Quaternion.Euler(0, 0, paddleTransform.rotation.eulerAngles.z + angleToFaceOrigin);
    }

    private void FixedUpdate() {
        if (!isLocalPlayer) return;

        paddleRB2D.velocity = new Vector2(0, moveDirection * moveSpeed);
    }

    private void OnDisable() {
        playerInputActions?.Disable();
    }
}
