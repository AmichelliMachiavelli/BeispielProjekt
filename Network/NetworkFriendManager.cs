using System;
using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

/// <summary>
/// Handles PlayFab friend networking, calendar loading, and local caching.
/// </summary>
public sealed class NetworkFriendManager : MonoBehaviour
{
    public static NetworkFriendManager Instance { get; private set; }

    [SerializeField] private FriendManagerView friendManagerView;
    [SerializeField] private NetworkStateMachine networkStateMachine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (friendManagerView != null)
            friendManagerView.OnAddFriend += AddFriendByPlayFabId;

        if (networkStateMachine != null)
            networkStateMachine.OnLoginSuccess += () =>
            {
                FetchFriendList();
            };
    }

    private void OnDestroy()
    {
        if (friendManagerView != null)
            friendManagerView.OnAddFriend -= AddFriendByPlayFabId;

        if (networkStateMachine != null)
            networkStateMachine.OnLoginSuccess -= () =>
            {
                FetchFriendList();
            };
    }

    // Friend Management
    private void AddFriendByPlayFabId(string friendPlayFabId)
    {
        if (string.IsNullOrWhiteSpace(friendPlayFabId))
        {
            Debug.LogWarning("[NetworkFriendManager] Friend PlayFab ID is null or empty.");
            return;
        }

        var request = new AddFriendRequest
        {
            FriendPlayFabId = friendPlayFabId
        };

        PlayFabClientAPI.AddFriend(request,
            result =>
            {
                Debug.Log($"[NetworkFriendManager] Friend added successfully: {friendPlayFabId}");
                FetchFriendList();
            },
            error =>
            {
                Debug.LogError($"[NetworkFriendManager] Failed to add friend: {error.GenerateErrorReport()}");
                FetchFriendList();
            });
    }

    public void RemoveFriend(string friendPlayFabId, Action<bool> onCompleted = null)
    {
        if (string.IsNullOrWhiteSpace(friendPlayFabId))
        {
            Debug.LogWarning("[NetworkFriendManager] RemoveFriend: PlayFab ID invalid.");
            onCompleted?.Invoke(false);
            return;
        }

        var request = new RemoveFriendRequest
        {
            FriendPlayFabId = friendPlayFabId
        };

        PlayFabClientAPI.RemoveFriend(request,
            result =>
            {
                Debug.Log($"[NetworkFriendManager] Friend removed: {friendPlayFabId}");
                NetworkDataInstance.NetworkFriendCache.Remove(friendPlayFabId);
                FetchFriendList();
                onCompleted?.Invoke(true);
            },
            error =>
            {
                Debug.LogError($"[NetworkFriendManager] Failed to remove friend: {error.GenerateErrorReport()}");
                onCompleted?.Invoke(false);
            });
    }

    public void FetchFriendList(Action<List<FriendProfileData>> onFetched = null)
    {
        var request = new GetFriendsListRequest
        {
            ProfileConstraints = new PlayerProfileViewConstraints
            {
                ShowDisplayName = true,
                ShowAvatarUrl = true
            }
        };

        PlayFabClientAPI.GetFriendsList(request,
            result =>
            {
                Debug.Log($"[NetworkFriendManager] Loaded {result.Friends.Count} friends from server.");

                var friends = new List<FriendProfileData>();

                foreach (var f in result.Friends)
                {
                    string id = f.FriendPlayFabId;
                    string name = f.TitleDisplayName ?? f.Username ?? id;

                    if (!NetworkDataInstance.NetworkFriendCache.TryGetValue(id, out var profile))
                    {
                        profile = new FriendProfileData
                        {
                            PlayFabId = id,
                            DisplayName = name,
                            //TODO fetch async
                            AvatarSprite = null// f.Profile?.AvatarUrl ?? string.Empty
                        };
                        NetworkDataInstance.NetworkFriendCache[id] = profile;
                    }
                    else
                    {
                        profile.DisplayName = name;
                        // TODO fetch avatar async
                        profile.AvatarSprite = null;// f.Profile?.AvatarUrl ?? string.Empty;
                    }

                    friends.Add(profile);
                }

                friendManagerView?.RefreshFriendList(friends);
                onFetched?.Invoke(friends);
            },
            error =>
            {
                Debug.LogError($"[NetworkFriendManager] Error fetching friends: {error.GenerateErrorReport()}");
                onFetched?.Invoke(null);
            });
    }
}