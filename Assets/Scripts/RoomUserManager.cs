using Normal.Realtime;
using System;
using System.Collections.Generic;
using UnityEngine;

public class RoomUserManager : MonoBehaviour
{
    public static RoomUserManager Instance { get; private set; }

    private Realtime _realtime;
    private RealtimeAvatarManager _avatarManager;

    private Dictionary<int, UserInfo> _users = new Dictionary<int, UserInfo>();
    private int _localClientID;

    [System.Serializable]
    public struct UserInfo
    {
        public string username;
        public string role;
        public string displayName;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        _realtime = FindObjectOfType<Realtime>();
        _avatarManager = FindObjectOfType<RealtimeAvatarManager>();

        if (_realtime != null)
        {
            _realtime.didConnectToRoom += OnConnectedToRoom;
            _realtime.didDisconnectFromRoom += OnDisconnectedFromRoom;
        }

        if (_avatarManager != null)
        {
            _avatarManager.avatarCreated += OnAvatarCreated;
            _avatarManager.avatarDestroyed += OnAvatarDestroyed;
        }
    }

    void OnDestroy()
    {
        if (_realtime != null)
        {
            _realtime.didConnectToRoom -= OnConnectedToRoom;
            _realtime.didDisconnectFromRoom -= OnDisconnectedFromRoom;
        }

        if (_avatarManager != null)
        {
            _avatarManager.avatarCreated -= OnAvatarCreated;
            _avatarManager.avatarDestroyed -= OnAvatarDestroyed;
        }
    }

    private void OnConnectedToRoom(Realtime room)
    {
        _localClientID = room.clientID;
        RegisterLocalUser();
    }

    private void OnDisconnectedFromRoom(Realtime room)
    {
        _users.Clear();
        _localClientID = 0;
    }

    private void OnAvatarCreated(RealtimeAvatarManager manager, RealtimeAvatar avatar, bool isLocalAvatar)
    {
        if (avatar == null || avatar.realtimeView == null) return;

        int clientId = avatar.realtimeView.ownerID;

        if (isLocalAvatar && SessionData.CurrentUser != null)
        {
            _users[clientId] = new UserInfo
            {
                username = SessionData.CurrentUser.username,
                role = SessionData.CurrentUser.role,
                displayName = (SessionData.CurrentUser.role == "Doctor" ? "Dr:" : "Patient:") + SessionData.CurrentUser.username
            };
        }
        else
        {
            var tag = avatar.gameObject.GetComponent<AvatarUserTag>();
            if (tag != null && !string.IsNullOrWhiteSpace(tag.displayName))
            {
                _users[clientId] = new UserInfo
                {
                    username = tag.displayName,
                    role = tag.role,
                    displayName = tag.displayName
                };
            }
        }

        UpdateAllAvatarTags();
    }

    private void OnAvatarDestroyed(RealtimeAvatarManager manager, RealtimeAvatar avatar, bool isLocalAvatar)
    {
        if (avatar == null || avatar.realtimeView == null) return;

        int clientId = avatar.realtimeView.ownerID;
        _users.Remove(clientId);
    }

    private void RegisterLocalUser()
    {
        if (SessionData.CurrentUser == null) return;

        var userInfo = new UserInfo
        {
            username = SessionData.CurrentUser.username,
            role = SessionData.CurrentUser.role,
            displayName = (SessionData.CurrentUser.role == "Doctor" ? "Dr:" : "Patient:") + SessionData.CurrentUser.username
        };

        if (_localClientID > 0)
        {
            _users[_localClientID] = userInfo;
        }

        Debug.Log($"[RoomUserManager] Registered local user: {userInfo.username} as {userInfo.role}");

        Invoke("UpdateAllAvatarTags", 1f);
    }

    public void UpdateAllAvatarTags()
    {
        if (_avatarManager == null) return;

        var avatars = FindObjectsOfType<RealtimeAvatar>(true);

        foreach (var avatar in avatars)
        {
            if (avatar == null || avatar.realtimeView == null) continue;

            int clientId = -1;
            try
            {
                clientId = avatar.realtimeView.ownerID;
            }
            catch
            {
                continue;
            }

            if (_users.TryGetValue(clientId, out UserInfo userInfo))
            {
                var tag = avatar.gameObject.GetComponent<AvatarUserTag>();
                if (tag == null)
                {
                    tag = avatar.gameObject.AddComponent<AvatarUserTag>();
                }

                tag.displayName = userInfo.displayName;
                tag.role = userInfo.role;
            }
        }
    }

    public static UserInfo? GetUserInfo(int clientId)
    {
        if (Instance == null || !Instance._users.ContainsKey(clientId))
            return null;

        return Instance._users[clientId];
    }

    public static UserInfo? GetLocalUserInfo()
    {
        if (Instance == null || Instance._localClientID <= 0)
            return null;

        return GetUserInfo(Instance._localClientID);
    }
}