using UnityEngine;
using Normal.Realtime;

public class NormcoreAvatarSync : RealtimeComponent<RealtimeAvatarModel> {
    [Header("Avatar Parts")]
    public Transform head;
    public Transform leftHand;
    public Transform rightHand;

    private Transform _xrCamera;
    private Transform _xrLeftHand;
    private Transform _xrRightHand;

    private void Start() {
        if (realtimeView.isOwnedLocallyInHierarchy) {
            FindXRNodes();
            HideAvatarLocally();
        }
    }

    private void HideAvatarLocally() {
        foreach (var r in GetComponentsInChildren<Renderer>()) {
            r.enabled = false;
        }
    }

    private void FindXRNodes() {
        _xrCamera = Camera.main.transform;
        _xrLeftHand = GameObject.Find("Left Controller")?.transform;
        _xrRightHand = GameObject.Find("Right Controller")?.transform;
    }

    // private void LateUpdate() { // LateUpdate is smoother for VR head tracking
    //     if (!realtimeView.isOwnedLocallyInHierarchy || _xrCamera == null) return;

    //     // 1. Calculate Floor Height
    //     float floorY = -2.1f; 
    //     if (_xrCamera.parent != null && _xrCamera.parent.parent != null) {
    //         floorY = _xrCamera.parent.parent.position.y;
    //     }

    //     // 2. Sync Root Position & Rotation
    //     transform.position = new Vector3(_xrCamera.position.x, floorY, _xrCamera.position.z);
    //     transform.rotation = Quaternion.Euler(0, _xrCamera.eulerAngles.y, 0);

    //     // 3. Sync Limbs
    //     if (head) {
    //         // head.position = _xrCamera.position;
    //         head.localPosition = Vector3.zero;
    //         head.rotation = _xrCamera.rotation;
    //     }

    //     SyncHand(leftHand, _xrLeftHand);
    //     SyncHand(rightHand, _xrRightHand);
    // }

    private void LateUpdate() {
    // 1. FLOOR SYNC (Only the owner determines where the floor is)
        if (realtimeView.isOwnedLocallyInHierarchy) {
            if (_xrCamera == null) return;

            float floorY = -2.1f; 
            if (_xrCamera.parent != null && _xrCamera.parent.parent != null) {
                floorY = _xrCamera.parent.parent.position.y;
            }

            transform.position = new Vector3(_xrCamera.position.x, floorY, _xrCamera.position.z);
            transform.rotation = Quaternion.Euler(0, _xrCamera.eulerAngles.y, 0);

            // Sync the actual hardware nodes
            if (head) head.rotation = _xrCamera.rotation;
            SyncHand(leftHand, _xrLeftHand);
            SyncHand(rightHand, _xrRightHand);
        }

        // 2. POSITION ENFORCEMENT (Runs for EVERYONE, including the Unity Player)
        // This overrides the "World Position" sync coming from the network
        if (head != null) {
            // By forcing LocalPosition to zero for everyone, we ensure the feet 
            // (the pivot) stay at the Root (the floor) on every screen.
            head.localPosition = Vector3.zero;
        }
    }

    private void SyncHand(Transform avatarHand, Transform xrController) {
        if (avatarHand && xrController) {
            avatarHand.position = xrController.position;
            avatarHand.rotation = xrController.rotation;
        }
    }
}