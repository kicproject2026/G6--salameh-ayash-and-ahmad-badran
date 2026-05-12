using UnityEngine;
using TMPro;

/// <summary>
/// World Space Canvas que sigue al avatar de un Paciente y muestra
/// sus datos clínicos. Solo el Doctor puede abrirlo vía DoctorPatientInteractor.
///
/// El canvas:
///  - Sigue la posición del Paciente (con offset en Y para aparecer sobre él).
///  - Rota (billboard) para mirar siempre hacia la cámara del Doctor.
///  - Muestra Name, Age, ID, Height, Weight y Description.
///  - Puede cerrarse con un botón.
///
/// Setup:
///  1. Crear un prefab tipo World Space Canvas con los TMP_Text asignados.
///  2. Agregar este componente al root del Canvas.
///  3. Instanciar una sola copia (se reutiliza entre pacientes distintos).
/// </summary>
public class PatientInfoWorldCanvas : MonoBehaviour
{
    [Header("UI Fields — asignar en el Inspector")]
    public TMP_Text nameText;
    public TMP_Text ageText;
    public TMP_Text idText;
    public TMP_Text heightText;
    public TMP_Text weightText;
    public TMP_Text descriptionText;
    public TMP_Text roleHeaderText;

    [Header("Positioning")]
    [Tooltip("Offset en Y sobre el avatar del paciente (metros).")]
    [SerializeField] private float heightAbovePatient = 0.5f;

    [Tooltip("Suavidad del movimiento al seguir al paciente.")]
    [SerializeField, Range(1f, 20f)] private float followSmoothSpeed = 8f;

    [Tooltip("Suavidad del billboard (rotación hacia la cámara).")]
    [SerializeField, Range(1f, 20f)] private float billboardSmoothSpeed = 10f;

    // ---------------------------------------------------------------
    // State
    // ---------------------------------------------------------------

    private Transform _targetPatient;   // Transform del avatar Paciente
    private Transform _doctorCamera;    // Camera del Doctor (para billboard)
    private bool _isVisible;

    // ---------------------------------------------------------------
    // Unity lifecycle
    // ---------------------------------------------------------------

    private void Awake()
    {
        gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (!_isVisible || _targetPatient == null) return;

        FollowPatient();
        BillboardToDoctor();
    }

    // ---------------------------------------------------------------
    // Public API
    // ---------------------------------------------------------------

    /// <summary>
    /// Abre el canvas y muestra los datos del paciente objetivo.
    /// </summary>
    public void ShowForPatient(Transform patientTransform, PatientDisplayData data, Transform doctorCamera)
    {
        // Verificar que el usuario local es Doctor
        if (SessionData.CurrentUser == null ||
            !SessionData.CurrentUser.role.Equals("Doctor", System.StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning("[PatientInfoWorldCanvas] Solo el Doctor puede abrir este canvas.");
            return;
        }

        _targetPatient = patientTransform;
        _doctorCamera  = doctorCamera;

        PopulateFields(data);

        // Posición inicial correcta antes de activar (evita frame de posición incorrecta)
        if (patientTransform != null)
            transform.position = patientTransform.position + Vector3.up * heightAbovePatient;

        gameObject.SetActive(true);
        _isVisible = true;
    }

    /// <summary>
    /// Cierra el canvas. Llamado por el botón de cerrar en el prefab.
    /// </summary>
    public void Hide()
    {
        _isVisible = false;
        _targetPatient = null;
        gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------
    // Data population
    // ---------------------------------------------------------------

    private void PopulateFields(PatientDisplayData data)
    {
        if (roleHeaderText  != null) roleHeaderText.text  = "Patient Information";
        if (nameText        != null) nameText.text        = $"Name: {Fallback(data.displayName)}";
        if (ageText         != null) ageText.text         = $"Age: {Fallback(data.age)}";
        if (idText          != null) idText.text          = $"ID: {Fallback(data.patientId)}";
        if (heightText      != null) heightText.text      = $"Height: {Fallback(data.height)}";
        if (weightText      != null) weightText.text      = $"Weight: {Fallback(data.weight)}";
        if (descriptionText != null) descriptionText.text = $"Description:\n{Fallback(data.description)}";
    }

    private static string Fallback(string value) =>
        string.IsNullOrWhiteSpace(value) ? "—" : value;

    // ---------------------------------------------------------------
    // Positioning helpers
    // ---------------------------------------------------------------

    private void FollowPatient()
    {
        Vector3 target = _targetPatient.position + Vector3.up * heightAbovePatient;
        transform.position = Vector3.Lerp(transform.position, target,
            followSmoothSpeed * Time.deltaTime);
    }

    private void BillboardToDoctor()
    {
        if (_doctorCamera == null) return;

        Vector3 dirToCamera = _doctorCamera.position - transform.position;
        if (dirToCamera.sqrMagnitude < 0.001f) return;

        Quaternion targetRot = Quaternion.LookRotation(-dirToCamera.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot,
            billboardSmoothSpeed * Time.deltaTime);
    }
}
