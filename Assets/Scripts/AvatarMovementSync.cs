using UnityEngine;
using Normal.Realtime;

public class AvatarMovementSync : RealtimeComponent<AvatarMovementModel>
{
    [Header("Settings")]
    public float walkingSpeedThreshold = 0.1f;

    private Animator _animator;
    private AvatarUserTag _userTag;
    private Vector3 _lastPosition;
    private float _currentSpeed;
    private bool _isWalking;
    private bool _initialized;

    private const string WALKING_BOOL = "isWalking";

    void Start()
    {
        _animator = GetComponentInChildren<Animator>();
        _userTag = GetComponentInParent<AvatarUserTag>();
        _lastPosition = transform.position;
        _initialized = true;
    }

    void Update()
    {
        if (!_initialized) return;
        if (!IsOwnedLocally()) return;

        CalculateMovement();
        UpdateAnimator();
        UpdateUserTag();
        UpdateNetworkState();
    }

    private bool IsOwnedLocally()
    {
        try { return realtimeView != null && realtimeView.isOwnedLocallyInHierarchy; }
        catch { return true; }
    }

    private void CalculateMovement()
    {
        Vector3 currentPosition = transform.position;
        float distance = Vector3.Distance(currentPosition, _lastPosition);
        _currentSpeed = distance / Time.deltaTime;
        _lastPosition = currentPosition;

        _isWalking = _currentSpeed > walkingSpeedThreshold;
    }

    private void UpdateAnimator()
    {
        if (_animator == null) return;

        if (_animator.GetBool(WALKING_BOOL) != _isWalking)
        {
            _animator.SetBool(WALKING_BOOL, _isWalking);
        }
    }

    private void UpdateUserTag()
    {
        if (_userTag == null) return;
        if (_userTag.isWalking == _isWalking) return;

        _userTag.isWalking = _isWalking;
    }

    private void UpdateNetworkState()
    {
        if (model == null) return;
        if (model.isWalking != _isWalking)
        {
            model.isWalking = _isWalking;
        }
    }

    protected override void OnRealtimeModelReplaced(AvatarMovementModel previousModel, AvatarMovementModel newModel)
    {
        base.OnRealtimeModelReplaced(previousModel, newModel);

        if (newModel != null)
        {
            newModel.isWalkingDidChange += OnIsWalkingChanged;
        }
    }

    private void OnIsWalkingChanged(AvatarMovementModel model, bool isWalking)
    {
        if (IsOwnedLocally()) return;

        if (_animator != null && _animator.GetBool(WALKING_BOOL) != isWalking)
        {
            _animator.SetBool(WALKING_BOOL, isWalking);
        }

        if (_userTag != null)
        {
            _userTag.isWalking = isWalking;
        }
    }

    public bool IsWalking => _isWalking;
    public float CurrentSpeed => _currentSpeed;
}