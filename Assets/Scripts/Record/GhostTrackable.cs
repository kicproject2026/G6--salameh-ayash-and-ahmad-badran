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

    [Header("Color Source (optional)")]
    [Tooltip("If empty, we'll auto-find AvatarColor, then a Renderer.")]
    public AvatarColor avatarColor;      // your existing script
    public Renderer bodyRenderer;        // fallback if avatarColor is not found

    private RealtimeView _view;

    void Awake()
    {
        _view = GetComponentInParent<RealtimeView>();

        if (string.IsNullOrWhiteSpace(displayName))
            displayName = gameObject.name;

        if (avatarColor == null)
            avatarColor = GetComponentInChildren<AvatarColor>(true);

        if (bodyRenderer == null)
        {
            // Try to use AvatarColor.bodyRenderer first (if available)
            if (avatarColor != null && avatarColor.bodyRenderer != null)
                bodyRenderer = avatarColor.bodyRenderer;
            else
                bodyRenderer = GetComponentInChildren<Renderer>(true);
        }
    }

    // Stable enough for recording within a session
    public string GetId()
    {
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

    public Color GetBodyColor()
    {
        // If AvatarColor is present and has a renderer, use it
        if (avatarColor != null && avatarColor.bodyRenderer != null)
            return avatarColor.bodyRenderer.material.color;

        // Fallback
        if (bodyRenderer != null)
            return bodyRenderer.material.color;

        return Color.white;
    }
}
