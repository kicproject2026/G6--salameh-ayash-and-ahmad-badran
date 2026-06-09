using Unity.XR.CoreUtils;
using UnityEngine;

public class XROriginTeleportButton : MonoBehaviour
{
    public XROrigin xrOrigin;
    public CharacterController characterController;

    public void TeleportToTarget(Transform target)
    {
        if (xrOrigin == null || target == null)
            return;

        bool hadCharacterController = characterController != null;
        if (hadCharacterController)
            characterController.enabled = false;

        xrOrigin.transform.SetPositionAndRotation(target.position, target.rotation);

        if (hadCharacterController)
            characterController.enabled = true;
    }
}
