using System;

[Serializable]
public sealed class DoorConfigurationData
{
    public int Index;
    public string PersonalNote;
    public DoorGameType GameType;
}

public sealed class DoorConfigWrapper
{
    public static CalendarDoorDescription[] WrapConfigToDescription(DoorConfigurationData[] data)
    {
        CalendarDoorDescription[] wrapped = new CalendarDoorDescription[data.Length];

        for(int i = 0; i < data.Length; i++)
        {
            wrapped[i] = new CalendarDoorDescription(data[i].Index, data[i].PersonalNote, data[i].GameType);
        }

        return wrapped; 
    }
}
