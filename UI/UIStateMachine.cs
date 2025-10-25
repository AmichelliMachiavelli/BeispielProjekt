using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class UIStateMachine : MonoBehaviour
{
    [System.Serializable]
    private sealed class StateChangedEvent : UnityEvent<string> { }

    [System.Serializable]
    private sealed class WindowDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private GameObject window;
        public string Id => id;
        public GameObject Window => window;
    }

    [SerializeField] private WindowDefinition[] windows;
    [SerializeField] private bool hideAllOnAwake = true;
    [SerializeField] private bool switchToInitialStateOnAwake = true;
    [SerializeField] private StateChangedEvent onStateChanged;

    [Header("Subsystem Views")]
    [SerializeField] private CalendarDoorSpawner calendarDoorSpawner;
    [SerializeField] private FriendView friendView;
    [SerializeField] private CalendarConfiguratorView calendarConfiguratorView;
    [SerializeField] private CalendarDoorConfigEditorView calendarDoorEditorView;
    [SerializeField] private CalendarDoorLiveView calendarDoorLiveView;
    [SerializeField] private GiftCenter giftCenterView;
    [SerializeField] private PlayMode playModeView;

    [Header("State IDs")]
    [SerializeField] private string loginStateId = "login";
    [SerializeField] private string mainStateId = "main";
    [SerializeField] private string calendarStateId = "calendar";
    [SerializeField] private string friendListStateId = "main";
    [SerializeField] private string friendViewStateId = "friend";
    [SerializeField] private string calendarDoorLiveStateId = "liveCalendar";
    [SerializeField] private string calendarConfigStateId = "calendarConfig";
    [SerializeField] private string calendarDoorConfigStateId = "calendarDoorConfig";
    [SerializeField] private string gameStateId = "game";
    [SerializeField] private string giftCenterStateId = "giftCenter";

    private readonly Dictionary<string, GameObject> _windowLookup = new();
    private string _currentStateId;

    private bool _networkSubscribed;
    private bool _calendarSubscribed;
    private bool _friendSubscribed;
    private bool _calendarConfigSubscribed;
    private bool _calendarDoorConfigSubscribed;
    private bool _calendarDoorLiveSubscribed;
    private bool _giftCenterBackSubscribed;
    private bool _playViewBackSubscribed;

    private void Awake()
    {
        if (string.IsNullOrEmpty(loginStateId))
            loginStateId = "login";

        BuildLookup();

        if (hideAllOnAwake)
            HideAllWindows();

        if (switchToInitialStateOnAwake)
            SwitchTo(loginStateId);
    }

    private void Start()
    {
        SubscribeToNetwork();
        SubscribeToCalendar();
        SubscribeToFriends();
        SubscribeToCalendarConfig();
        SubscribeToCalendarDoorConfig();
        SubscribeToCalendarDoorLiveView();
        SubscribeToGiftCenterBackButton();
        SubscribeToPlayViewBackButton();
    }

    private void OnDisable()
    {
        UnsubscribeFromNetwork();
        UnsubscribeFromCalendar();
        UnsubscribeFromFriends();
        UnsubscribeFromCalendarConfig();
        UnsubscribeFromCalendarDoorConfig();
        UnsubscribeFromCalendarDoorLiveView();
        UnsubscribeFromGiftCenterBackButton();
        UnsubscribeFromPlayViewBackButton();
    }

    public void SwitchTo(string stateId)
    {
        if (string.IsNullOrEmpty(stateId)) return;
        if (!_windowLookup.TryGetValue(stateId, out var target) || target == null) return;

        foreach (var kvp in _windowLookup)
        {
            if (kvp.Value == null) continue;
            kvp.Value.SetActive(kvp.Key == stateId);
        }

        _currentStateId = stateId;
        onStateChanged?.Invoke(_currentStateId);
    }

    public void HideAllWindows()
    {
        foreach (var kvp in _windowLookup)
            if (kvp.Value != null) kvp.Value.SetActive(false);
        _currentStateId = string.Empty;
    }

    private void BuildLookup()
    {
        _windowLookup.Clear();
        if (windows == null) return;

        foreach (var w in windows)
        {
            if (w == null || string.IsNullOrEmpty(w.Id)) continue;
            if (_windowLookup.ContainsKey(w.Id)) continue;
            _windowLookup.Add(w.Id, w.Window);
        }
    }

    private void SubscribeToNetwork()
    {
        if (_networkSubscribed) return;
        var net = NetworkStateMachine.Instance;
        if (!net) return;

        net.OnLoginSuccess += HandleLoginSuccess;
        net.OnLogout += HandleLogout;
        net.OnEventLive += HandleEventLive;
        _networkSubscribed = true;

        if (net.TryGetLastSnapshot(out var snapshot) && snapshot.HasStarted && !snapshot.HasEnded)
            HandleEventLive(snapshot);
    }

    private void UnsubscribeFromNetwork()
    {
        if (!_networkSubscribed) return;
        var net = NetworkStateMachine.Instance;
        if (!net)
        {
            _networkSubscribed = false;
            return;
        }

        net.OnLoginSuccess -= HandleLoginSuccess;
        net.OnLogout -= HandleLogout;
        net.OnEventLive -= HandleEventLive;
        _networkSubscribed = false;
    }

    private void SubscribeToCalendar()
    {
        if (_calendarSubscribed || calendarDoorSpawner == null) return;
        calendarDoorSpawner.DoorClicked += HandleDoorClicked;
        calendarDoorSpawner.OnBackClicked += HandleFriendBack;
        _calendarSubscribed = true;
    }

    private void UnsubscribeFromCalendar()
    {
        if (!_calendarSubscribed || calendarDoorSpawner == null) return;
        calendarDoorSpawner.DoorClicked -= HandleDoorClicked;
        calendarDoorSpawner.OnBackClicked -= HandleFriendBack;
        _calendarSubscribed = false;
    }

    private void SubscribeToFriends()
    {
        if (_friendSubscribed) return;
        FriendItemView.OnFriendSelectEvent += HandleFriendSelected;
        if (friendView != null)
        {
            friendView.OnBackPressed += HandleFriendBack;
            friendView.OnCalenderConfigClicked += HandleCalendarConfigClicked;
            friendView.OnCalendarPlayRequested += HandleCalendarPlayRequested;
        }

        _friendSubscribed = true;
    }

    private void UnsubscribeFromFriends()
    {
        if (!_friendSubscribed) return;
        FriendItemView.OnFriendSelectEvent -= HandleFriendSelected;
        if (friendView != null)
        {
            friendView.OnBackPressed -= HandleFriendBack;
            friendView.OnCalenderConfigClicked -= HandleCalendarConfigClicked;
            friendView.OnCalendarPlayRequested -= HandleCalendarPlayRequested;
        }

        _friendSubscribed = false;
    }

    private void SubscribeToCalendarConfig()
    {
        if (_calendarConfigSubscribed || calendarConfiguratorView == null) return;
        
        calendarConfiguratorView.OnCalendarLayoutSaved += HandleCalendarSaved;
        calendarConfiguratorView.OnCalendarDoorConfigClicked += HandleDoorConfigOpen;
        _calendarConfigSubscribed = true;
    }

    private void UnsubscribeFromCalendarConfig()
    {
        if (!_calendarConfigSubscribed || calendarConfiguratorView == null) return;
        calendarConfiguratorView.OnCalendarLayoutSaved -= HandleCalendarSaved;
        calendarConfiguratorView.OnCalendarDoorConfigClicked -= HandleDoorConfigOpen;
        _calendarConfigSubscribed = false;
    }

    private void SubscribeToCalendarDoorConfig()
    {
        if (_calendarDoorConfigSubscribed || calendarDoorEditorView == null) return;
        calendarDoorEditorView.OnConfigSaved += HandleDoorConfigSaved;
        calendarDoorEditorView.OnBackPressed += HandleDoorConfigBack;
        _calendarDoorConfigSubscribed = true;
    }

    private void UnsubscribeFromCalendarDoorConfig()
    {
        if (!_calendarDoorConfigSubscribed || calendarDoorEditorView == null) return;
        calendarDoorEditorView.OnConfigSaved -= HandleDoorConfigSaved;
        calendarDoorEditorView.OnBackPressed -= HandleDoorConfigBack;
        _calendarDoorConfigSubscribed = false;
    }

    private void SubscribeToCalendarDoorLiveView()
    {
        if (_calendarDoorLiveSubscribed || calendarDoorLiveView == null) return;

        calendarDoorLiveView.BackRequested += HandleDoorLiveBackRequested;
        calendarDoorLiveView.PlayRequested += HandleDoorLivePlayRequested;
        calendarDoorLiveView.GiftRequested += HandleDoorLiveGiftRequested;
        _calendarDoorLiveSubscribed = true;
    }

    private void UnsubscribeFromCalendarDoorLiveView()
    {
        if (!_calendarDoorLiveSubscribed || calendarDoorLiveView == null) return;

        calendarDoorLiveView.BackRequested -= HandleDoorLiveBackRequested;
        calendarDoorLiveView.PlayRequested -= HandleDoorLivePlayRequested;
        calendarDoorLiveView.GiftRequested -= HandleDoorLiveGiftRequested;
        _calendarDoorLiveSubscribed = false;
    }

    private void SubscribeToGiftCenterBackButton()
    {
        if (_giftCenterBackSubscribed || giftCenterView == null) return;

        giftCenterView.OnBackButtonClicked += HandleGiftCenterBack;
        _giftCenterBackSubscribed = true;
    }

    private void UnsubscribeFromGiftCenterBackButton()
    {
        if (!_giftCenterBackSubscribed || giftCenterView == null) return;

        giftCenterView.OnBackButtonClicked -= HandleGiftCenterBack;
        _giftCenterBackSubscribed = false;
    }

    private void SubscribeToPlayViewBackButton()
    {
        if (_playViewBackSubscribed || playModeView == null) return;

        playModeView.OnBackButtonClicked += HandlePlayViewBack;
        _playViewBackSubscribed = true;
    }

    private void UnsubscribeFromPlayViewBackButton()
    {
        if (!_playViewBackSubscribed || playModeView == null) return;

        playModeView.OnBackButtonClicked -= HandlePlayViewBack;
        _playViewBackSubscribed = false;
    }

    private void HandleLoginSuccess() => SwitchTo(mainStateId);
    private void HandleLogout() => SwitchTo(loginStateId);

    private void HandleEventLive(ServerClockSnapshot snapshot)
    {
        if (calendarDoorSpawner == null || calendarDoorSpawner.ActiveCalendar == null)
            return;

        if (!CalendarTimeUtility.IsEventLive(calendarDoorSpawner.ActiveCalendar, snapshot))
            return;

        SwitchTo(calendarStateId);
    }

    private void HandleDoorClicked(CalendarDescription calendar, CalendarDoorDescription door)
    {
        if (calendarDoorLiveView != null && calendarDoorSpawner != null)
        {
            calendarDoorLiveView.LoadDoor(calendar, door);
        }

        SwitchTo(calendarDoorLiveStateId);
    }

    private void HandleDoorLiveBackRequested()
    {
        SwitchTo(calendarStateId);
    }

    private void HandleDoorLivePlayRequested()
    {
        if (!string.IsNullOrEmpty(gameStateId))
            SwitchTo(gameStateId);
    }

    private void HandleDoorLiveGiftRequested()
    {
        if (string.IsNullOrEmpty(giftCenterStateId))
            return;

        SwitchTo(giftCenterStateId);
    }

    private void HandleFriendSelected(FriendProfileData data)
    {
        if (friendView == null) return;
        friendView.LoadView(data);
        SwitchTo(friendViewStateId);
    }

    private void HandleFriendBack() => SwitchTo(friendListStateId);

    private void HandleCalendarPlayRequested(CalendarDescription calendar)
    {
        if (calendarDoorSpawner != null)
            calendarDoorSpawner.SetActiveCalendar(calendar);

        SwitchTo(calendarStateId);
    }

    private void HandleCalendarConfigClicked()
    {
        Debug.Log($"[UIStateMachine] Calendar configurator opened.");
        SwitchTo(calendarConfigStateId);
    }

    private void HandleCalendarSaved(List<DoorConfigurationData> doors)
    {
        Debug.Log($"[UIStateMachine] Calendar saved ({doors.Count} doors). Upload pending.");
        SwitchTo(friendViewStateId);
    }

    private void HandleDoorConfigOpen(DoorConfigurationData door)
    {
        Debug.Log($"[UIStateMachine] Door #{door.Index} config opened.");
        SwitchTo(calendarDoorConfigStateId);
    }

    private void HandleDoorConfigSaved(DoorConfigurationData door)
    {
        Debug.Log($"[UIStateMachine] Door #{door.Index} config saved.");
        SwitchTo(calendarConfigStateId);
    }

    private void HandleDoorConfigBack() => SwitchTo(calendarConfigStateId);

    private void HandleGiftCenterBack() => SwitchTo(calendarDoorLiveStateId);

    private void HandlePlayViewBack() => SwitchTo(calendarDoorLiveStateId);
}
