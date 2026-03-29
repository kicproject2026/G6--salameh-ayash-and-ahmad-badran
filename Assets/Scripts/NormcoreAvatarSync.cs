using System;
using UnityEngine;
using Normal.Realtime;

public class NormcoreAvatarSync : RealtimeComponent<RealtimeAvatarModel> {
    [Header("Avatar Parts")]
    public Transform head;
    public Transform leftHand;
    public Transform rightHand;

    [Header("Visual Rig (Auto-Mapped by Mesh Bounds)")]
    [SerializeField] private Transform body;
    [SerializeField] private Transform leftLeg;
    [SerializeField] private Transform rightLeg;

    [Header("Rig Settings")]
    [Tooltip("Target fixed physical height for the legs (e.g. 0.7m)")]
    [SerializeField] private float targetLegHeight = 0.7f;
    [Tooltip("Target spacing from the head center down to the top of the body")]
    [SerializeField] private float headToBodyOffset = 0.15f;

    [Header("XR Sources")]
    [SerializeField] private Transform xrCamera;
    [SerializeField] private Transform xrLeftHand;
    [SerializeField] private Transform xrRightHand;

    private Transform _xrCamera;
    private Transform _xrLeftHand;
    private Transform _xrRightHand;

    private bool _localVisualsHidden;
    private float _nextRebindTime;

    private Bounds _bodyBounds;
    private Bounds _legBounds;
    private Vector3 _baseBodyScale;

    private void Start() {
        FindXRNodes();
        if (body) {
            _bodyBounds = GetLocalBounds(body);
            _baseBodyScale = body.localScale;
        } else {
            _bodyBounds = new Bounds(Vector3.zero, Vector3.one);
            _baseBodyScale = Vector3.one;
        }

        if (leftLeg) {
            _legBounds = GetLocalBounds(leftLeg);
        } else if (rightLeg) {
            _legBounds = GetLocalBounds(rightLeg);
        } else {
            _legBounds = new Bounds(Vector3.zero, Vector3.one);
        }
    }

    private Bounds GetLocalBounds(Transform t) {
        var filter = t.GetComponentInChildren<MeshFilter>(true);
        if (filter != null && filter.sharedMesh != null) {
            return filter.sharedMesh.bounds;
        }
        return new Bounds(Vector3.zero, Vector3.one);
    }

    private void OnEnable() {
        Application.onBeforeRender += OnBeforeRender;
    }

    private void OnDisable() {
        Application.onBeforeRender -= OnBeforeRender;
    }

    private void LateUpdate() {
        UpdateAvatar();
    }

    private void OnBeforeRender() {
        UpdateAvatar();
    }

    private void UpdateAvatar() {
        if (!IsOwnedLocallySafe()) return;

        if (!_localVisualsHidden) HideAvatarLocally();

        if (_xrCamera == null || _xrLeftHand == null || _xrRightHand == null) {
            if (Time.unscaledTime >= _nextRebindTime) {
                _nextRebindTime = Time.unscaledTime + 0.5f;
                FindXRNodes();
            }
            if (_xrCamera == null) return;
        }

        float floorY = 0f;
        if (_xrCamera.parent != null && _xrCamera.parent.parent != null)
            floorY = _xrCamera.parent.parent.position.y;

        float heightOffset = 0f;

        Vector3 targetHeadPos = _xrCamera.position + Vector3.up * heightOffset;

        transform.position = new Vector3(_xrCamera.position.x, floorY, _xrCamera.position.z);
        transform.rotation = GetYawOnlyRotation(_xrCamera.rotation, _xrCamera.forward);

        if (head) {
            head.position = targetHeadPos;
            head.rotation = _xrCamera.rotation;
        }
        SyncHandWithOffset(leftHand, _xrLeftHand, heightOffset);
        SyncHandWithOffset(rightHand, _xrRightHand, heightOffset);

        UpdateVisualRig(floorY, targetHeadPos);
    }

    private void UpdateVisualRig(float floorY, Vector3 targetHeadPos) {
        float rawLegHeight = Mathf.Max(_legBounds.size.y, 0.01f);
        float legScaleY = targetLegHeight / rawLegHeight;
        
        float legPivotY = floorY - (_legBounds.min.y * legScaleY);

        if (leftLeg != null) {
            leftLeg.localScale = new Vector3(leftLeg.localScale.x, legScaleY, leftLeg.localScale.z);
            leftLeg.position = new Vector3(leftLeg.position.x, legPivotY, leftLeg.position.z);
        }
        if (rightLeg != null) {
            rightLeg.localScale = new Vector3(rightLeg.localScale.x, legScaleY, rightLeg.localScale.z);
            rightLeg.position = new Vector3(rightLeg.position.x, legPivotY, rightLeg.position.z);
        }

        if (body != null) {
            float topOfLegsY = floorY + targetLegHeight;
            float bottomOfHeadY = targetHeadPos.y - headToBodyOffset;
            
            float bodySpaceHeight = bottomOfHeadY - topOfLegsY;
            bodySpaceHeight = Mathf.Clamp(bodySpaceHeight, 0.05f, 5.0f);

            float rawBodyHeight = Mathf.Max(_bodyBounds.size.y, 0.01f);
            float targetBodyScaleY = bodySpaceHeight / rawBodyHeight;

            body.localScale = new Vector3(_baseBodyScale.x, targetBodyScaleY, _baseBodyScale.z);
            float targetVisualCenterY = topOfLegsY + (bodySpaceHeight / 2f);
            float bodyPivotY = targetVisualCenterY - (_bodyBounds.center.y * targetBodyScaleY);

            body.position = new Vector3(head.position.x, bodyPivotY, head.position.z);
            body.rotation = transform.rotation;
        }
    }

    private void HideAvatarLocally() {
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            r.enabled = false;
        _localVisualsHidden = true;
    }

    private void SyncHandWithOffset(Transform avatarHand, Transform xrController, float offset) {
        if (avatarHand && xrController) {
            avatarHand.position = xrController.position + Vector3.up * offset;
            avatarHand.rotation = xrController.rotation;
        }
    }

    private static Quaternion GetYawOnlyRotation(Quaternion sourceRotation, Vector3 sourceForward) {
        Vector3 flatForward = Vector3.ProjectOnPlane(sourceForward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.0001f)
            return Quaternion.Euler(0f, sourceRotation.eulerAngles.y, 0f);
        return Quaternion.LookRotation(flatForward.normalized, Vector3.up);
    }

    private bool IsOwnedLocallySafe() {
        try {
            return realtimeView != null && realtimeView.isOwnedLocallyInHierarchy;
        } catch {
            return false;
        }
    }

    private void FindXRNodes() {
        _xrCamera = xrCamera != null ? xrCamera : (Camera.main != null ? Camera.main.transform : null);
        _xrLeftHand = xrLeftHand != null ? xrLeftHand : FindTransformInScene("Left Controller");
        _xrRightHand = xrRightHand != null ? xrRightHand : FindTransformInScene("Right Controller");
    }

    private static Transform FindTransformInScene(string objectName) {
        if (string.IsNullOrWhiteSpace(objectName)) return null;
        GameObject byName = GameObject.Find(objectName);
        if (byName != null) return byName.transform;
        
        Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        foreach (var t in transforms) {
            if (t != null && string.Equals(t.name, objectName, StringComparison.OrdinalIgnoreCase))
                return t;
        }
        return null;
    }
}
