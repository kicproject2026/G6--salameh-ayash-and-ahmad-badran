using System.Collections.Generic;
using UnityEngine;

public class AuditMenuToggle : MonoBehaviour
{
    [Header("Menu Root (Canvas or Panel that is shown/hidden)")]
    public GameObject menuRoot;

    public void LogMenuState()
    {
        if (menuRoot == null) return;

        bool isOpen = menuRoot.activeSelf;

        string who = SessionData.CurrentUser != null
            ? (SessionData.CurrentUser.role == "Doctor"
                ? $"Dr:{SessionData.CurrentUser.username}"
                : $"Patient:{SessionData.CurrentUser.username}")
            : "UnknownUser";

        AuditLogger.Instance?.Log(
            "MenuToggle",
            who,
            new Dictionary<string, object> {
                { "state", isOpen ? "Open" : "Closed" },
                { "menu", menuRoot.name }
            }
        );
    }
}
