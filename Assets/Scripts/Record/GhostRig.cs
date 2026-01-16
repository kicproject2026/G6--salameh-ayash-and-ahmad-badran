using UnityEngine;

public class GhostRig : MonoBehaviour
{
    [Header("Tracked transforms")]
    public Transform head;
    public Transform leftHand;
    public Transform rightHand;

    [Header("Visuals (for color replay)")]
    public Renderer bodyRenderer;   // assign to ghost body mesh/renderer (Sphere/Body)
}
