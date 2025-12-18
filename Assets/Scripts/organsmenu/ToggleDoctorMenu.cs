using UnityEngine;
using UnityEngine.InputSystem;
using Normal.Realtime;
using System.Collections;

public class ToggleDoctorMenu : MonoBehaviour
{
    [Header("Input (optional)")]
    [Tooltip("Optional: assign an InputActionReference. If left empty, this script will create one bound to LeftHand primaryButton (X).")]
    public InputActionReference toggleAction;

    [Tooltip("Editor/testing fallback.")]
    public bool enableKeyboardFallback = true;

    [Header("Menu")]
    public GameObject menuRoot;

    private RealtimeView view;
    private AvatarRole role;

    private InputAction _runtimeAction; // used if toggleAction is not assigned

    private InputAction ActionToUse => (toggleAction != null && toggleAction.action != null)
        ? toggleAction.action
        : _runtimeAction;

    private void Awake()
    {
        view = GetComponentInParent<RealtimeView>();
        role = GetComponent<AvatarRole>();

        // If no action reference provided, create a runtime action bound to Left X (primaryButton).
        if (toggleAction == null)
        {
            _runtimeAction = new InputAction("ToggleMenu", InputActionType.Button);

            // Works for OpenXR / XR hands generally (Left primary = X on Oculus).
            _runtimeAction.AddBinding("<XRController>{LeftHand}/primaryButton");

            // Extra explicit Oculus binding (harmless if layout isn't present).
            _runtimeAction.AddBinding("<OculusTouchController>{LeftHand}/primaryButton");

            if (enableKeyboardFallback)
                _runtimeAction.AddBinding("<Keyboard>/m");
        }
    }

    private void OnEnable()
    {
        StartCoroutine(WaitThenSubscribe());
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(0.5f);
        menuRoot.transform.SetParent(transform);
    }

    private IEnumerator WaitThenSubscribe()
    {
        // Wait a few frames until Normcore finishes setting up the RealtimeView internally
        for (int i = 0; i < 60; i++)
        {
            if (IsOwnedLocallySafe())
                break;

            // If not owned locally, stop (this is a remote avatar)
            if (IsRemoteSafe())
                yield break;

            yield return null;
        }

        // If after waiting it's still not safe/owned, just stop.
        if (!IsOwnedLocallySafe())
            yield break;

        var action = ActionToUse;
        if (action == null)
        {
            Debug.LogError("[ToggleDoctorMenu] No InputAction available. Assign toggleAction or allow runtime action creation.");
            yield break;
        }

        action.Enable();
        action.performed += Toggle;

        Debug.Log("[ToggleDoctorMenu] Subscribed: LeftHand primaryButton (X) toggles menuRoot");
    }

    private bool IsOwnedLocallySafe()
    {
        if (view == null) return true; // if no view, assume local (single player)
        try { return view.isOwnedLocallyInHierarchy; }
        catch { return false; } // model not ready yet
    }

    private bool IsRemoteSafe()
    {
        if (view == null) return false;
        try { return !view.isOwnedLocallyInHierarchy; }
        catch { return false; }
    }

    private void Unsubscribe()
    {
        var action = ActionToUse;
        if (action != null)
        {
            action.performed -= Toggle;
            action.Disable();
        }
    }

    private void Toggle(InputAction.CallbackContext ctx)
    {
        if (role == null || !role.isDoctor)
        {
            Debug.Log("[ToggleDoctorMenu] Blocked: not a doctor");
            return;
        }

        if (menuRoot == null)
        {
            Debug.LogError("[ToggleDoctorMenu] menuRoot is NULL");
            return;
        }

        menuRoot.SetActive(!menuRoot.activeSelf);
        Debug.Log("[ToggleDoctorMenu] Menu toggled");
    }
}
