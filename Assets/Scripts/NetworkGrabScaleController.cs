using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using Normal.Realtime;

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
[RequireComponent(typeof(RealtimeTransform))]
public class NetworkGrabScaleWithTriggers : MonoBehaviour
{
    [Header("Scale")]
    [SerializeField, Min(0.01f)] private float minScaleMultiplier = 0.5f;
    [SerializeField, Min(0.01f)] private float maxScaleMultiplier = 2.0f;
    [SerializeField, Min(0.01f)] private float scaleSpeed = 0.8f;

    [Header("Trigger Settings")]
    [SerializeField, Range(0.01f, 1f)] private float triggerThreshold = 0.2f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;
    [SerializeField] private bool debugTriggerValues = false;

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable _grabInteractable;
    private RealtimeTransform _realtimeTransform;

    private InputAction _rightTriggerAction;
    private InputAction _leftTriggerAction;

    private Vector3 _baseLocalScale;
    private float _currentMultiplier = 1f;
    private bool _isLocallyHeld = false;

    private void Awake()
    {
        _grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        _realtimeTransform = GetComponent<RealtimeTransform>();

        _baseLocalScale = transform.localScale;
        if (_baseLocalScale == Vector3.zero)
        {
            _baseLocalScale = Vector3.one;
            transform.localScale = Vector3.one;
        }

        CreateTriggerActions();
        RecalculateCurrentMultiplierFromTransform();
    }

    private void OnEnable()
    {
        _grabInteractable.selectEntered.AddListener(OnSelectEntered);
        _grabInteractable.selectExited.AddListener(OnSelectExited);

        _rightTriggerAction?.Enable();
        _leftTriggerAction?.Enable();

        Log("Enabled.");
    }

    private void OnDisable()
    {
        _grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
        _grabInteractable.selectExited.RemoveListener(OnSelectExited);

        _rightTriggerAction?.Disable();
        _leftTriggerAction?.Disable();

        _isLocallyHeld = false;
    }

    private void OnDestroy()
    {
        _rightTriggerAction?.Dispose();
        _leftTriggerAction?.Dispose();
    }

    private void Update()
    {
        if (!_isLocallyHeld)
            return;

        float rightValue = ReadTrigger(_rightTriggerAction);
        float leftValue = ReadTrigger(_leftTriggerAction);

        bool rightPressed = rightValue > triggerThreshold;
        bool leftPressed = leftValue > triggerThreshold;

        if (debugTriggerValues)
        {
            Debug.Log(
                $"[NetworkGrabScaleWithTriggers] RightTrigger={rightValue:F2} LeftTrigger={leftValue:F2}",
                this
            );
        }

        // Si los dos est�n presionados al mismo tiempo, no pasa nada.
        if (rightPressed && leftPressed)
            return;

        // Si ninguno est� presionado, no pasa nada.
        if (!rightPressed && !leftPressed)
            return;

        _realtimeTransform.RequestOwnership();

        float direction = rightPressed ? 1f : -1f;
        float previousMultiplier = _currentMultiplier;

        _currentMultiplier += direction * scaleSpeed * Time.deltaTime;
        _currentMultiplier = Mathf.Clamp(_currentMultiplier, minScaleMultiplier, maxScaleMultiplier);

        if (!Mathf.Approximately(previousMultiplier, _currentMultiplier))
        {
            transform.localScale = _baseLocalScale * _currentMultiplier;
        }
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        _isLocallyHeld = true;
        _realtimeTransform.RequestOwnership();
        RecalculateCurrentMultiplierFromTransform();

        Log($"Grabbed. Current multiplier = {_currentMultiplier:F2}");
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        _isLocallyHeld = false;
        Log($"Released. Final multiplier = {_currentMultiplier:F2}");
    }

    private void CreateTriggerActions()
    {
        if (_rightTriggerAction == null)
        {
            _rightTriggerAction = new InputAction(
                name: "RightTriggerScaleUp",
                type: InputActionType.Value
            );
            _rightTriggerAction.AddBinding("<XRController>{RightHand}/trigger");
        }

        if (_leftTriggerAction == null)
        {
            _leftTriggerAction = new InputAction(
                name: "LeftTriggerScaleDown",
                type: InputActionType.Value
            );
            _leftTriggerAction.AddBinding("<XRController>{LeftHand}/trigger");
        }
    }

    private float ReadTrigger(InputAction action)
    {
        if (action == null)
            return 0f;

        try
        {
            return action.ReadValue<float>();
        }
        catch
        {
            return 0f;
        }
    }

    private void RecalculateCurrentMultiplierFromTransform()
    {
        float x = SafeDivide(transform.localScale.x, _baseLocalScale.x, 1f);
        float y = SafeDivide(transform.localScale.y, _baseLocalScale.y, 1f);
        float z = SafeDivide(transform.localScale.z, _baseLocalScale.z, 1f);

        _currentMultiplier = (x + y + z) / 3f;
        _currentMultiplier = Mathf.Clamp(_currentMultiplier, minScaleMultiplier, maxScaleMultiplier);
    }

    private float SafeDivide(float value, float divisor, float fallback)
    {
        if (Mathf.Approximately(divisor, 0f))
            return fallback;

        return value / divisor;
    }

    private void Log(string msg)
    {
        if (!debugLogs) return;
        Debug.Log($"[NetworkGrabScaleWithTriggers] {msg}", this);
    }
}