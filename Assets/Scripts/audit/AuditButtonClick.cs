using System.Collections.Generic;
using UnityEngine;

public class AuditButtonClick : MonoBehaviour
{
    [Tooltip("What to write in the audit log, e.g. SpawnSkeleton / SpawnHeart / OpenRecordMenu")]
    public string eventName = "ButtonClick";

    public void LogClick()
    {
        string who = SessionData.CurrentUser != null
            ? (SessionData.CurrentUser.role == "Doctor"
                ? $"Dr:{SessionData.CurrentUser.username}"
                : $"Patient:{SessionData.CurrentUser.username}")
            : "UnknownUser";

        AuditLogger.Instance?.Log("ButtonClick", who, new Dictionary<string, object> {
            {"button", eventName}
        });
    }
}
