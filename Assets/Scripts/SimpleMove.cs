using UnityEngine;
using UnityEngine.XR; // Required for XR checking

public class SimpleMoveMouseLook : MonoBehaviour
{
    public float moveSpeed = 3f;
    public float mouseSensitivity = 2f;
    public Transform cameraTransform;

    private float yaw = 0f;
    private float pitch = 0f;
    private bool _isVRActive = false;

    void Start()
    {
        _isVRActive = CheckXRActive();

        // If VR is active, we disable this script's functionality entirely
        if (_isVRActive)
        {
            Debug.Log("VR Device detected. SimpleMoveMouseLook is standby.");
            return; 
        }

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        Vector3 angles = cameraTransform.localEulerAngles;
        yaw = angles.y;
        pitch = angles.x;
    }

    void Update()
    {
        // Safety check: Exit if VR is being used
        if (_isVRActive) return;

        // ---------- MOUSE LOOK ----------
        if (Input.GetMouseButton(1)) 
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            yaw += mouseX * mouseSensitivity;
            pitch -= mouseY * mouseSensitivity;
            pitch = Mathf.Clamp(pitch, -80f, 80f);

            cameraTransform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        // ---------- MOVEMENT ----------
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector3 forward = cameraTransform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 right = cameraTransform.right;
        right.y = 0f;
        right.Normalize();

        Vector3 moveDirection = (forward * vertical + right * horizontal).normalized;
        transform.position += moveDirection * moveSpeed * Time.deltaTime;
    }

    private bool CheckXRActive()
    {
        // Checks if a VR device is actually connected and rendering
        var xrDisplay = new System.Collections.Generic.List<XRDisplaySubsystem>();
        SubsystemManager.GetSubsystems(xrDisplay);
        foreach (var display in xrDisplay)
        {
            if (display.running) return true;
        }
        return false;
    }
}