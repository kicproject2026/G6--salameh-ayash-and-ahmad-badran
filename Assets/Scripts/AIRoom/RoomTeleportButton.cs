using UnityEngine;
using UnityEngine.UI;

public class RoomTeleportButton : MonoBehaviour
{
    [Header("References")]
    public Transform rigRoot;
    public Transform headCamera;
    public Transform destination;
    public Button triggerButton;
    public CharacterController characterController;

    [Header("Behavior")]
    public bool matchDestinationYaw = true;
    public Vector3 positionOffset = Vector3.zero;

    private void Awake()
    {
        if (triggerButton != null)
            triggerButton.onClick.AddListener(TeleportNow);
    }

    private void OnDestroy()
    {
        if (triggerButton != null)
            triggerButton.onClick.RemoveListener(TeleportNow);
    }

    public void TeleportNow()
    {
        if (rigRoot == null || destination == null)
            return;

        bool hadCharacterController = characterController != null;
        if (hadCharacterController)
            characterController.enabled = false;

        if (matchDestinationYaw)
            ApplyYawAlignment();

        Vector3 flatHeadOffset = Vector3.zero;
        if (headCamera != null)
        {
            flatHeadOffset = headCamera.position - rigRoot.position;
            flatHeadOffset.y = 0f;
        }

        rigRoot.position = destination.position - flatHeadOffset + positionOffset;

        if (hadCharacterController)
            characterController.enabled = true;
    }

    private void ApplyYawAlignment()
    {
        if (headCamera == null)
        {
            rigRoot.rotation = Quaternion.Euler(0f, destination.eulerAngles.y, 0f);
            return;
        }

        float deltaYaw = destination.eulerAngles.y - headCamera.eulerAngles.y;
        rigRoot.Rotate(Vector3.up, deltaYaw, Space.World);
    }
}
