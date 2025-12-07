using Normal.Realtime;
using Normal.Realtime.Serialization;
using UnityEngine;

[RealtimeModel]
public partial class AvatarColorModel {
    // 1) Body color (doctor cyan / patient random)
    [RealtimeProperty(1, true, true)]
    private Color _bodyColor;

    // 2) Display name that everyone sees ("Dr. username" or "username")
    [RealtimeProperty(2, true, true)]
    private string _displayName;
}

