using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CalendarDoorLiveView : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image gameImage;
    [SerializeField] private TMP_Text descriptionLabel;
    [SerializeField] private TMP_Text personalNoteLabel;
    [SerializeField] private TMP_Text dateLabel;
    [SerializeField] private TMP_Text dayLabel;
    [SerializeField] private Button playButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Button rewardButton;

    public event Action BackRequested;
    public event Action PlayRequested;
    public event Action GiftRequested;

    private CalendarDescription _activeCalendar;
    private CalendarDoorDescription _activeDoor;
    private CalendarSaveData _currentSaveData;
    private DoorSaveData _activeDoorSaveData;
    private bool _isSaveDataLoaded;
    private bool _isGameRunning;

    private void Awake()
    {
        if (playButton != null)
        {
            playButton.onClick.AddListener(HandlePlayClicked);
        }

        if (backButton != null)
        {
            backButton.onClick.AddListener(HandleBackClicked);
        }

        if (rewardButton != null)
        {
            rewardButton.onClick.AddListener(HandleRewardClicked);
        }
    }

    private void OnDestroy()
    {
        if (playButton != null)
        {
            playButton.onClick.RemoveListener(HandlePlayClicked);
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveListener(HandleBackClicked);
        }

        if (rewardButton != null)
        {
            rewardButton.onClick.RemoveListener(HandleRewardClicked);
        }
    }

    public void LoadDoor(CalendarDescription calendar, CalendarDoorDescription door)
    {
        _activeCalendar = calendar;
        _activeDoor = door ?? new CalendarDoorDescription(1, string.Empty, DoorGameType.FLOPPY_BIRD);
        _currentSaveData = null;
        _activeDoorSaveData = null;
        _isSaveDataLoaded = false;
        _isGameRunning = false;

        UpdateVisuals();
        RefreshButtons();

        string calendarId = ResolveCalendarId(calendar);
        var manager = NetworkCalendarSavestatManager.Instance;

        if (manager != null)
        {
            manager.LoadCalendarSaveData(calendarId, HandleSaveDataLoaded);
        }
        else
        {
            Debug.LogWarning("[CalendarDoorLiveView] NetworkCalendarSavestatManager missing. Using local save data instance.");
            HandleSaveDataLoaded(false, new CalendarSaveData(calendarId));
        }
    }

    private void HandleSaveDataLoaded(bool success, CalendarSaveData data)
    {
        string calendarId = ResolveCalendarId(_activeCalendar);
        _currentSaveData = data ?? new CalendarSaveData(calendarId);
        _currentSaveData.CalendarId = calendarId;
        _activeDoorSaveData = EnsureDoorSaveData();
        _isSaveDataLoaded = true;
        RefreshButtons();
    }

    private void HandlePlayClicked()
    {
        if (_isGameRunning || !_isSaveDataLoaded || _activeDoor == null)
        {
            return;
        }

        if (PlayMode.Instance == null)
        {
            Debug.LogWarning("[CalendarDoorLiveView] PlayMode instance missing.");
            return;
        }

        _isGameRunning = true;
        RefreshButtons();

        PlayRequested?.Invoke();

        PlayMode.Instance.StartGame(_activeDoor.DoorGameType, OnGameFinished);
    }

    private void OnGameFinished(bool wasSuccessful)
    {
        _isGameRunning = false;

        if (!wasSuccessful)
        {
            RefreshButtons();
            return;
        }

        _activeDoorSaveData ??= EnsureDoorSaveData();
        if (_activeDoorSaveData == null)
        {
            Debug.LogWarning("[CalendarDoorLiveView] Unable to track door save data after game completion.");
            RefreshButtons();
            return;
        }

        _activeDoorSaveData.RegisterSuccess();

        PersistSaveData();
        RefreshButtons();
    }

    private void HandleRewardClicked()
    {
        if (!_isSaveDataLoaded || _activeDoorSaveData == null || !_activeDoorSaveData.DoorSuccessful)
        {
            Debug.LogWarning("[CalendarDoorLiveView] Reward requested before door completion.");
            return;
        }

        if (GiftCenter.Instance == null)
        {
            Debug.LogWarning("[CalendarDoorLiveView] GiftCenter instance missing.");
            return;
        }

        GiftRequested?.Invoke();

        bool usedReroll = _activeDoorSaveData.AdditionalRerollTokens > 0 && GiftCenter.Instance.SupportsReroll;

        if (usedReroll)
        {
            bool consumed = _activeDoorSaveData.ConsumeRerollToken();
            if (consumed)
            {
                GiftCenter.Instance.RerollReward(_activeCalendar, _activeDoor, _activeDoorSaveData);
            }
            else
            {
                GiftCenter.Instance.ClaimReward(_activeCalendar, _activeDoor, _activeDoorSaveData);
            }
        }
        else
        {
            GiftCenter.Instance.ClaimReward(_activeCalendar, _activeDoor, _activeDoorSaveData);
        }

        PersistSaveData();
        RefreshButtons();
    }

    private void HandleBackClicked()
    {
        BackRequested?.Invoke();
    }

    private void UpdateVisuals()
    {
        UpdateDayLabel();
        UpdateDateLabel();
        UpdateGameDetails();
        UpdatePersonalNote();
    }

    private void UpdatePersonalNote()
    {
        if (personalNoteLabel == null)
        {
            return;
        }

        personalNoteLabel.text = _activeDoor != null ? _activeDoor.PersonalNote : "Roses are red, violets are blue, here is definitly no note for You.";
    }

    private void UpdateDayLabel()
    {
        if (dayLabel == null)
        {
            return;
        }

        dayLabel.text = _activeDoor != null ? _activeDoor.DoorDay.ToString("00") : "--";
    }

    private void UpdateDateLabel()
    {
        if (dateLabel == null)
        {
            return;
        }

        if (_activeCalendar != null && _activeDoor != null && CalendarTimeUtility.TryGetDoorDate(_activeCalendar, _activeDoor.DoorDay, out var date))
        {
            dateLabel.text = date.ToString("dd.MM.yyyy");
        }
        else
        {
            dateLabel.text = "--";
        }
    }

    private void UpdateGameDetails()
    {
        GameLibrary.GameDefinition definition = null;
        if (_activeDoor != null && GameLibrary.Instance != null)
        {
            GameLibrary.Instance.TryGetGame(_activeDoor.DoorGameType, out definition);
        }

        if (descriptionLabel != null)
        {
            string description = definition != null ? definition.Description : string.Empty;

            if (string.IsNullOrWhiteSpace(description))
            {
                description = "No description available.";
            }

            descriptionLabel.text = description;
        }

        if (gameImage != null)
        {
            Sprite sprite = definition != null ? definition.Sprite : null;
            gameImage.sprite = sprite;
            gameImage.enabled = sprite != null;
        }
    }

    private void RefreshButtons()
    {
        bool canInteract = _isSaveDataLoaded && !_isGameRunning && _activeDoor != null;

        if (playButton != null)
        {
            playButton.interactable = _isSaveDataLoaded && !_isGameRunning && _activeDoor != null;
        }

        if (rewardButton != null)
        {
            bool canReward = canInteract && _activeDoorSaveData != null && _activeDoorSaveData.DoorSuccessful;
            rewardButton.interactable = canReward;
        }
    }

    private DoorSaveData EnsureDoorSaveData()
    {
        if (_currentSaveData == null || _activeDoor == null)
        {
            return null;
        }

        return _currentSaveData.GetOrCreateDoor(_activeDoor.DoorDay);
    }

    private void PersistSaveData()
    {
        if (_currentSaveData == null)
        {
            return;
        }

        if (_activeDoorSaveData != null)
        {
            _currentSaveData.UpdateDoor(_activeDoorSaveData);
        }

        var manager = NetworkCalendarSavestatManager.Instance;
        if (manager != null)
        {
            manager.SaveCalendarSaveData(_currentSaveData);
        }
        else
        {
            Debug.LogWarning("[CalendarDoorLiveView] Cannot persist save data without manager instance.");
        }
    }

    private string ResolveCalendarId(CalendarDescription calendar)
    {
        if (calendar == null)
        {
            return "calendar_default";
        }

        if (!string.IsNullOrWhiteSpace(calendar.CalendarID))
        {
            return calendar.CalendarID;
        }

        string start = string.IsNullOrWhiteSpace(calendar.StartDate) ? "start" : calendar.StartDate;
        string end = string.IsNullOrWhiteSpace(calendar.EndDate) ? "end" : calendar.EndDate;
        return $"{start}_{end}";
    }
}
