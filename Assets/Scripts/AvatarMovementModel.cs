using Normal.Realtime;
using Normal.Realtime.Serialization;
using UnityEngine;

[RealtimeModel]
public partial class AvatarMovementModel
{
    [RealtimeProperty(1, true, true)]
    private bool _isWalking;
}