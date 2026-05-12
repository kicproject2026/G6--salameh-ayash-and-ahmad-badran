using Normal.Realtime;
using UnityEngine;

/// <summary>
/// RealtimeComponent que escribe y lee PatientInfoModel.
///
/// Debe estar en el prefab del avatar (Doctor y Paciente).
/// Al ser instanciado como avatar local, escribe los datos del usuario actual.
/// Cuando el Doctor interactúa con el avatar de un Paciente, lee los datos de
/// este componente para mostrarlos en el PatientInfoWorldCanvas.
///
/// Setup en el prefab del avatar:
///  1. Agregar este componente al root del prefab.
///  2. Asegurarse de que el prefab también tenga RealtimeView.
///     (Ya lo tiene porque usa RealtimeAvatar / Normcore.)
/// </summary>
public class PatientInfoSync : RealtimeComponent<PatientInfoModel>
{
    // ---------------------------------------------------------------
    // Model replac callback — cuando el modelo Normcore está listo
    // ---------------------------------------------------------------

    protected override void OnRealtimeModelReplaced(
        PatientInfoModel previousModel,
        PatientInfoModel currentModel)
    {
        base.OnRealtimeModelReplaced(previousModel, currentModel);

        if (currentModel == null) return;

        // Si somos el propietario local, publicamos nuestros datos
        // El chequeo se hace en Start también porque el modelo puede
        // estar disponible antes o después de Start según timing de Normcore.
        TryPublishLocalData();
    }

    private void Start()
    {
        TryPublishLocalData();
    }

    // ---------------------------------------------------------------
    // Write — solo el owner escribe sus propios datos
    // ---------------------------------------------------------------

    private void TryPublishLocalData()
    {
        if (!IsOwnedLocally()) return;
        if (model == null) return;
        if (SessionData.CurrentUser == null) return;

        var user = SessionData.CurrentUser;

        model.displayName  = user.username;
        model.role         = user.role;
        model.isPatient    = user.role.Equals("Patient", System.StringComparison.OrdinalIgnoreCase);
        model.age          = user.age ?? "";
        model.patientId    = user.patientId ?? "";
        model.height       = user.height ?? "";
        model.weight       = user.weight ?? "";
        model.description  = user.description ?? "";

        Debug.Log($"[PatientInfoSync] Datos publicados para: {user.username} ({user.role})");
    }

    // ---------------------------------------------------------------
    // Read — cualquier cliente puede leer los datos del modelo
    // ---------------------------------------------------------------

    /// <summary>
    /// Retorna los datos del paciente sincronizados por Normcore.
    /// El Doctor llama esto cuando apunta al avatar del Paciente.
    /// </summary>
    public PatientDisplayData GetPatientData()
    {
        if (model == null)
            return new PatientDisplayData { displayName = "Cargando..." };

        return new PatientDisplayData
        {
            displayName = model.displayName,
            age         = model.age,
            patientId   = model.patientId,
            height      = model.height,
            weight      = model.weight,
            description = model.description,
            role        = model.role,
            isPatient   = model.isPatient
        };
    }

    public bool IsPatientAvatar => model != null && model.isPatient;

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private bool IsOwnedLocally()
    {
        try
        {
            return realtimeView != null && realtimeView.isOwnedLocallyInHierarchy;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// DTO simple para pasar datos del paciente al canvas.
/// No tiene dependencias de Normcore — puede usarse en cualquier contexto.
/// </summary>
[System.Serializable]
public struct PatientDisplayData
{
    public string displayName;
    public string age;
    public string patientId;
    public string height;
    public string weight;
    public string description;
    public string role;
    public bool   isPatient;
}
