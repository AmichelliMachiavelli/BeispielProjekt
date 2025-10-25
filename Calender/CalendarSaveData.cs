using System;
using System.Collections.Generic;

[Serializable]
public sealed class CalendarSaveData
{
    public string CalendarId;
    public DoorSaveData[] Doors;

    public CalendarSaveData(string calendarId)
    {
        CalendarId = string.IsNullOrWhiteSpace(calendarId) ? "calendar_default" : calendarId;
        Doors = Array.Empty<DoorSaveData>();
    }

    public DoorSaveData GetOrCreateDoor(int doorIndex)
    {
        if (doorIndex <= 0)
        {
            doorIndex = 1;
        }

        if (Doors != null)
        {
            for (int i = 0; i < Doors.Length; i++)
            {
                DoorSaveData existing = Doors[i];
                if (existing != null && existing.DoorIndex == doorIndex)
                {
                    return existing;
                }
            }
        }
        else
        {
            Doors = Array.Empty<DoorSaveData>();
        }

        var door = new DoorSaveData(doorIndex);
        var list = new List<DoorSaveData>(Doors) { door };
        Doors = list.ToArray();
        return door;
    }

    public void UpdateDoor(DoorSaveData doorData)
    {
        if (doorData == null)
        {
            return;
        }

        if (Doors == null || Doors.Length == 0)
        {
            Doors = new[] { doorData };
            return;
        }

        for (int i = 0; i < Doors.Length; i++)
        {
            DoorSaveData existing = Doors[i];
            if (existing != null && existing.DoorIndex == doorData.DoorIndex)
            {
                Doors[i] = doorData;
                return;
            }
        }

        var list = new List<DoorSaveData>(Doors) { doorData };
        Doors = list.ToArray();
    }
}

[Serializable]
public sealed class DoorSaveData
{
    public int DoorIndex;
    public bool DoorSuccessful;
    public int AdditionalRerollTokens;

    public DoorSaveData(int doorIndex)
    {
        DoorIndex = Math.Max(1, doorIndex);
        DoorSuccessful = false;
        AdditionalRerollTokens = 0;
    }

    public void RegisterSuccess()
    {
        if (DoorSuccessful)
        {
            AdditionalRerollTokens++;
        }
        else
        {
            DoorSuccessful = true;
        }
    }

    public bool ConsumeRerollToken()
    {
        if (AdditionalRerollTokens <= 0)
        {
            return false;
        }

        AdditionalRerollTokens--;
        return true;
    }
}
