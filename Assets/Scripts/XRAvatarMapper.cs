using UnityEngine;
using Normal.Realtime;

public class XRAvatarMapper : MonoBehaviour {
    public RealtimeAvatar realtimeAvatar;

    public Transform xrHead;       // Main Camera
    public Transform xrLeftHand;   // Left controller
    public Transform xrRightHand;  // Right controller

    void LateUpdate() {
        if (realtimeAvatar == null || !realtimeAvatar.isOwnedLocallyInHierarchy)
            return;

        // Head follows VR camera
        if (xrHead != null && realtimeAvatar.head != null) {
            realtimeAvatar.head.position = xrHead.position;
            realtimeAvatar.head.rotation = xrHead.rotation;
        }

        // Left hand follows left controller
        if (xrLeftHand != null && realtimeAvatar.leftHand != null) {
            realtimeAvatar.leftHand.position = xrLeftHand.position;
            realtimeAvatar.leftHand.rotation = xrLeftHand.rotation;
        }

        // Right hand follows right controller
        if (xrRightHand != null && realtimeAvatar.rightHand != null) {
            realtimeAvatar.rightHand.position = xrRightHand.position;
            realtimeAvatar.rightHand.rotation = xrRightHand.rotation;
        }
    }
}
