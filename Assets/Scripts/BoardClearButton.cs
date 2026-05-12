using UnityEngine;

/// <summary>
/// Componente de botón "Clear" para el tablero de dibujo.
/// Debe estar en un botón UI (Button) dentro del World Space Canvas del tablero.
///
/// Setup:
///  1. Agregar este componente al Button de "Borrar Todo".
///  2. Asignar drawingBoard: referencia al DrawingBoard de la escena.
///  3. Conectar el evento OnClick del Button → BoardClearButton.OnClearClicked().
/// </summary>
public class BoardClearButton : MonoBehaviour
{
    [Tooltip("Referencia al componente DrawingBoard del tablero.")]
    public DrawingBoard drawingBoard;

    /// <summary>
    /// Llamado por el evento OnClick del Button de Unity UI.
    /// </summary>
    public void OnClearClicked()
    {
        if (SessionData.CurrentUser == null ||
            !SessionData.CurrentUser.role.Equals("Doctor", System.StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning("[BoardClearButton] Solo el Doctor puede borrar el tablero.");
            return;
        }

        if (drawingBoard != null)
            drawingBoard.ClearBoard();
    }
}
