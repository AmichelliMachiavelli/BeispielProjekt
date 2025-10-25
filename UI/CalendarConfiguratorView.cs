using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CalendarConfiguratorView : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField startDateInput;
    [SerializeField] private TMP_InputField endDateInput;
    [SerializeField] private TMP_InputField personalNoteInput;
    [SerializeField] private Button generateButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private RectTransform doorListRoot;
    [SerializeField] private GameObject doorItemPrefab;

    [SerializeField] private CalendarDoorConfigEditorView editorView;
    [SerializeField] private FriendView friendView;

    public event Action<List<DoorConfigurationData>> OnCalendarLayoutSaved;
    public event Action<DoorConfigurationData> OnCalendarDoorConfigClicked;
    public event Action OnBackClicked;

    private readonly List<CalendarDoorConfigItemView> _spawnedItems = new();
    private CalendarDescription _cachedCalendarDescription;

    private void Awake()
    {
        if (generateButton != null)
            generateButton.onClick.AddListener(GenerateDoors);

        if (saveButton != null)
            saveButton.onClick.AddListener(SaveConfiguration);

        if (friendView != null)
        {
            friendView.OnMeToFriendCalendarLoaded += SetCalendarDescriptionData;
            friendView.OnCalenderConfigClicked += UpdateValuesByCalenderDescription;
        }

        OnCalendarLayoutSaved += doors =>
        {
            var startDate = startDateInput.text;
            var endDate = endDateInput.text;
            var personalNote = personalNoteInput.text;  
            var currentFriend = FriendView.CurrentSelectedFriend;

            // Update cache
            _cachedCalendarDescription = new CalendarDescription(startDate, endDate, personalNote, DoorConfigWrapper.WrapConfigToDescription(doors.ToArray()));

            NetworkCalendarManager.Instance.UploadCalendarForFriend(currentFriend.PlayFabId, personalNote, doors, startDate, endDate);
        };
    }

    private void OnDestroy()
    {
        if (generateButton != null)
            generateButton.onClick.RemoveListener(GenerateDoors);

        if (saveButton != null)
            saveButton.onClick.RemoveListener(SaveConfiguration);

        if (friendView != null)
        {
            friendView.OnMeToFriendCalendarLoaded -= SetCalendarDescriptionData;
            friendView.OnCalenderConfigClicked -= UpdateValuesByCalenderDescription;
        }
    }

    private void SetCalendarDescriptionData(CalendarDescription description)
    {
        _cachedCalendarDescription = description;
    }

    private void UpdateValuesByCalenderDescription()
    {
        if (_cachedCalendarDescription == null)
        {
            Debug.Log("[CalendarConfiguratorView] No cached calendar to load.");
            return;
        }

        Debug.Log("[CalendarConfiguratorView] Loading cached calendar data into configurator.");

        startDateInput.text = _cachedCalendarDescription.StartDate;
        endDateInput.text = _cachedCalendarDescription.EndDate;
        personalNoteInput.text = _cachedCalendarDescription.PersonalMessage;

        foreach (Transform child in doorListRoot)
            Destroy(child.gameObject);
        _spawnedItems.Clear();

        if (_cachedCalendarDescription.Doors == null || _cachedCalendarDescription.Doors.Length == 0)
        {
            Debug.LogWarning("[CalendarConfiguratorView] Cached calendar has no doors, generating from dates.");
            GenerateDoors();
            return;
        }

        DateTime.TryParse(_cachedCalendarDescription.StartDate, out var start);
        for (int i = 0; i < _cachedCalendarDescription.Doors.Length; i++)
        {
            var door = _cachedCalendarDescription.Doors[i];
            var itemGO = Instantiate(doorItemPrefab, doorListRoot);
            var item = itemGO.GetComponent<CalendarDoorConfigItemView>();
            if (item == null)
                continue;

            DateTime date = start.AddDays(i);
            item.Initialize(door, date, data =>
            {
                editorView.Open(_cachedCalendarDescription, data);
                OnCalendarDoorConfigClicked?.Invoke(data);
            });

            // optional: prefill name/type if available
            var configData = item.GetDoorConfig();
            configData.PersonalNote = door.PersonalNote;
            configData.GameType = door.DoorGameType;
            item.ApplyConfig(configData);

            _spawnedItems.Add(item);
        }

        Debug.Log($"[CalendarConfiguratorView] Applied cached calendar with {_cachedCalendarDescription.Doors.Length} doors.");
    }

    private void GenerateDoors()
    {
        if (string.IsNullOrWhiteSpace(startDateInput.text) ||
            string.IsNullOrWhiteSpace(endDateInput.text))
        {
            Debug.LogWarning("[CalendarConfiguratorView] Start or End date missing.");
            return;
        }

        if (!DateTime.TryParse(startDateInput.text, out var start) ||
            !DateTime.TryParse(endDateInput.text, out var end))
        {
            Debug.LogWarning("[CalendarConfiguratorView] Invalid date format.");
            return;
        }

        if (start >= end)
        {
            Debug.LogWarning("[CalendarConfiguratorView] Start date must be before end date.");
            return;
        }

        foreach (Transform child in doorListRoot)
            Destroy(child.gameObject);
        _spawnedItems.Clear();

        int totalDays = (end - start).Days + 1;
        for (int i = 0; i < totalDays; i++)
        {
            var itemGO = Instantiate(doorItemPrefab, doorListRoot);
            var item = itemGO.GetComponent<CalendarDoorConfigItemView>();
            if (item == null)
                continue;

            DateTime date = start.AddDays(i);
            item.Initialize(new CalendarDoorDescription(i+1, "", DoorGameType.FLOPPY_BIRD), date, data =>
            {
                editorView.Open(_cachedCalendarDescription ,data);
                OnCalendarDoorConfigClicked?.Invoke(data);
            });
            _spawnedItems.Add(item);
        }

        Debug.Log($"[CalendarConfiguratorView] Generated {totalDays} door items.");
    }

    private void SaveConfiguration()
    {
        var configs = new List<DoorConfigurationData>();
        foreach (var item in _spawnedItems)
        {
            configs.Add(item.GetDoorConfig());
        }

        OnCalendarLayoutSaved?.Invoke(configs);
        OnBackClicked?.Invoke();

        Debug.Log("[CalendarConfiguratorView] Calendar configuration saved (upload pending).");
    }
}
