using UnityEngine;

public class Billboard : MonoBehaviour {
    public Camera targetCamera;   // optional – can leave empty

    void LateUpdate() {
        // If no camera assigned, try to use the main camera
        if (targetCamera == null) {
            targetCamera = Camera.main;
            if (targetCamera == null) return;
        }

        // Make this object face the camera
        Vector3 dir = transform.position - targetCamera.transform.position;
        dir.y = 0f; // keep it upright (no tilting)

        if (dir.sqrMagnitude > 0.0001f) {
            transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}