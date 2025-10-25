using System;
using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

public sealed class NetworkCalendarSavestatManager : MonoBehaviour
{
    [SerializeField] private string saveKeyPrefix = "calendarProgress_";

    public static NetworkCalendarSavestatManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[NetworkCalendarSavestatManager] Duplicate instance detected. Destroying the newest instance.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void LoadCalendarSaveData(string calendarId, Action<bool, CalendarSaveData> onCompleted)
    {
        string key = BuildKey(calendarId);

        var request = new GetUserDataRequest
        {
            Keys = new List<string> { key }
        };

        PlayFabClientAPI.GetUserData(request,
            result =>
            {
                CalendarSaveData data = null;
                if (result.Data != null && result.Data.TryGetValue(key, out var record) && !string.IsNullOrWhiteSpace(record.Value))
                {
                    data = JsonUtility.FromJson<CalendarSaveData>(record.Value);
                }

                if (data == null)
                {
                    data = new CalendarSaveData(calendarId);
                }
                else if (string.IsNullOrWhiteSpace(data.CalendarId))
                {
                    data.CalendarId = calendarId;
                }

                onCompleted?.Invoke(true, data);
            },
            error =>
            {
                Debug.LogWarning($"[NetworkCalendarSavestatManager] Failed to load calendar save data: {error.GenerateErrorReport()}");
                onCompleted?.Invoke(false, new CalendarSaveData(calendarId));
            });
    }

    public void SaveCalendarSaveData(CalendarSaveData data, Action<bool> onCompleted = null)
    {
        if (data == null)
        {
            Debug.LogWarning("[NetworkCalendarSavestatManager] Attempted to save null data.");
            onCompleted?.Invoke(false);
            return;
        }

        string key = BuildKey(data.CalendarId);
        string json = JsonUtility.ToJson(data);

        var request = new UpdateUserDataRequest
        {
            Data = new Dictionary<string, string> { { key, json } },
            Permission = UserDataPermission.Private
        };

        PlayFabClientAPI.UpdateUserData(request,
            result =>
            {
                Debug.Log($"[NetworkCalendarSavestatManager] Saved calendar progress under key {key}.");
                onCompleted?.Invoke(true);
            },
            error =>
            {
                Debug.LogError($"[NetworkCalendarSavestatManager] Failed to save calendar progress: {error.GenerateErrorReport()}");
                onCompleted?.Invoke(false);
            });
    }

    private string BuildKey(string calendarId)
    {
        calendarId = string.IsNullOrWhiteSpace(calendarId) ? "calendar_default" : calendarId;
        return $"{saveKeyPrefix}{calendarId}";
    }
}
