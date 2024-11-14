using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.Composites;

public class PlayerController : NetworkBehaviour
{
    protected Rigidbody2D rigidbody2D;
    protected PlayerInputActions playerInputActions;
    protected float moveDirection;

    [SerializeField]
    protected float moveSpeed;

    public override void OnStartAuthority() {
        rigidbody2D = GetComponent<Rigidbody2D>();

        playerInputActions = new PlayerInputActions();
        playerInputActions.Player.Move.Enable();
        playerInputActions.Player.Look.Enable();
        playerInputActions.Player.Fire.Enable();

        playerInputActions.Player.Move.performed += OnMove;
        playerInputActions.Player.Move.canceled += OnMoveCanceled;
    }

    private void OnMove(InputAction.CallbackContext context) {
        Debug.Log(context);
        moveDirection = context.ReadValue<float>();
    }

    private void OnMoveCanceled(InputAction.CallbackContext context) {
        Debug.Log(context);
        moveDirection = 0;
    }

    private void FixedUpdate() {
        if (!isLocalPlayer) return;

        rigidbody2D.velocity = new Vector2(0, moveDirection * moveSpeed);
    }

    private void OnDisable() {
        playerInputActions?.Disable();
    }
}
