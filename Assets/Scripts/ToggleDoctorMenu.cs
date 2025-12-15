using UnityEngine;
using UnityEngine.InputSystem;
using Normal.Realtime;
using System.Collections;

public class ToggleDoctorMenu : MonoBehaviour
{
    public InputActionReference toggleAction;
    public GameObject menuRoot;

    private RealtimeView view;
    private AvatarRole role;

    private void Awake() {
        view = GetComponentInParent<RealtimeView>();
        role = GetComponent<AvatarRole>();
    }

    private void OnEnable() {
        StartCoroutine(WaitThenSubscribe());
    }

    private void OnDisable() {
        Unsubscribe();
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

        if (toggleAction == null) {
            Debug.LogError("[ToggleDoctorMenu] toggleAction is NULL");
            yield break;
        }

        toggleAction.action.Enable();
        toggleAction.action.performed += Toggle;

        Debug.Log("[ToggleDoctorMenu] Subscribed to X (safe)");
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
        if (toggleAction != null)
        {
            toggleAction.action.performed -= Toggle;
            toggleAction.action.Disable();
        }
    }

    private void Toggle(InputAction.CallbackContext ctx)
    {
        if (role == null || !role.isDoctor) {
            Debug.Log("[ToggleDoctorMenu] Blocked: not a doctor");
            return;
        }

        if (menuRoot == null) {
            Debug.LogError("[ToggleDoctorMenu] menuRoot is NULL");
            return;
        }

        menuRoot.SetActive(!menuRoot.activeSelf);
        Debug.Log("[ToggleDoctorMenu] Menu toggled");
    }
}
