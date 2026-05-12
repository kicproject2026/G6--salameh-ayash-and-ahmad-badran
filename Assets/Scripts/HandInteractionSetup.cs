using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Gestiona el sistema de manos VR:
///  - Detecta si hand tracking está disponible en el runtime.
///  - Activa el rig de hand tracking (XR Hands) cuando está disponible.
///  - Activa los controladores visuales como fallback cuando no hay hand tracking.
///
/// Setup en el prefab XR Origin (VR):
///  1. Agregar este componente al GameObject raíz XR Origin (VR).
///  2. Asignar handTrackingRig: el GameObject con los interactors de hand tracking.
///     (Puede ser el "HandTrackingRig" creado desde XR Hands HandVisualizer sample,
///      o el sub-rig de manos del XRIT Starter Assets XR Origin (XR Rig).prefab)
///  3. Asignar controllerRig: el GameObject con los interactors de controladores.
///  4. El sistema revisará el estado cada checkInterval segundos.
/// </summary>
public class HandInteractionSetup : MonoBehaviour
{
    [Header("Rigs")]
    [Tooltip("GameObject que contiene los interactors y visuales de hand tracking.")]
    public GameObject handTrackingRig;

    [Tooltip("GameObject que contiene los interactors y visuales de controladores físicos.")]
    public GameObject controllerRig;

    [Header("Settings")]
    [Tooltip("Segundos entre cada chequeo de disponibilidad de hand tracking.")]
    [SerializeField] private float checkInterval = 1f;

    [Tooltip("Si está activo, muestra logs en Console para debugging.")]
    [SerializeField] private bool debugLogs = true;

    [Tooltip("Fuerza el uso de Hand Tracking en el Editor, ignorando el XR Device Simulator.")]
    [SerializeField] private bool forceHandTrackingInEditor = false;

    private float _nextCheckTime;
    private bool _lastHandTrackingState = false;

    // ---------------------------------------------------------------
    // Unity lifecycle
    // ---------------------------------------------------------------

    private void Start()
    {
        // Forzar un chequeo inicial al inicio
        _nextCheckTime = 0f;
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextCheckTime) return;
        _nextCheckTime = Time.unscaledTime + checkInterval;

        bool handTrackingAvailable = IsHandTrackingAvailable();

        if (handTrackingAvailable != _lastHandTrackingState)
        {
            _lastHandTrackingState = handTrackingAvailable;
            ApplyRigState(handTrackingAvailable);
        }
    }

    // ---------------------------------------------------------------
    // Detection
    // ---------------------------------------------------------------

    /// <summary>
    /// Detecta si el subsistema de hand tracking está activo y disponible.
    /// Funciona con Meta Quest (OpenXR + Hand Tracking extension) y
    /// con cualquier runtime compatible con XRHandSubsystem.
    /// </summary>
    private bool IsHandTrackingAvailable()
    {
        if (Application.isEditor && forceHandTrackingInEditor)
        {
            return true;
        }

        // En el Editor, si el XR Device Simulator está presente y activo, priorizamos controladores
        if (Application.isEditor)
        {
            if (GameObject.Find("XR Device Simulator") != null || GameObject.Find("XR Device Simulator (New)") != null)
            {
                // Si el simulador está presente, no forzamos hand tracking a menos que se detecte activamente
                // Por ahora, devolvemos false para asegurar que los controladores funcionen en el simulador.
                return false;
            }
        }

        // Método 1: chequear InputDevices con HandTracking capability
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.HandTracking | InputDeviceCharacteristics.Left,
            devices);

        if (devices.Count > 0 && devices[0].isValid)
        {
            if (debugLogs) Debug.Log("[HandInteractionSetup] Hand tracking detectado vía InputDevices.");
            return true;
        }

        // Método 2: chequear XRHandSubsystem (requiere com.unity.xr.hands)
#if UNITY_XR_HANDS
        var handSubsystems = new List<UnityEngine.XR.Hands.XRHandSubsystem>();
        SubsystemManager.GetSubsystems(handSubsystems);
        foreach (var subsystem in handSubsystems)
        {
            if (subsystem.running)
            {
                if (debugLogs) Debug.Log("[HandInteractionSetup] Hand tracking detectado vía XRHandSubsystem.");
                return true;
            }
        }
#endif

        return false;
    }

    // ---------------------------------------------------------------
    // Activation
    // ---------------------------------------------------------------

    private void ApplyRigState(bool useHandTracking)
    {
        if (Application.isEditor && !useHandTracking)
        {
            // En el editor, nos aseguramos de que los controladores estén activos si no hay manos
            if (debugLogs) Debug.Log("[HandInteractionSetup] Forzando activación de Controladores en Editor.");
        }

        if (handTrackingRig != null)
        {
            if (debugLogs) Debug.Log($"[HandInteractionSetup] {(useHandTracking ? "Activando" : "Desactivando")} HandTrackingRig: {handTrackingRig.name}");
            handTrackingRig.SetActive(useHandTracking);
        }

        if (controllerRig != null)
        {
            if (debugLogs) Debug.Log($"[HandInteractionSetup] {(!useHandTracking ? "Activando" : "Desactivando")} ControllerRig: {controllerRig.name}");
            controllerRig.SetActive(!useHandTracking);
        }

        if (debugLogs)
            Debug.Log($"[HandInteractionSetup] Estado final aplicado. HandTracking: {useHandTracking}");
    }

    // ---------------------------------------------------------------
    // Public API
    // ---------------------------------------------------------------

    /// <summary>
    /// Fuerza activación del modo controladores (útil para debugging o
    /// cuando el hardware no soporta hand tracking).
    /// </summary>
    public void ForceControllerMode()
    {
        _lastHandTrackingState = false;
        ApplyRigState(false);
    }

    /// <summary>
    /// Fuerza activación del modo hand tracking (útil para testing).
    /// </summary>
    public void ForceHandTrackingMode()
    {
        _lastHandTrackingState = true;
        ApplyRigState(true);
    }
}
