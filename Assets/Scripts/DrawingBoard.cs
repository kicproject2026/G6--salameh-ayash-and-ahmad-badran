using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Normal.Realtime;

/// <summary>
/// Tablero de dibujo interactivo. Solo el Doctor puede dibujar.
///
/// Funcionalidades:
///  - Dibujo con lápiz rojo o negro usando Ray Interactor o puntero de mano.
///  - Borrado total con botón Clear.
///  - Sincronización de trazos por Normcore (todos los usuarios ven el dibujo).
///  - Role guard: solo Doctor puede activar el modo de dibujo.
///  - Optimización: no sincroniza cada frame, solo al finalizar un trazo.
///
/// Setup:
///  1. Crear un GameObject plano (Plane o Quad) que represente el tablero.
///  2. Agregar este componente al root del tablero.
///  3. Agregar un RealtimeView al tablero.
///  4. Crear el Canvas de botones (color rojo, negro, clear) y asignarlos.
///  5. drawingCamera: la cámara principal del Doctor.
///  6. boardCollider: el Collider del tablero (para raycast).
///  7. lineRendererPrefab: prefab de LineRenderer con material adecuado.
/// </summary>
[RequireComponent(typeof(RealtimeView))]
public class DrawingBoard : RealtimeComponent<DrawingBoardModel>
{
    [Header("References")]
    [Tooltip("Cámara principal del usuario (para raycast de dibujo con controlador/mano).")]
    public Camera drawingCamera;

    [Tooltip("Collider del tablero (Plane o Quad).")]
    public Collider boardCollider;

    [Tooltip("Prefab de LineRenderer para renderizar trazos.")]
    public LineRenderer lineRendererPrefab;

    [Header("Drawing Settings")]
    [SerializeField] private Color redColor   = Color.red;
    [SerializeField] private Color blackColor = Color.black;
    [SerializeField, Range(0.001f, 0.05f)] private float lineWidth = 0.005f;

    [Tooltip("Distancia mínima entre puntos para evitar sobrecarga (metros).")]
    [SerializeField] private float minPointDistance = 0.005f;

    [Header("Input")]
    [Tooltip("Acción de dibujo (mantener presionado para dibujar). " +
             "Default: Trigger del controlador derecho.")]
    public InputActionReference drawAction;

    [Tooltip("Habilitar teclado fallback para pruebas en editor (mantener tecla D).")]
    [SerializeField] private bool enableKeyboardFallback = true;

    [Header("Networking")]
    [Tooltip("Si es false, el dibujo es solo local (no se sincroniza por Normcore).")]
    [SerializeField] private bool syncOverNetwork = true;

    // ---------------------------------------------------------------
    // State
    // ---------------------------------------------------------------

    private Color          _currentColor;
    private bool           _isDrawing;
    private DrawingStroke  _currentStroke;
    private LineRenderer   _activeLineRenderer;
    private Vector3        _lastDrawPoint;

    private StrokesData    _allStrokes = new StrokesData();
    private List<LineRenderer> _renderedLines = new List<LineRenderer>();

    private InputAction _runtimeDrawAction;
    private InputAction DrawActionToUse => (drawAction?.action != null)
        ? drawAction.action
        : _runtimeDrawAction;

    private int _lastClearNonce = -1;
    private int _lastNonce      = -1;

    // ---------------------------------------------------------------
    // Unity lifecycle
    // ---------------------------------------------------------------

    private void Awake()
    {
        _currentColor = blackColor;

        if (drawAction == null)
        {
            _runtimeDrawAction = new InputAction("DrawOnBoard", InputActionType.Button);
            _runtimeDrawAction.AddBinding("<XRController>{RightHand}/trigger");
            if (enableKeyboardFallback)
                _runtimeDrawAction.AddBinding("<Keyboard>/d");
        }

        if (drawingCamera == null && Camera.main != null)
            drawingCamera = Camera.main;
    }

    private void OnEnable()
    {
        DrawActionToUse?.Enable();
    }

    private void OnDisable()
    {
        DrawActionToUse?.Disable();
        _runtimeDrawAction?.Dispose();

        if (_isDrawing)
            FinalizeStroke();
    }

    private void Update()
    {
        if (!IsDoctorLocal()) return;

        // Buscamos si algún Ray Interactor está seleccionando el tablero
        Vector3 hitPoint;
        bool selectingBoard = CheckInteractorsDrawing(out hitPoint);

        // Fallback: si no hay interactores activos o no detectan selección, usamos el input action directo o el mouse
        if (!selectingBoard)
        {
            bool pressing = (DrawActionToUse != null && DrawActionToUse.IsPressed());
            
            // Si estamos en PC/Editor, también chequeamos el clic izquierdo
            if (!pressing && UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.isPressed)
            {
                pressing = true;
            }

            // Si se detecta presión (gatillo o click), hacemos un raycast manual para validar el tablero
            if (pressing && RaycastBoard(out hitPoint))
            {
                selectingBoard = true;
            }
        }

        if (selectingBoard)
        {
            if (!_isDrawing) BeginStroke(hitPoint);
            else             ContinueStroke(hitPoint);
        }
        else if (_isDrawing)
        {
            FinalizeStroke();
        }
    }

    private bool CheckInteractorsDrawing(out Vector3 hitPoint)
    {
        hitPoint = Vector3.zero;
        
        // 1. Buscamos NearFarInteractor (Nuevo estándar en XRI 3.x)
        var nearFarInteractors = GameObject.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor>(FindObjectsSortMode.None);
        foreach (var interactor in nearFarInteractors)
        {
            if (CheckNearFarInteractor(interactor, out hitPoint)) return true;
        }

        // 2. Fallback a XRRayInteractor (Clásico)
        var rayInteractors = GameObject.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>(FindObjectsSortMode.None);
        foreach (var interactor in rayInteractors)
        {
            // Evitamos procesar si es un NearFarInteractor (ya que hereda de XRRayInteractor en algunas versiones)
            if (interactor is UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor) continue;
            if (CheckRayInteractor(interactor, out hitPoint)) return true;
        }

        return false;
    }

    private bool CheckNearFarInteractor(UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor interactor, out Vector3 hitPoint)
    {
        hitPoint = Vector3.zero;
        Ray ray = new Ray(interactor.transform.position, interactor.transform.forward);
        if (boardCollider.Raycast(ray, out RaycastHit hit, 10f))
        {
            // Usamos isSelectActive que funciona si el tablero tiene XRSimpleInteractable
            // O verificamos si el gatillo está presionado globalmente
            bool isTriggered = interactor.isSelectActive || (DrawActionToUse != null && DrawActionToUse.IsPressed());

            if (isTriggered) 
            { 
                hitPoint = hit.point; 
                return true; 
            }
        }
        return false;
    }

    private bool CheckRayInteractor(UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor interactor, out Vector3 hitPoint)
    {
        hitPoint = Vector3.zero;
        Ray ray = new Ray(interactor.transform.position, interactor.transform.forward);
        if (boardCollider.Raycast(ray, out RaycastHit hit, 10f))
        {
            bool isTriggered = interactor.isSelectActive || (DrawActionToUse != null && DrawActionToUse.IsPressed());

            if (isTriggered) 
            { 
                hitPoint = hit.point; 
                return true; 
            }
        }
        return false;
    }

    // ---------------------------------------------------------------
    // Normcore model callbacks
    // ---------------------------------------------------------------

    protected override void OnRealtimeModelReplaced(
        DrawingBoardModel previousModel,
        DrawingBoardModel currentModel)
    {
        base.OnRealtimeModelReplaced(previousModel, currentModel);

        if (previousModel != null)
        {
            previousModel.nonceDidChange      -= OnNonceChanged;
            previousModel.clearNonceDidChange -= OnClearNonceChanged;
        }

        if (currentModel != null)
        {
            currentModel.nonceDidChange      += OnNonceChanged;
            currentModel.clearNonceDidChange += OnClearNonceChanged;

            // Reconstruir trazos al entrar a la sala
            RebuildAllStrokes();
        }
    }

    private void OnNonceChanged(DrawingBoardModel m, int value)
    {
        if (IsOwnedLocally()) return; // ya lo aplicamos localmente
        RebuildAllStrokes();
    }

    private void OnClearNonceChanged(DrawingBoardModel m, int value)
    {
        ClearLocalRenderers();
        _allStrokes = new StrokesData();
    }

    // ---------------------------------------------------------------
    // Drawing logic
    // ---------------------------------------------------------------

    private void BeginStroke(Vector3 hitPoint)
    {
        _isDrawing     = true;
        _currentStroke = new DrawingStroke
        {
            colorHex  = ColorUtility.ToHtmlStringRGB(_currentColor),
            lineWidth = lineWidth
        };
        _currentStroke.points.Add(new StrokePoint(hitPoint));
        _lastDrawPoint = hitPoint;

        // Crear LineRenderer para este trazo
        _activeLineRenderer = Instantiate(lineRendererPrefab, transform);
        _activeLineRenderer.startColor = _currentColor;
        _activeLineRenderer.endColor   = _currentColor;
        _activeLineRenderer.startWidth = lineWidth;
        _activeLineRenderer.endWidth   = lineWidth;
        _activeLineRenderer.positionCount = 1;
        _activeLineRenderer.SetPosition(0, hitPoint);

        _renderedLines.Add(_activeLineRenderer);
    }

    private void ContinueStroke(Vector3 hitPoint)
    {

        // Filtrar puntos muy cercanos para no sobrecargar
        if (Vector3.Distance(hitPoint, _lastDrawPoint) < minPointDistance) return;

        _lastDrawPoint = hitPoint;
        _currentStroke.points.Add(new StrokePoint(hitPoint));

        int count = _currentStroke.points.Count;
        _activeLineRenderer.positionCount = count;
        _activeLineRenderer.SetPosition(count - 1, hitPoint);
    }

    private void FinalizeStroke()
    {
        _isDrawing = false;

        if (_currentStroke == null || _currentStroke.points.Count < 2)
        {
            _currentStroke = null;
            return;
        }

        // Guardar localmente
        _allStrokes.strokes.Add(_currentStroke);
        _currentStroke = null;

        // Sincronizar (solo si el modelo está listo y la sala está conectada)
        if (syncOverNetwork && model != null && realtimeView.realtime != null && realtimeView.realtime.connected)
        {
            try {
                if (!IsOwnedLocally())
                {
                    var view = GetComponent<RealtimeView>();
                    view?.RequestOwnership();
                }
                model.strokesJson = JsonUtility.ToJson(_allStrokes);
                model.nonce++;
            } catch (System.Exception e) {
                Debug.LogWarning("[DrawingBoard] Error al sincronizar trazo: " + e.Message);
            }
        }
    }

    // ---------------------------------------------------------------
    // Clear
    // ---------------------------------------------------------------

    /// <summary>
    /// Llamado por BoardClearButton o por botón UI del tablero.
    /// </summary>
    public void ClearBoard()
    {
        if (!IsDoctorLocal())
        {
            Debug.LogWarning("[DrawingBoard] Solo el Doctor puede borrar el tablero.");
            return;
        }

        ClearLocalRenderers();
        _allStrokes = new StrokesData();

        if (syncOverNetwork && model != null)
        {
            if (!IsOwnedLocally())
                GetComponent<RealtimeView>()?.RequestOwnership();

            model.strokesJson = "";
            model.clearNonce++;
        }

        Debug.Log("[DrawingBoard] Tablero borrado.");
    }

    private void ClearLocalRenderers()
    {
        foreach (var lr in _renderedLines)
            if (lr != null) Destroy(lr.gameObject);
        _renderedLines.Clear();
    }

    // ---------------------------------------------------------------
    // Network rebuild
    // ---------------------------------------------------------------

    private void RebuildAllStrokes()
    {
        if (model == null) return;

        string json = model.strokesJson;
        if (string.IsNullOrEmpty(json)) return;

        ClearLocalRenderers();

        StrokesData data = JsonUtility.FromJson<StrokesData>(json);
        if (data?.strokes == null) return;

        _allStrokes = data;

        foreach (var stroke in data.strokes)
            RenderStroke(stroke);
    }

    private void RenderStroke(DrawingStroke stroke)
    {
        if (stroke.points == null || stroke.points.Count < 2) return;

        Color color = Color.black;
        ColorUtility.TryParseHtmlString("#" + stroke.colorHex, out color);

        LineRenderer lr = Instantiate(lineRendererPrefab, transform);
        lr.startColor     = color;
        lr.endColor       = color;
        lr.startWidth     = stroke.lineWidth;
        lr.endWidth       = stroke.lineWidth;
        lr.positionCount  = stroke.points.Count;

        for (int i = 0; i < stroke.points.Count; i++)
            lr.SetPosition(i, stroke.points[i].ToVector3());

        _renderedLines.Add(lr);
    }

    // ---------------------------------------------------------------
    // Raycast
    // ---------------------------------------------------------------

    private bool RaycastBoard(out Vector3 hitPoint)
    {
        hitPoint = Vector3.zero;
        if (boardCollider == null) return false;

        // 1. Prioridad: Ray Interactors de las manos (apuntar con el joystick)
        var rayInteractors = GameObject.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>(FindObjectsSortMode.None);
        foreach (var interactor in rayInteractors)
        {
            if (interactor.TryGetCurrent3DRaycastHit(out RaycastHit rayHit))
            {
                if (rayHit.collider == boardCollider)
                {
                    hitPoint = rayHit.point;
                    return true;
                }
            }
        }

        // 2. Fallback: Ratón (Screen space para PC)
        if (UnityEngine.InputSystem.Mouse.current != null)
        {
            Vector2 mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            Ray ray = drawingCamera.ScreenPointToRay(mousePos);
            if (boardCollider.Raycast(ray, out RaycastHit hit, 10f))
            {
                hitPoint = hit.point;
                return true;
            }
        }

        // 3. Fallback: Centro de la cámara (para VR Headsets o simulador sin mouse libre)
        if (drawingCamera != null)
        {
            Ray ray = new Ray(drawingCamera.transform.position, drawingCamera.transform.forward);
            if (boardCollider.Raycast(ray, out RaycastHit hit, 5f))
            {
                hitPoint = hit.point;
                return true;
            }
        }

        return false;
    }

    // ---------------------------------------------------------------
    // Color selection (público para llamar desde botones UI)
    // ---------------------------------------------------------------

    public void SelectRedPencil()
    {
        _currentColor = redColor;
        Debug.Log("[DrawingBoard] Color seleccionado: Rojo");
    }

    public void SelectBlackPencil()
    {
        _currentColor = blackColor;
        Debug.Log("[DrawingBoard] Color seleccionado: Negro");
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private bool IsDoctorLocal()
    {
        return SessionData.CurrentUser != null &&
               SessionData.CurrentUser.role.Equals("Doctor", System.StringComparison.OrdinalIgnoreCase);
    }

    private bool IsOwnedLocally()
    {
        try
        {
            return realtimeView != null && realtimeView.isOwnedLocallyInHierarchy;
        }
        catch { return false; }
    }
}
