using UnityEngine;
using Normal.Realtime;

public class LocalAvatarAssigner : MonoBehaviour {
    [Header("Avatar Prefabs")]
    public GameObject doctorAvatarPrefab;
    public GameObject patientAvatarPrefab;
    
    private RealtimeAvatarManager avatarManager;

    void Awake() {
        avatarManager = GetComponent<RealtimeAvatarManager>();
        AssignAvatarPrefab();
    }

    void OnEnable() {
        if (avatarManager != null)
            avatarManager.avatarCreated += OnAvatarCreated;
    }

    void OnDisable() {
        if (avatarManager != null)
            avatarManager.avatarCreated -= OnAvatarCreated;
    }

    private void AssignAvatarPrefab() {
        if (avatarManager == null) return;
        
        string role = "Patient";
        if (SessionData.CurrentUser != null && !string.IsNullOrEmpty(SessionData.CurrentUser.role)) {
            role = SessionData.CurrentUser.role;
        }

        GameObject prefabToUse = null;
        if (role == "Doctor" && doctorAvatarPrefab != null) {
            prefabToUse = doctorAvatarPrefab;
        } else if (role == "Patient" && patientAvatarPrefab != null) {
            prefabToUse = patientAvatarPrefab;
        } else {
            Debug.LogWarning($"[LocalAvatarAssigner] No prefab configured for role: {role}. Using default.");
            return;
        }

        avatarManager.localAvatarPrefab = prefabToUse;
        Debug.Log($"[LocalAvatarAssigner] Assigned {(role == "Doctor" ? "Doctor" : "Patient")} avatar prefab");
    }

    private void OnAvatarCreated(RealtimeAvatarManager manager, RealtimeAvatar avatar, bool isLocalAvatar)
    {
        if (isLocalAvatar)
        {
            if (avatar == null || avatar.gameObject == null) return;

            var tag = avatar.gameObject.AddComponent<AvatarUserTag>();

            if (SessionData.CurrentUser != null)
            {
                string role = SessionData.CurrentUser.role;
                string name = SessionData.CurrentUser.username;

                tag.role = role;
                if (role == "Doctor")
                    tag.displayName = "Dr:" + name;
                else
                    tag.displayName = "Patient:" + name;
            }
            else
            {
                tag.displayName = "UnknownUser";
                tag.role = "Patient";
            }
        }
    }
}