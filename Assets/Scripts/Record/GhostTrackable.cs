using UnityEngine;
using Normal.Realtime;

public class GhostTrackable : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("If empty, we'll try to use the GameObject name.")]
    public string displayName = "";

    [Header("Tracked Transforms")]
    public Transform head;
    public Transform leftHand;
    public Transform rightHand;

    private RealtimeView _view;

    void Awake()
    {
        _view = GetComponentInParent<RealtimeView>();
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = gameObject.name;
    }

    // Stable unique ID per avatar instance in the room
    public string GetId()
    {
        // Normcore viewID is stable across clients for this avatar instance
        if (_view != null)
    return $"avatar{_view.GetInstanceID()}";

return $"avatar{GetInstanceID()}";
    }

    public string GetDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(displayName))
            return displayName;
        return gameObject.name;
    }
}
