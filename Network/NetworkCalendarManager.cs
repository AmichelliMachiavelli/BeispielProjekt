using PlayFab;
using PlayFab.ClientModels;
using System;
using UnityEngine;
using System.Collections.Generic;


public sealed class NetworkCalendarManager : MonoBehaviour
{
    public static NetworkCalendarManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Loads both: the friend's calendar for me, and my own calendar for that friend.
    /// </summary>
    public void FetchFriendCalendar(string friendPlayFabId, Action<bool, CalendarDescription, CalendarDescription> onCompleted)
    {
        if (string.IsNullOrWhiteSpace(friendPlayFabId))
        {
            Debug.LogWarning("[NetworkCalendarManager] FetchFriendCalendar: Invalid friend ID.");
            onCompleted?.Invoke(false, null, null);
            return;
        }

        string myId = NetworkDataInstance.GetLocalPlayerId();
        string keyFriendToMe = $"calendar_{myId}";
        string keyMeToFriend = $"calendar_{friendPlayFabId}";

        CalendarDescription friendToMe = null;
        CalendarDescription meToFriend = null;

        var request = new GetUserDataRequest { PlayFabId = friendPlayFabId };
        var myRequest = new GetUserDataRequest { PlayFabId = myId };

        // Friend's data for me
        PlayFabClientAPI.GetUserData(request,
            friendResult =>
            {
                if (friendResult.Data != null && friendResult.Data.TryGetValue(keyFriendToMe, out var entry))
                {
                    try
                    {
                        friendToMe = JsonUtility.FromJson<CalendarDescription>(entry.Value);
                        Debug.Log($"[NetworkCalendarManager] Friend→Me calendar loaded ({friendPlayFabId} → {myId}).");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[NetworkCalendarManager] Failed to parse Friend→Me calendar: {ex.Message}");
                    }
                }
                else
                {
                    Debug.LogError($"[NetworkCalendarManager] Calendar not created yet.");
                }

                    // My data for that friend
                    PlayFabClientAPI.GetUserData(myRequest,
                        myResult =>
                        {
                            if (myResult.Data != null && myResult.Data.TryGetValue(keyMeToFriend, out var myEntry))
                            {
                                try
                                {
                                    meToFriend = JsonUtility.FromJson<CalendarDescription>(myEntry.Value);
                                    Debug.Log($"[NetworkCalendarManager] Me→Friend calendar loaded ({myId} → {friendPlayFabId}).");
                                }
                                catch (Exception ex)
                                {
                                    Debug.LogError($"[NetworkCalendarManager] Failed to parse Me→Friend calendar: {ex.Message}");
                                }
                            }

                            // Update cache if exists
                            if (NetworkDataInstance.NetworkFriendCache.TryGetValue(friendPlayFabId, out var cached))
                            {
                                cached.Calendar = friendToMe ?? meToFriend;
                            }

                            onCompleted?.Invoke(true, friendToMe, meToFriend);
                        },
                        error =>
                        {
                            Debug.LogError($"[NetworkCalendarManager] Error fetching my calendar: {error.GenerateErrorReport()}");
                            onCompleted?.Invoke(true, friendToMe, null);
                        });
            },
            error =>
            {
                Debug.LogError($"[NetworkCalendarManager] Error fetching friend's calendar: {error.GenerateErrorReport()}");
                onCompleted?.Invoke(false, null, null);
            });
    }

    /// <summary>
    /// Uploads the currently configured calendar layout for the selected friend.
    /// Stored in my user data under the key "calendar_{friendPlayFabId}".
    /// </summary>
    public void UploadCalendarForFriend(string friendPlayFabId, string personalNote, List<DoorConfigurationData> doors, string startDate, string endDate)
    {
        if (string.IsNullOrWhiteSpace(friendPlayFabId))
        {
            Debug.LogWarning("[NetworkCalendarManager] UploadCalendarForFriend: Invalid friend ID.");
            return;
        }

        string myId = NetworkDataInstance.GetLocalPlayerId();
        string key = $"calendar_{friendPlayFabId}";

        var doorDescriptions = new List<CalendarDoorDescription>();
        for (int i = 0; i < doors.Count; i++)
        {
            var d = doors[i];
            doorDescriptions.Add(new CalendarDoorDescription(d.Index, d.PersonalNote, d.GameType));
        }

        var calendar = new CalendarDescription(
            startDate,
            endDate,
            personalNote,
            doors: doorDescriptions.ToArray()
        );

        string json = JsonUtility.ToJson(calendar);

        var updateRequest = new UpdateUserDataRequest
        {
            Data = new Dictionary<string, string> { { key, json } },
            Permission = UserDataPermission.Public
        };

        PlayFabClientAPI.UpdateUserData(updateRequest,
            result =>
            {
                Debug.Log($"[NetworkCalendarManager] Uploaded calendar for friend {friendPlayFabId} under key {key}.");
            },
            error =>
            {
                Debug.LogError($"[NetworkCalendarManager] Failed to upload calendar: {error.GenerateErrorReport()}");
            });
    }
}


// Data Structures

/// <summary>
/// Holds a cached friend profile and any associated calendar data.
/// </summary>
[Serializable]
public sealed class FriendProfileData
{
    public string PlayFabId;
    public string DisplayName;
    public Sprite AvatarSprite;
    public CalendarDescription Calendar;
}

/// <summary>
/// JSON-serializable description of a calendar configuration.
/// </summary>
[Serializable]
public sealed class CalendarDescription
{
    public string StartDate;
    public string EndDate;
    public string CalendarID;
    public string PersonalMessage;
    public CalendarDoorDescription[] Doors;

    public CalendarDescription(string startDate, string endDate, string personalMessage, CalendarDoorDescription[] doors)
    {
        StartDate = startDate;
        EndDate = endDate;
        Doors = doors;
        PersonalMessage = personalMessage;
    }
}

/// <summary>
/// JSON-serializable description of a calendar doors.
/// </summary>
[Serializable]
public sealed class CalendarDoorDescription
{
    public int DoorDay;
    public string PersonalNote;
    public DoorGameType DoorGameType;

    public CalendarDoorDescription(int doorIndex, string peronalNote, DoorGameType doorType)
    {
        DoorDay = doorIndex;
        PersonalNote = peronalNote;
        DoorGameType = doorType;
    }
}

public enum DoorGameType
{
    FLOPPY_BIRD = 0,
    MEMORY_MASTER = 1,
    MAJONG = 2,
    SNAKE = 3,
    PONG = 4,
}