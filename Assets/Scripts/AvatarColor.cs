using UnityEngine;
using Normal.Realtime;

public class AvatarColor : MonoBehaviour
{
    public Renderer bodyRenderer;

    void Start()
    {
        // Get the RealtimeView on this avatar
        var view = GetComponent<RealtimeView>();

        if (view != null && view.isOwnedLocallySelf)
        {
            // This is ME (local player)
            bodyRenderer.material.color = Color.cyan;
        }
        else
        {
            // This is ANOTHER player
            bodyRenderer.material.color = Color.red;
        }
    }
}
