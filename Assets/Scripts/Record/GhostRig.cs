using UnityEngine;
using TMPro;

public class GhostRig : MonoBehaviour
{
    [Header("Tracked bones")]
    public Transform head;
    public Transform leftHand;
    public Transform rightHand;

    [Header("Visual Rig")]
    public Transform body;
    public Transform leftLeg;
    public Transform rightLeg;


    [Header("Optional visuals")]
    public Renderer bodyRenderer;
    public TMP_Text nameText;
    public Animator animator;


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