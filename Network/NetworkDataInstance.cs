using PlayFab;
using System.Collections.Generic;
using UnityEngine;

public static class NetworkDataInstance
{
    // Local friend data cache
    public static readonly Dictionary<string, FriendProfileData> NetworkFriendCache = new();

    // Get Player ID
    public static string GetLocalPlayerId() => PlayFabSettings.staticPlayer.PlayFabId;
}
