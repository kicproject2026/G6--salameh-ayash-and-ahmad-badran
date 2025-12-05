using UnityEngine;
using Normal.Realtime;

public class LocalAvatarAssigner : MonoBehaviour {
    public XRAvatarMapper avatarMapper;
    public RealtimeAvatarManager avatarManager;

    void OnEnable() {
        if (avatarManager != null)
            avatarManager.avatarCreated += OnAvatarCreated;
    }

    void OnDisable() {
        if (avatarManager != null)
            avatarManager.avatarCreated -= OnAvatarCreated;
    }

    private void OnAvatarCreated(RealtimeAvatarManager manager, RealtimeAvatar avatar, bool isLocalAvatar) {
        if (isLocalAvatar)
            avatarMapper.realtimeAvatar = avatar;
    }
}
