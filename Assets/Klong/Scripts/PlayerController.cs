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
    protected Transform paddleTransform;
    [SerializeField]
    protected Transform arrowAnchorTransform;
    [SerializeField]
    protected Transform arrowTransform;
    [SerializeField]
    [Tooltip("The size (in Deg) in which the paddle arrow can move around in")]
    [Range(10, 160)]
    protected int arrowArcSize = 160;

    protected float paddleRotationOffset;

    public override void OnStartAuthority() {
        paddleRB2D = GetComponent<Rigidbody2D>();

        playerInputActions = new PlayerInputActions();
        playerInputActions.Player.Move.Enable();
        playerInputActions.Player.Look.Enable();
        playerInputActions.Player.Fire.Enable();

        playerInputActions.Player.Move.performed += OnMove;
        playerInputActions.Player.Move.canceled += OnMoveCanceled;

        playerInputActions.Player.Look.performed += OnLook;

        AlignPaddle();
    }

    private void FixedUpdate() {
        if (!isLocalPlayer) return;

        paddleRB2D.velocity = new Vector2(0, moveDirection * moveSpeed);
    }

    private void OnDisable() {
        playerInputActions?.Disable();
    }

    #region Events
    private void OnMove(InputAction.CallbackContext context) {
        Debug.Log(context);
        moveDirection = context.ReadValue<float>();
    }

    private void OnMoveCanceled(InputAction.CallbackContext context) {
        Debug.Log(context);
        moveDirection = 0;
    }

    /// <summary>
    /// Paddle Arrow follows Client's mouse cursor. Compensates for any
    /// paddle rotations that may have occured at the start of Client authority.
    /// <see cref="AlignPaddle"/>
    /// </summary>
    /// <param name="context"></param>
    private void OnLook(InputAction.CallbackContext context) {
        Vector2 mousePosition = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        Vector2 diff = mousePosition - (Vector2)arrowTransform.position;

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
                arrowTransform.rotation = Quaternion.Euler(0, 0, angle - 90);
            }
        }
        else {
            // Arc crosses 360/0 boundary
            if (angle >= minAngle || angle <= maxAngle) {
                arrowTransform.rotation = Quaternion.Euler(0, 0, angle - 90);
            }
        }
    }
    #endregion

    #region Methods
    /// <summary>
    /// Orients Client Paddles so they face the Origin.
    /// </summary>
    private void AlignPaddle() {
        // Calculate direction from paddle's position to the origin (0, 0)
        Vector2 directionToOrigin = (Vector2.zero - (Vector2)paddleTransform.position).normalized;

        // Calculate direction from paddle to arrow anchor in local space
        Vector2 localArrowDirection = (arrowAnchorTransform.position - paddleTransform.position).normalized;

        // Calculate the angle between the paddle’s local arrow direction and the direction to the origin
        float angleToFaceOrigin = Vector2.SignedAngle(localArrowDirection, directionToOrigin);
        paddleRotationOffset = angleToFaceOrigin;

        // Apply rotation to the paddle to align the arrow anchor with the origin
        paddleTransform.rotation = Quaternion.Euler(0, 0, paddleTransform.rotation.eulerAngles.z + angleToFaceOrigin);
    }
    #endregion
}
