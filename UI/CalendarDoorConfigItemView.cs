using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CalendarDoorConfigItemView : MonoBehaviour
{
    [SerializeField] private TMP_Text indexLabel;
    [SerializeField] private TMP_Text dateLabel;
    [SerializeField] private Button configureButton;

    private DoorConfigurationData _data;

    public void Initialize(CalendarDoorDescription data, DateTime date, Action<DoorConfigurationData> OnDoorClicked)
    {
        _data = new DoorConfigurationData
        {
            Index = data.DoorDay,
            PersonalNote = data.PersonalNote,
            GameType = data.DoorGameType
        };

        if (indexLabel != null)
            indexLabel.text = $"#{_data.Index}";

        if (dateLabel != null)
            dateLabel.text = date.ToString("dd.MM.yyyy");

        if (configureButton != null)
        {
            configureButton.onClick.RemoveAllListeners();
            configureButton.onClick.AddListener(() => OnDoorClicked?.Invoke(_data));
        }
    }

    public DoorConfigurationData GetDoorConfig() => _data;

    public void ApplyConfig(DoorConfigurationData config) => _data = config;
}
