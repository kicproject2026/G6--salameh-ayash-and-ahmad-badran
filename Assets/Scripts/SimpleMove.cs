using UnityEngine;

public class SimpleMoveMouseLook : MonoBehaviour
{
    public float moveSpeed = 3f;
    public float mouseSensitivity = 2f;

    public Transform cameraTransform; // Drag XR Camera / Main Camera here

    private float yaw = 0f;   // left/right
    private float pitch = 0f; // up/down

    void Start()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        // Initialize yaw/pitch from current camera rotation
        Vector3 angles = cameraTransform.localEulerAngles;
        yaw = angles.y;
        pitch = angles.x;

        // Lock cursor to center and hide it
       // Cursor.lockState = CursorLockMode.Locked;
        //Cursor.visible = false;
    }

    void Update()
    {
        // ---------- MOUSE LOOK (only rotates camera, NOT the player) ----------
        if (Input.GetMouseButton(1)) // Right mouse button held
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            yaw   += mouseX * mouseSensitivity;
            pitch -= mouseY * mouseSensitivity;

            // Limit up/down look
            pitch = Mathf.Clamp(pitch, -80f, 80f);

            // Apply rotation ONLY to camera
            cameraTransform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        // ---------- MOVEMENT (WASD based on camera direction) ----------
        float horizontal = Input.GetAxisRaw("Horizontal"); // A / D
        float vertical   = Input.GetAxisRaw("Vertical");   // W / S

        // Use camera forward/right but ignore vertical tilt
        Vector3 forward = cameraTransform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 right = cameraTransform.right;
        right.y = 0f;
        right.Normalize();

        Vector3 moveDirection = (forward * vertical + right * horizontal).normalized;

        transform.position += moveDirection * moveSpeed * Time.deltaTime;
    }
}
