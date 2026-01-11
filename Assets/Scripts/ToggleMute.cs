using UnityEngine;
using UnityEngine.InputSystem;
using Normal.Realtime;

public class ToggleMute : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Optional InputAction. If empty, A button will be bound at runtime.")]
    public InputActionReference muteAction;

    [Tooltip("Keyboard fallback for testing")]
    public bool enableKeyboardFallback = true;

    [Header("Voice")]
    public RealtimeAvatarVoice avatarVoice;   // assign if needed

    private InputAction _runtimeAction;
    private bool isMuted = false;
    private RealtimeView view;

    private InputAction ActionToUse =>
        (muteAction != null && muteAction.action != null) ? muteAction.action : _runtimeAction;

    private void Awake()
    {
        view = GetComponentInParent<RealtimeView>();

        // If no InputAction provided, create one bound to A button
        if (muteAction == null)
        {
            _runtimeAction = new InputAction("ToggleMute", InputActionType.Button);

            // B button (right hand secondary button)
_runtimeAction.AddBinding("<XRController>{RightHand}/secondaryButton");

// Oculus explicit binding
_runtimeAction.AddBinding("<OculusTouchController>{RightHand}/secondaryButton");

            if (enableKeyboardFallback)
                _runtimeAction.AddBinding("<Keyboard>/m");
        }
    }

    private void OnEnable()
    {
        if (ActionToUse != null)
        {
            ActionToUse.Enable();
            ActionToUse.performed += Toggle;
        }
    }

    private void OnDisable()
    {
        if (ActionToUse != null)
        {
            ActionToUse.performed -= Toggle;
            ActionToUse.Disable();
        }
    }

    private void Start()
    {
        // Auto-find voice component if not assigned
        if (avatarVoice == null)
            avatarVoice = GetComponentInChildren<RealtimeAvatarVoice>();

        if (avatarVoice == null)
            Debug.LogError("[ToggleMute] RealtimeAvatarVoice not found.");
    }

    private void Toggle(InputAction.CallbackContext ctx)
    {
        // Only allow local player to mute themselves
        if (view != null && !view.isOwnedLocallyInHierarchy)
            return;

        if (avatarVoice == null)
            return;

        isMuted = !isMuted;

        // Disable or enable microphone
        avatarVoice.mute = isMuted;

        Debug.Log(isMuted
            ? "[ToggleMute] Microphone muted"
            : "[ToggleMute] Microphone unmuted");
    }
}
