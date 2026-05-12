using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

/// <summary>
/// Grab transformer que permite rotación COMPLETA (horizontal + vertical + roll)
/// en todos los ejes. Reemplaza al RotationAxisLockGrabTransformer cuando se
/// requiere rotación libre en todos los ejes.
///
/// También incluye suavizado de inicio para evitar saltos bruscos al agarrar.
///
/// Setup por objeto interactuable:
///  1. Seleccionar el objeto que tiene XRGrabInteractable.
///  2. Remover cualquier RotationAxisLockGrabTransformer que restrinja ejes.
///  3. Agregar este componente al mismo GameObject.
///  4. En XRGrabInteractable > Add Single Grab Transformer: asignar este componente.
///     (O dejarlo en "Auto" y este se registrará automáticamente.)
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class FullRotationGrabTransformer : XRGeneralGrabTransformer
{
    [Header("Rotation Settings")]
    [Tooltip("Velocidad de suavizado al iniciar el grab (para evitar saltos). 0 = sin suavizado.")]
    [SerializeField, Range(0f, 1f)] private float snapSmoothFactor = 0.15f;

    [Tooltip("Si está activo, aplica un damping suave a la rotación durante el grab.")]
    [SerializeField] private bool smoothRotation = false;

    [Tooltip("Velocidad de interpolación de rotación suave (solo si smoothRotation = true).")]
    [SerializeField, Range(1f, 30f)] private float rotationSmoothSpeed = 15f;

    [Header("Inspection Rotation (Simulator & VR)")]
    [Tooltip("Permite rotar el objeto sobre su eje usando las flechas del teclado o el Joystick.")]
    [SerializeField] private bool enableInspectionRotation = true;
    [SerializeField] private float inspectionSpeed = 120f;
    [Tooltip("Invierte la rotación horizontal (Izquierda/Derecha).")]
    [SerializeField] private bool invertHorizontal = false;
    [Tooltip("Invierte la rotación vertical (Arriba/Abajo).")]
    [SerializeField] private bool invertVertical = false;

    [Header("Input Bindings")]
    [Tooltip("Acción para el joystick del control derecho (usado para rotación manual).")]
    [SerializeField] private InputActionProperty rightJoystickAction;

    // Permite registro tanto para grab de 1 como de 2 manos
    protected override RegistrationMode registrationMode => RegistrationMode.SingleAndMultiple;

    private Quaternion _currentSmoothedRotation;
    private Quaternion _additionalRotation = Quaternion.identity;
    private bool _initialized;
    private UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionProvider[] _locomotionProviders;

    // ---------------------------------------------------------------
    // Overrides
    // ---------------------------------------------------------------

    public override void OnLink(XRGrabInteractable grabInteractable)
    {
        base.OnLink(grabInteractable);
        _initialized = false;
        _additionalRotation = Quaternion.identity;

        // Encontrar todos los proveedores de locomoción (Movimiento y Giro) en la escena
        if (_locomotionProviders == null || _locomotionProviders.Length == 0)
        {
            _locomotionProviders = GameObject.FindObjectsOfType<UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionProvider>();
        }
    }

    public override void OnGrab(XRGrabInteractable grabInteractable)
    {
        base.OnGrab(grabInteractable);
        // Capturar la rotación actual del objeto al momento de agarrarlo
        _currentSmoothedRotation = grabInteractable.transform.rotation;
        _initialized = true;

        // BLOQUEAR LOCOMOCIÓN: Desactivamos todos los LocomotionProviders
        // para que el joystick no mueva ni rote al jugador mientras inspecciona.
        if (_locomotionProviders != null)
        {
            foreach (var provider in _locomotionProviders)
            {
                if (provider != null) provider.enabled = false;
            }
        }

        // Suscribirse al evento de soltar para rehabilitar la locomoción
        grabInteractable.selectExited.RemoveListener(OnRelease);
        grabInteractable.selectExited.AddListener(OnRelease);
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        // RE-HABILITAR LOCOMOCIÓN al soltar el objeto
        if (_locomotionProviders != null)
        {
            foreach (var provider in _locomotionProviders)
            {
                if (provider != null) provider.enabled = true;
            }
        }
    }

    public override void OnUnlink(XRGrabInteractable grabInteractable)
    {
        base.OnUnlink(grabInteractable);
        grabInteractable.selectExited.RemoveListener(OnRelease);
        // Asegurar que si se destruye el objeto, la locomoción vuelva
        if (_locomotionProviders != null)
        {
            foreach (var provider in _locomotionProviders)
            {
                if (provider != null) provider.enabled = true;
            }
        }
    }

    public override void Process(
        XRGrabInteractable grabInteractable,
        XRInteractionUpdateOrder.UpdatePhase updatePhase,
        ref Pose targetPose,
        ref Vector3 localScale)
    {
        // Llamar a la base primero para que calcule la posición y rotación base (crucial para mover el objeto)
        base.Process(grabInteractable, updatePhase, ref targetPose, ref localScale);

        // Solo procesar en la fase dinámica
        if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic) return;

        if (!_initialized)
        {
            _currentSmoothedRotation = grabInteractable.transform.rotation;
            _initialized = true;
        }

        // Lógica de rotación de inspección (Teclado/Joystick)
        if (enableInspectionRotation)
        {
            float horizontal = 0;
            float vertical = 0;

            // 1. Input de Joystick (VR)
            Vector2 joystickInput = rightJoystickAction.action != null 
                ? rightJoystickAction.action.ReadValue<Vector2>() 
                : Vector2.zero;

            if (joystickInput.sqrMagnitude > 0.01f)
            {
                // Mapeo directo del joystick. Si el usuario prefiere invertido, se aplica.
                horizontal = invertHorizontal ? -joystickInput.x : joystickInput.x;
                vertical = invertVertical ? -joystickInput.y : joystickInput.y;
            }
            // 2. Fallback a Teclado (Editor/Simulator)
            else if (Application.isEditor)
            {
                if (Input.GetKey(KeyCode.LeftArrow)) horizontal = invertHorizontal ? 1 : -1;
                else if (Input.GetKey(KeyCode.RightArrow)) horizontal = invertHorizontal ? -1 : 1;
                
                if (Input.GetKey(KeyCode.UpArrow)) vertical = invertVertical ? -1 : 1;
                else if (Input.GetKey(KeyCode.DownArrow)) vertical = invertVertical ? 1 : -1;
            }

            if (horizontal != 0 || vertical != 0)
            {
                float deltaSpeed = inspectionSpeed * Time.deltaTime;
                
                // Rotar en base a los ejes de la cámara para que sea 100% intuitivo (Espacio Mundial)
                Transform cam = Camera.main != null ? Camera.main.transform : null;
                Vector3 upAxis = cam != null ? cam.up : Vector3.up;
                Vector3 rightAxis = cam != null ? cam.right : Vector3.right;

                // Calculamos el delta de rotación en el mundo. 
                // Horizontal = girar sobre el eje Y de la cámara (Up)
                // Vertical = girar sobre el eje X de la cámara (Right)
                Quaternion deltaWorld = Quaternion.AngleAxis(-horizontal * deltaSpeed, upAxis) * 
                                        Quaternion.AngleAxis(vertical * deltaSpeed, rightAxis);

                // Aplicamos el delta global sobre la rotación total actual del objeto
                Quaternion currentFullRot = targetPose.rotation * _additionalRotation;
                Quaternion newFullRot = deltaWorld * currentFullRot;

                // Actualizamos el acumulador local "restando" la rotación base de la mano
                _additionalRotation = Quaternion.Inverse(targetPose.rotation) * newFullRot;
            }
        }

        // Aplicamos la rotación manual acumulada a la rotación que viene del grab
        targetPose.rotation *= _additionalRotation;

        if (smoothRotation)
        {
            // Interpolar suavemente hacia la rotación objetivo (que ya incluye la manual)
            _currentSmoothedRotation = Quaternion.Slerp(
                _currentSmoothedRotation,
                targetPose.rotation,
                rotationSmoothSpeed * Time.deltaTime);

            targetPose.rotation = _currentSmoothedRotation;
        }
    }
}
