using UnityEngine;
using Normal.Realtime;
using System.Collections;

public class ApplyLoginRoleToAvatar : MonoBehaviour
{
    private RealtimeView view;

    private void Awake() {
        view = GetComponentInParent<RealtimeView>();
    }

    private void OnEnable() {
        StartCoroutine(WaitThenApply());
    }

    private IEnumerator WaitThenApply()
    {
        // wait until ownership check stops throwing (view ready)
        for (int i = 0; i < 60; i++)
        {
            if (IsOwnedLocallySafe())
                break;

            if (IsRemoteSafe())
                yield break;

            yield return null;
        }

        if (!IsOwnedLocallySafe())
            yield break;

        var avatarRole = GetComponent<AvatarRole>();
        if (avatarRole == null) yield break;

        bool isDoctor = false;
        if (SessionData.CurrentUser != null && !string.IsNullOrEmpty(SessionData.CurrentUser.role))
            isDoctor = SessionData.CurrentUser.role.Trim().Equals("Doctor", System.StringComparison.OrdinalIgnoreCase);

        avatarRole.isDoctor = isDoctor;
        Debug.Log($"[ApplyLoginRoleToAvatar] Role set: {(isDoctor ? "Doctor" : "Patient")}");
    }

    private bool IsOwnedLocallySafe()
    {
        if (view == null) return true;
        try { return view.isOwnedLocallyInHierarchy; }
        catch { return false; }
    }

    private bool IsRemoteSafe()
    {
        if (view == null) return false;
        try { return !view.isOwnedLocallyInHierarchy; }
        catch { return false; }
    }
}
