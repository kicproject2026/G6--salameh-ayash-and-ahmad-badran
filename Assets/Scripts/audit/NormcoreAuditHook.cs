using UnityEngine;
using Normal.Realtime;
using System.Collections.Generic;
using TMPro;

public class NormcoreAuditHook : MonoBehaviour
{
    public Realtime realtime;
    public RealtimeAvatarManager avatarManager;

    private void Awake()
    {
        if (realtime == null) realtime = FindObjectOfType<Realtime>();
        if (avatarManager == null) avatarManager = FindObjectOfType<RealtimeAvatarManager>();
    }

    private void OnEnable()
    {
        if (realtime != null)
        {
            realtime.didConnectToRoom += OnConnected;
            realtime.didDisconnectFromRoom += OnDisconnected;
        }

        if (avatarManager != null)
        {
            avatarManager.avatarCreated += OnAvatarCreated;
        }
    }

    private void OnDisable()
    {
        if (realtime != null)
        {
            realtime.didConnectToRoom -= OnConnected;
            realtime.didDisconnectFromRoom -= OnDisconnected;
        }

        if (avatarManager != null)
        {
            avatarManager.avatarCreated -= OnAvatarCreated;
        }
    }

    private void OnConnected(Realtime rt)
    {
        AuditLogger.Instance?.Log("RoomConnected", "system", new Dictionary<string, object> {
            {"room", rt.room != null ? rt.room.name : "unknown"}
        });
    }

    private void OnDisconnected(Realtime rt)
    {
        AuditLogger.Instance?.Log("RoomDisconnected", "system", null);
    }

    private void OnAvatarCreated(RealtimeAvatarManager manager, RealtimeAvatar avatar, bool isLocalAvatar)
{
    string who = "UnknownUser";

    if (isLocalAvatar)
    {
        if (SessionData.CurrentUser != null)
        {
            string role = SessionData.CurrentUser.role;   // "Doctor" or "Patient"
            string name = SessionData.CurrentUser.username;

            who = (role == "Doctor") ? $"Dr:{name}" : $"Patient:{name}";
        }
    }
    else
    {
        // Remote: ONLY accept name labels that start with "patient." or "dr."
        var texts = avatar.GetComponentsInChildren<TMP_Text>(true);

        foreach (var t in texts)
        {
            if (t == null) continue;

            string s = (t.text ?? "").Trim();
            if (string.IsNullOrEmpty(s)) continue;

            string lower = s.ToLowerInvariant();

            // Accept only the label formats you described
            if (lower.StartsWith("patient.") || lower.StartsWith("patient:") ||
                lower.StartsWith("dr.") || lower.StartsWith("dr:"))
            {
                // Normalize format to exactly what you want in the audit file:
                // "Dr:Name" or "Patient:Name"
                if (lower.StartsWith("dr."))
                    who = "Dr:" + s.Substring(3).Trim();
                else if (lower.StartsWith("dr:"))
                    who = "Dr:" + s.Substring(3).Trim();
                else if (lower.StartsWith("patient."))
                    who = "Patient:" + s.Substring(8).Trim();
                else if (lower.StartsWith("patient:"))
                    who = "Patient:" + s.Substring(8).Trim();

                break;
            }
        }
    }

    AuditLogger.Instance?.Log(
        isLocalAvatar ? "AvatarCreatedLocal" : "AvatarCreatedRemote",
        who,
        null
    );
}





}
