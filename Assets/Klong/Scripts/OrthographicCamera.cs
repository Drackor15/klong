using System;
using System.Collections;
using System.Collections.Generic;
using TMPro.EditorUtilities;
using UnityEngine;

public class OrthographicCamera : MonoBehaviour {

    private Camera camera;

    [SerializeField]
    [Tooltip("(Optional) Parent object containing empty children that define the camera bounds.")]
    private Transform boundsParent;

    [SerializeField]
    [Tooltip("How much the camera should shrink or expand around the objects in the scene.")]
    private float buffer;

    void Start() {
        camera = GetComponent<Camera>();
    }

    void Update() {
        AdjustCamera();
    }

    private void AdjustCamera() {
        if (boundsParent == null) return;

        var (center, size) = CalculateOrthoSize();
        camera.orthographicSize = size;
        camera.transform.position = center;
    }

    private (Vector3 center, float size) CalculateOrthoSize() {
        if (boundsParent.childCount == 0) {
            Debug.LogWarning("Bounds parent has no children. Cannot calculate bounds.");
            return (transform.position, camera.orthographicSize);
        }

        var bounds = new Bounds();
        foreach (Transform child in boundsParent) {
            bounds.Encapsulate(child.position);
        }

        bounds.Expand(buffer);

        var vertical = bounds.size.y;
        var horizontal = bounds.size.x * camera.pixelHeight / camera.pixelWidth;

        var size = Mathf.Max(horizontal, vertical) * 0.5f;
        var center = bounds.center + new Vector3(0, 0, -10);
        return (center, size);
    }
}
