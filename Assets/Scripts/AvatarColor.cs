using UnityEngine;
using Normal.Realtime;

public class AvatarColor : RealtimeComponent<AvatarColorModel>
{
    public Renderer bodyRenderer;

    protected override void OnRealtimeModelReplaced(AvatarColorModel previousModel, AvatarColorModel currentModel)
    {
        if (previousModel != null) {
            previousModel.bodyColorDidChange -= ColorDidChange;
        }

        if (currentModel != null) {
            
            if (currentModel.isFreshModel && realtimeView.isOwnedLocallySelf) {
                // Determine color based on role
                if (SessionData.CurrentUser != null && SessionData.CurrentUser.role == "Doctor") {
                    currentModel.bodyColor = Color.cyan;
                } else {
                    // random color for patients
                    currentModel.bodyColor = Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.7f, 1f);
                }
            }

            // Apply color now
            ApplyColor(currentModel.bodyColor);

            // Listen for changes
            currentModel.bodyColorDidChange += ColorDidChange;
        }
    }

    private void ColorDidChange(AvatarColorModel model, Color color) {
        ApplyColor(color);
    }

    private void ApplyColor(Color color) {
        if (bodyRenderer != null)
            bodyRenderer.material.color = color;
    }
}
