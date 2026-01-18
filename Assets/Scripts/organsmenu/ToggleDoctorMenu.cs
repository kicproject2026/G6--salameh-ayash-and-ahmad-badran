using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class ToggleDoctorMenu : MonoBehaviour
{
    [Header("Input")]
    public InputActionReference toggleAction;
    public bool enableKeyboardFallback = true;

    [Header("Menu")]
    public GameObject menuRoot; // Assign the Canvas or Panel here

    [Header("Audit")]
    public AuditMenuToggle auditMenuToggle;

    private InputAction _runtimeAction;
    private InputAction ActionToUse => (toggleAction != null && toggleAction.action != null)
        ? toggleAction.action
        : _runtimeAction;

    private void Awake()
    {
        // Auto-find audit if missing
        if (auditMenuToggle == null)
            auditMenuToggle = GetComponent<AuditMenuToggle>();

        // Create the X button binding if no reference is provided
        if (toggleAction == null)
        {
            _runtimeAction = new InputAction("ToggleMenu", InputActionType.Button);
            _runtimeAction.AddBinding("<XRController>{LeftHand}/primaryButton"); // X Button
            if (enableKeyboardFallback)
                _runtimeAction.AddBinding("<Keyboard>/m");
        }
    }

    private void OnEnable()
    {
        var action = ActionToUse;
        if (action != null)
        {
            action.Enable();
            action.performed += Toggle;
        }
    }

    private void OnDisable()
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
        // 1. Role Check: Only the local user with the "Doctor" role can proceed
        if (SessionData.CurrentUser == null || 
            !SessionData.CurrentUser.role.Trim().Equals("Doctor", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (menuRoot == null) return;

        // 2. Local Activation: This only happens on the Doctor's screen
        menuRoot.SetActive(!menuRoot.activeSelf);
        Debug.Log("[ToggleDoctorMenu] Menu toggled. Now active = " + menuRoot.activeSelf);

        // 3. Audit Logging
        if (auditMenuToggle != null)
        {
            auditMenuToggle.LogMenuState();
        }
    }
}

