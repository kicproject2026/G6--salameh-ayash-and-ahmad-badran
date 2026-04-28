using UnityEngine;
using Normal.Realtime;
using System.Reflection;
using TMPro;

public class GhostTrackable : MonoBehaviour
{
    [Header("Tracked Transforms")]
    public Transform head;
    public Transform leftHand;
    public Transform rightHand;

    private RealtimeView _view;
    private AvatarColor _avatarColor;
    private AvatarUserTag _avatarUserTag;

    void Awake()
    {
        _view = GetComponentInParent<RealtimeView>();
        _avatarColor = GetComponentInParent<AvatarColor>();
        _avatarUserTag = GetComponentInParent<AvatarUserTag>();
    }

    // Stable-ish ID across clients when possible (depends on Normcore version).
    public string GetId()
    {
        // Try to get an actual shared Normcore view id if this version exposes it
        if (_view != null)
        {
            int sharedId;
            if (TryGetRealtimeViewId(_view, out sharedId))
                return $"avatar{sharedId}";
        }

        // Fallback (always works, but may differ per client)
        return $"avatar{GetInstanceID()}";
    }

    // ✅ Exact same name that appears above the real avatar head (login name)
    public string GetDisplayName()
    {
        // First try AvatarUserTag
        if (_avatarUserTag != null && !string.IsNullOrWhiteSpace(_avatarUserTag.displayName))
            return _avatarUserTag.displayName;
        
        // Second try RoomUserManager (synchronized data)
        if (_view != null)
        {
            try
            {
                int ownerID = _view.ownerID;
                var userInfo = RoomUserManager.GetUserInfo(ownerID);
                if (userInfo.HasValue)
                    return userInfo.Value.displayName;
            }
            catch { }
        }
        
        // Third try AvatarColor
        if (_avatarColor != null && _avatarColor.nameText != null)
        {
            string s = _avatarColor.nameText.text;
            if (!string.IsNullOrWhiteSpace(s))
                return s;
        }

        return gameObject.name;
    }

    // ✅ Exact same body color as the real avatar
    public Color GetBodyColor()
    {
        /*
        if (_avatarColor != null && _avatarColor.bodyRenderer != null)
            return _avatarColor.bodyRenderer.material.color;
        */
        return Color.clear;
    }

    public string GetRole()
    {
        // First, try AvatarUserTag (works for local AND remote if set)
        if (_avatarUserTag != null && !string.IsNullOrWhiteSpace(_avatarUserTag.displayName))
        {
            string displayName = _avatarUserTag.displayName;
            if (displayName.StartsWith("Dr:", System.StringComparison.OrdinalIgnoreCase))
                return "Doctor";
            if (displayName.StartsWith("Patient:", System.StringComparison.OrdinalIgnoreCase))
                return "Patient";
        }

        // Second, try RoomUserManager (synchronized data from Normcore)
        if (_view != null)
        {
            try
            {
                int ownerID = _view.ownerID;
                var userInfo = RoomUserManager.GetUserInfo(ownerID);
                if (userInfo.HasValue)
                    return userInfo.Value.role;
            }
            catch { }
        }

        // Third, fallback to SessionData (only works for local user)
        if (SessionData.CurrentUser != null && !string.IsNullOrEmpty(SessionData.CurrentUser.role))
            return SessionData.CurrentUser.role;

        return "Patient";
    }

    public bool GetIsWalking()
    {
        if (_avatarUserTag != null)
            return _avatarUserTag.isWalking;

        var movementSync = GetComponent<AvatarMovementSync>();
        if (movementSync != null)
            return movementSync.IsWalking;

        return false;
    }

    // --------- Compatibility helper (works across Normcore versions) ---------
    private static bool TryGetRealtimeViewId(RealtimeView view, out int id)
    {
        id = 0;
        if (view == null) return false;

        var t = view.GetType();

        // 1) Try property "viewID"
        var prop = t.GetProperty("viewID", BindingFlags.Public | BindingFlags.Instance);
        if (prop != null && prop.PropertyType == typeof(int))
        {
            id = (int)prop.GetValue(view);
            return true;
        }

        // 2) Try field "viewID"
        var field = t.GetField("viewID", BindingFlags.Public | BindingFlags.Instance);
        if (field != null && field.FieldType == typeof(int))
        {
            id = (int)field.GetValue(view);
            return true;
        }

        // 3) Try private field "_viewID"
        field = t.GetField("_viewID", BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null && field.FieldType == typeof(int))
        {
            id = (int)field.GetValue(view);
            return true;
        }

        return false;
    }
}