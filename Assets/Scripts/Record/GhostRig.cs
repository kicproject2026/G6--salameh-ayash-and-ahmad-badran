using UnityEngine;
using TMPro;

public class GhostRig : MonoBehaviour
{
    [Header("Tracked bones")]
    public Transform head;
    public Transform leftHand;
    public Transform rightHand;

    [Header("Optional visuals")]
    public Renderer bodyRenderer;  // the ghost body renderer (Sphere or Body mesh)
    public TMP_Text nameText;      // the ghost NameText (TMP)

    public void ApplyColor(Color c)
    {
        if (bodyRenderer != null)
            bodyRenderer.material.color = c;
    }

    public void ApplyName(string s)
    {
        if (nameText != null)
            nameText.text = string.IsNullOrWhiteSpace(s) ? "" : s;
    }
}