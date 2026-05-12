using UnityEngine;

/// <summary>
/// Muestra el cuerpo del jugador local (piernas + torso inferior) en VR
/// sin clipping con la cámara. Este objeto NO es el avatar de Normcore;
/// es un rig visual separado, solo visible para el jugador local.
///
/// Setup:
///  1. Crear un GameObject hijo del XR Origin (VR) llamado "LocalBodyRig".
///  2. Asignarle este script.
///  3. En xrCamera asignar Main Camera.
///  4. En bodyRoot asignar el root del modelo 3D (Doctor o Paciente).
///  5. En headRenderers asignar los renderers de cabeza/cuello/torso superior
///     que NO deben verse (se desactivan).
///  6. En visibleRenderers asignar piernas y torso inferior que sí se ven.
/// </summary>
public class LocalBodyRig : MonoBehaviour
{
    [Header("XR References")]
    [Tooltip("Transform de la cámara VR (Main Camera dentro de XR Origin).")]
    public Transform xrCamera;

    [Header("Body Visual")]
    [Tooltip("Root del modelo 3D del personaje (Doctor o Paciente).")]
    public Transform bodyRoot;

    [Tooltip("Renderers que NO deben ser visibles para el jugador local (cabeza, cuello, torso superior).")]
    public Renderer[] hiddenRenderers;

    [Tooltip("Renderers que SÍ deben ser visibles para el jugador local (piernas, torso inferior).")]
    public Renderer[] visibleRenderers;

    [Header("Positioning")]
    [Tooltip("Altura en Y desde el suelo donde se posiciona el cuerpo.")]
    [SerializeField] private float floorOffset = 0f;

    [Tooltip("Qué tan suave sigue el cuerpo al XR Origin en rotación (yaw).")]
    [SerializeField, Range(1f, 30f)] private float rotationSmoothSpeed = 10f;

    [Tooltip("Desplazamiento hacia adelante del cuerpo respecto a la cámara (para que no tape la vista).")]
    [SerializeField] private float forwardOffset = 0f;

    [Header("Animation")]
    [Tooltip("Animator del modelo local (para IDLE / Walking).")]
    public Animator bodyAnimator;

    [Tooltip("Umbral de velocidad (m/s) para activar animación de caminata.")]
    [SerializeField] private float walkThreshold = 0.1f;

    private Vector3 _lastPosition;
    private static readonly int IsWalkingHash = Animator.StringToHash("isWalking");

    // ---------------------------------------------------------------
    // Unity lifecycle
    // ---------------------------------------------------------------

    private void Start()
    {
        if (xrCamera == null && Camera.main != null)
            xrCamera = Camera.main.transform;

        _lastPosition = GetFloorPosition();
        ApplyRendererVisibility();
    }

    private void LateUpdate()
    {
        if (xrCamera == null) return;

        PositionBody();
        UpdateBodyRotation();
        UpdateAnimation();
    }

    // ---------------------------------------------------------------
    // Positioning
    // ---------------------------------------------------------------

    /// <summary>
    /// Mueve el cuerpo debajo de la cámara, alineado al suelo.
    /// </summary>
    private void PositionBody()
    {
        // La posición en el suelo bajo la cámara
        Vector3 floorPos = GetFloorPosition();

        // Aplicar offset hacia adelante (basado en la orientación yaw)
        Vector3 flatForward = Vector3.ProjectOnPlane(xrCamera.forward, Vector3.up).normalized;
        Vector3 targetPos = floorPos + flatForward * forwardOffset;
        targetPos.y = floorPos.y + floorOffset;

        transform.position = targetPos;

        // El bodyRoot (modelo 3D) se posiciona en la misma raíz
        if (bodyRoot != null)
            bodyRoot.position = transform.position;
    }

    private Vector3 GetFloorPosition()
    {
        // El XR Origin floor es el parent del Camera Offset
        // xrCamera.parent = Camera Offset
        // xrCamera.parent.parent = XR Origin (el floor)
        float floorY = 0f;
        if (xrCamera.parent != null && xrCamera.parent.parent != null)
            floorY = xrCamera.parent.parent.position.y;

        return new Vector3(xrCamera.position.x, floorY, xrCamera.position.z);
    }

    // ---------------------------------------------------------------
    // Rotation
    // ---------------------------------------------------------------

    private void UpdateBodyRotation()
    {
        Vector3 flatForward = Vector3.ProjectOnPlane(xrCamera.forward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.001f) return;

        Quaternion targetYaw = Quaternion.LookRotation(flatForward.normalized, Vector3.up);

        // Suavizar la rotación para evitar jitter
        Quaternion smoothed = Quaternion.Slerp(transform.rotation, targetYaw,
            rotationSmoothSpeed * Time.deltaTime);

        transform.rotation = smoothed;
        if (bodyRoot != null)
            bodyRoot.rotation = smoothed;
    }

    // ---------------------------------------------------------------
    // Animation
    // ---------------------------------------------------------------

    private void UpdateAnimation()
    {
        if (bodyAnimator == null) return;

        Vector3 currentPos = GetFloorPosition();
        float speed = Vector3.Distance(currentPos, _lastPosition) / Mathf.Max(Time.deltaTime, 0.0001f);
        _lastPosition = currentPos;

        bool isWalking = speed > walkThreshold;
        if (bodyAnimator.GetBool(IsWalkingHash) != isWalking)
            bodyAnimator.SetBool(IsWalkingHash, isWalking);
    }

    // ---------------------------------------------------------------
    // Renderer visibility
    // ---------------------------------------------------------------

    /// <summary>
    /// Oculta los renderers de cabeza/cuello para el jugador local
    /// y activa los de piernas/cuerpo inferior.
    /// </summary>
    private void ApplyRendererVisibility()
    {
        foreach (var r in hiddenRenderers)
            if (r != null) r.enabled = false;

        foreach (var r in visibleRenderers)
            if (r != null) r.enabled = true;
    }

    // ---------------------------------------------------------------
    // Public API
    // ---------------------------------------------------------------

    /// <summary>
    /// Permite forzar la visibilidad del cuerpo local (útil para
    /// deshabilitar temporalmente, por ejemplo, en menús).
    /// </summary>
    public void SetBodyVisible(bool visible)
    {
        foreach (var r in visibleRenderers)
            if (r != null) r.enabled = visible;
    }
}
