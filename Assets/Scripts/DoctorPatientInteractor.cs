using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Sistema de interacción Doctor → Paciente.
/// Cuando el Doctor apunta a un avatar con rol Paciente e interactúa,
/// abre el PatientInfoWorldCanvas con los datos de ese paciente.
///
/// Setup en el XR Origin (VR):
///  1. Agregar este componente al root del XR Origin (VR).
///  2. Asignar doctorCamera (Main Camera).
///  3. Asignar patientInfoCanvas (instancia única en la escena del prefab PatientInfoCanvas).
///  4. El interactAction puede ser el trigger derecho o un botón secundario.
///  5. El sistema también detecta automáticamente si los Ray Interactors de las manos están apuntando al paciente.
///  6. maxRayDistance: distancia máxima de detección en metros (default 5m).
///  6. patientLayer: Layer mask de los avatares (o usar "Default" si no tienen layer propio).
/// </summary>
public class DoctorPatientInteractor : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Transform de la cámara del Doctor (Main Camera dentro del XR Origin).")]
    public Transform doctorCamera;

    [Tooltip("Instancia del PatientInfoWorldCanvas en la escena.")]
    public PatientInfoWorldCanvas patientInfoCanvas;

    [Header("Raycast")]
    [Tooltip("Distancia máxima del raycast para detectar avatares de Paciente.")]
    [SerializeField] private float maxRayDistance = 5f;

    [Tooltip("Layer mask que incluye los avatares. Dejar en Everything si los avatares están en Default layer.")]
    [SerializeField] private LayerMask patientLayerMask = ~0;

    [Header("Input")]
    [Tooltip("Acción de input para abrir/cerrar el canvas del paciente. " +
             "Default: Grip del controlador derecho. " +
             "También puede asignarse como InputActionReference desde el Inspector.")]
    public InputActionReference openCanvasAction;

    [Tooltip("Fallback de teclado para debugging (tecla I).")]
    [SerializeField] private bool enableKeyboardFallback = true;

    // ---------------------------------------------------------------
    // Private state
    // ---------------------------------------------------------------

    private InputAction _runtimeAction;
    private InputAction ActionToUse => (openCanvasAction?.action != null)
        ? openCanvasAction.action
        : _runtimeAction;

    private bool _canvasOpen;
    private Transform _currentPatientTarget;

    // ---------------------------------------------------------------
    // Unity lifecycle
    // ---------------------------------------------------------------

    private void Awake()
    {
        // Fallback: crear acción en runtime si no hay referencia asignada
        if (openCanvasAction == null)
        {
            _runtimeAction = new InputAction("OpenPatientCanvas", InputActionType.Button);
            _runtimeAction.AddBinding("<XRController>{RightHand}/gripButton");
            if (enableKeyboardFallback)
                _runtimeAction.AddBinding("<Keyboard>/i");
        }

        if (doctorCamera == null && Camera.main != null)
            doctorCamera = Camera.main.transform;
    }

    private void OnEnable()
    {
        var action = ActionToUse;
        if (action != null)
        {
            action.Enable();
            action.performed += OnInteractPerformed;
        }
    }

    private void OnDisable()
    {
        var action = ActionToUse;
        if (action != null)
        {
            action.performed -= OnInteractPerformed;
            action.Disable();
        }
        _runtimeAction?.Dispose();
    }

    private void Update()
    {
        // Si el canvas está abierto y el paciente objetivo ya no existe, cerrar
        if (_canvasOpen && (_currentPatientTarget == null ||
                            !_currentPatientTarget.gameObject.activeInHierarchy))
        {
            CloseCanvas();
            return;
        }

        // Proactividad: Si el Doctor apunta y presiona Select (Trigger) o Click Izquierdo, intentar abrir
        if (!_canvasOpen && IsDoctorLocal())
        {
            // VR
            CheckInteractorsForOpen();

            // PC (Mouse)
            if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
            {
                Ray ray = Camera.main.ScreenPointToRay(UnityEngine.InputSystem.Mouse.current.position.ReadValue());
                if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, patientLayerMask))
                {
                    TryProcessHit(hit);
                }
            }
        }
    }

    private void CheckInteractorsForOpen()
    {
        // 1. Prioridad: NearFarInteractor
        var nearFarInteractors = GameObject.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor>(FindObjectsSortMode.None);
        foreach (var interactor in nearFarInteractors)
        {
            if (CheckNearFarInteractorForOpen(interactor)) return;
        }

        // 2. Fallback: XRRayInteractor
        var rayInteractors = GameObject.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>(FindObjectsSortMode.None);
        foreach (var interactor in rayInteractors)
        {
            if (interactor is UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor) continue;
            if (CheckRayInteractorForOpen(interactor)) return;
        }
    }

    private bool CheckNearFarInteractorForOpen(UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor interactor)
    {
        if (interactor.isSelectActive)
        {
            Ray ray = new Ray(interactor.transform.position, interactor.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, patientLayerMask))
            {
                if (TryProcessHit(hit)) return true;
            }
        }
        return false;
    }

    private bool CheckNearFarInteractor(UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor interactor, out Vector3 hitPoint)
    {
        hitPoint = Vector3.zero;
        // NearFarInteractor en XRI 3.x a veces no expone TryGetCurrent3DRaycastHit directamente.
        // Usamos un raycast manual desde su posición/dirección contra nuestro collider.
        Ray ray = new Ray(interactor.transform.position, interactor.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, patientLayerMask))
        {
            bool isSelecting = interactor.isSelectActive;
            if (isSelecting) { hitPoint = hit.point; return true; }
        }
        return false;
    }

    private bool CheckRayInteractorForOpen(UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor interactor)
    {
        if (interactor.isSelectActive && interactor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            if (TryProcessHit(hit)) return true;
        }
        return false;
    }

    private bool IsDoctorLocal()
    {
        return SessionData.CurrentUser != null &&
               SessionData.CurrentUser.role.Equals("Doctor", System.StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------
    // Input handler
    // ---------------------------------------------------------------

    private void OnInteractPerformed(InputAction.CallbackContext ctx)
    {
        // Solo el Doctor puede usar esto
        if (!IsDoctorLocal()) return;

        // Si ya hay un canvas abierto, cerrarlo
        if (_canvasOpen)
        {
            CloseCanvas();
            return;
        }

        TryOpenCanvas();
    }

    // ---------------------------------------------------------------
    // Raycast logic
    // ---------------------------------------------------------------

    private void TryOpenCanvas()
    {
        if (doctorCamera == null) return;

        Ray ray = new Ray(doctorCamera.position, doctorCamera.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, patientLayerMask))
        {
            if (TryProcessHit(hit)) return;
        }

        // Fallback: Chequear los Ray Interactors de las manos (XRIT)
        var rayInteractors = GameObject.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>(FindObjectsSortMode.None);
        foreach (var interactor in rayInteractors)
        {
            if (interactor.TryGetCurrent3DRaycastHit(out RaycastHit rayHit))
            {
                if (TryProcessHit(rayHit)) return;
            }
        }
    }

    private bool TryProcessHit(RaycastHit hit)
    {
        // Buscar PatientInfoSync en el objeto golpeado y sus padres
        PatientInfoSync patientSync = hit.collider.GetComponentInParent<PatientInfoSync>();
        if (patientSync == null) return false;

        // Verificar que el avatar es un Paciente
        if (!patientSync.IsPatientAvatar)
        {
            Debug.Log("[DoctorPatientInteractor] Avatar detectado pero no es Paciente.");
            return false;
        }

        PatientDisplayData data = patientSync.GetPatientData();
        _currentPatientTarget = patientSync.transform;

        if (patientInfoCanvas != null)
        {
            patientInfoCanvas.ShowForPatient(_currentPatientTarget, data, doctorCamera);
            _canvasOpen = true;
            Debug.Log($"[DoctorPatientInteractor] Canvas abierto para: {data.displayName}");
            return true;
        }
        return false;
    }

    private void CloseCanvas()
    {
        if (patientInfoCanvas != null)
            patientInfoCanvas.Hide();

        _canvasOpen = false;
        _currentPatientTarget = null;
    }

    // ---------------------------------------------------------------
    // Gizmos para debugging en Editor
    // ---------------------------------------------------------------

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (doctorCamera == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(doctorCamera.position,
            doctorCamera.forward * maxRayDistance);
    }
#endif
}
