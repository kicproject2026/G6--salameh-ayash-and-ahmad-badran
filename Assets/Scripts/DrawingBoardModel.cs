using Normal.Realtime;
using Normal.Realtime.Serialization;

/// <summary>
/// RealtimeModel de Normcore que sincroniza el estado de los trazos del tablero.
///
/// Estrategia de sincronización:
///  - Se guarda un string JSON compacto con todos los trazos ("strokes").
///  - Se usa un nonce (entero que incrementa) para forzar el re-render en
///    todos los clientes aunque el contenido no cambie.
///  - "clearNonce" se usa solo para el borrado total (evita conflictos).
///
/// Alternativa de bajo costo: si se quiere solo local, no usar este modelo
/// y deshabilitar la sincronización en DrawingBoard.cs.
/// </summary>
[RealtimeModel]
public partial class DrawingBoardModel
{
    // JSON serializado de todos los trazos activos
    [RealtimeProperty(1, true, true)] private string _strokesJson;
    // Incrementa cada vez que se modifica para forzar actualización en clientes
    [RealtimeProperty(2, true, true)] private int    _nonce;
    // Incrementa cuando el Doctor borra todo el tablero
    [RealtimeProperty(3, true, true)] private int    _clearNonce;
}
