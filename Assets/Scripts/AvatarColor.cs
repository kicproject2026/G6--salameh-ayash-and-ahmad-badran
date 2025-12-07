using UnityEngine;
using TMPro;
using Normal.Realtime;

public class AvatarColor : RealtimeComponent<AvatarColorModel> {
    [Header("Visual References")]
    public Renderer bodyRenderer;   // body mesh
    public TMP_Text nameText;       // text above head

    protected override void OnRealtimeModelReplaced(AvatarColorModel previousModel, AvatarColorModel currentModel) {
        if (previousModel != null) {
            previousModel.bodyColorDidChange   -= OnBodyColorDidChange;
            previousModel.displayNameDidChange -= OnDisplayNameDidChange;
        }

        if (currentModel != null) {
            // First time this avatar is created and we own it
            if (currentModel.isFreshModel && realtimeView.isOwnedLocallySelf) {
                bool isDoctor = SessionData.CurrentUser != null &&
                                SessionData.CurrentUser.role == "Doctor";

                // 1) Body color
                if (isDoctor) {
                    currentModel.bodyColor = Color.blue;
                } else {
                    currentModel.bodyColor = Color.cyan;
                }

                // 2) Display name
                string username = SessionData.CurrentUser != null
                    ? SessionData.CurrentUser.username
                    : "User";

                currentModel.displayName = isDoctor ? $"Dr. {username}" : $"Patient. {username}";
            }

            // Apply current values now
            ApplyBodyColor(currentModel.bodyColor);
            ApplyDisplayName(currentModel.displayName);

            // Listen for changes from network
            currentModel.bodyColorDidChange   += OnBodyColorDidChange;
            currentModel.displayNameDidChange += OnDisplayNameDidChange;
        }
    }

    private void OnBodyColorDidChange(AvatarColorModel model, Color value) {
        ApplyBodyColor(value);
    }

    private void OnDisplayNameDidChange(AvatarColorModel model, string value) {
        ApplyDisplayName(value);
    }

    private void ApplyBodyColor(Color color) {
        if (bodyRenderer != null)
            bodyRenderer.material.color = color;
    }

    private void ApplyDisplayName(string displayName) {
        if (nameText != null && !string.IsNullOrEmpty(displayName))
            nameText.text = displayName;
    }
}
