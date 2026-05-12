using Normal.Realtime;
using Normal.Realtime.Serialization;

/// <summary>
/// RealtimeModel de Normcore que sincroniza los datos del paciente entre
/// todos los clientes de la sala.
///
/// El Paciente escribe estos datos al conectarse a la sala.
/// El Doctor los lee cuando apunta al avatar del Paciente.
///
/// Campos sincronizados:
///  - displayName, age, patientId, height, weight, description
///
/// IMPORTANTE: Normcore genera el código partial de este modelo automáticamente.
/// No modificar las anotaciones [RealtimeModel] ni [RealtimeProperty].
/// Los IDs de RealtimeProperty deben ser únicos dentro de este modelo.
/// </summary>
[RealtimeModel]
public partial class PatientInfoModel
{
    [RealtimeProperty(1, true, true)] private string _displayName;
    [RealtimeProperty(2, true, true)] private string _age;
    [RealtimeProperty(3, true, true)] private string _patientId;
    [RealtimeProperty(4, true, true)] private string _height;
    [RealtimeProperty(5, true, true)] private string _weight;
    [RealtimeProperty(6, true, true)] private string _description;
    [RealtimeProperty(7, true, true)] private string _role;   // "Doctor" o "Patient"
    [RealtimeProperty(8, true, true)] private bool   _isPatient; // true si este avatar es Paciente
}
