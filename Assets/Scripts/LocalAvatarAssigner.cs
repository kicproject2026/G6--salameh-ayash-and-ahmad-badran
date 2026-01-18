using UnityEngine;
using Normal.Realtime;

public class LocalAvatarAssigner : MonoBehaviour {
    public RealtimeAvatarManager avatarManager;

    void OnEnable() {
        if (avatarManager != null)
            avatarManager.avatarCreated += OnAvatarCreated;
    }

    void OnDisable() {
        if (avatarManager != null)
            avatarManager.avatarCreated -= OnAvatarCreated;
    }

    private void OnAvatarCreated(RealtimeAvatarManager manager, RealtimeAvatar avatar, bool isLocalAvatar)
{
    if (isLocalAvatar)
    {
        if (avatar == null || avatar.gameObject == null) return;

        // Attach display name with role
        var tag = avatar.gameObject.AddComponent<AvatarUserTag>();

        if (SessionData.CurrentUser != null)
        {
            string role = SessionData.CurrentUser.role;   // "Doctor" or "Patient"
            string name = SessionData.CurrentUser.username;

            if (role == "Doctor")
                tag.displayName = "Dr:" + name;
            else
                tag.displayName = "Patient:" + name;
        }
        else
        {
            tag.displayName = "UnknownUser";
        }
    }
}

}
