using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class FriendView : MonoBehaviour
{
    public static FriendProfileData CurrentSelectedFriend { get; internal set; }

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI friendNameText;
    [SerializeField] private Image avatarImage;
    [SerializeField] private Button playCalendarButton;
    [SerializeField] private Button configureCalendarButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Button deleteFriendButton;

    [Header("Runtime Data")]
    private FriendProfileData _currentFriend;
    private bool _isFetchingCalendar;

    /// <summary>
    /// Fired after a friend's calendar data has been successfully loaded.
    /// </summary>
    public event Action<FriendProfileData> OnFriendLoaded;
    public event Action<CalendarDescription> OnMeToFriendCalendarLoaded;
    public event Action<CalendarDescription> OnFriendToMeCalendarLoaded;
    public event Action OnBackPressed;
    public event Action OnCalenderConfigClicked;
    public event Action<CalendarDescription> OnCalendarPlayRequested;

    private void Awake()
    {
        // Disable Play button until data is available
        if (playCalendarButton != null)
            playCalendarButton.interactable = false;

        RegisterButtonEvents();
    }

    private void RegisterButtonEvents()
    {
        if (playCalendarButton != null)
            playCalendarButton.onClick.AddListener(OnPlayCalendarPressed);

        if (configureCalendarButton != null)
            configureCalendarButton.onClick.AddListener(OnConfigureCalendarPressed);

        if (backButton != null)
            backButton.onClick.AddListener(OnBackButtonPressed);

        if (deleteFriendButton != null)
            deleteFriendButton.onClick.AddListener(OnDeleteFriendPressed);
    }

    /// <summary>
    /// Loads and displays the selected friend's data in the view.
    /// </summary>
    public void LoadView(FriendProfileData selectedFriend)
    {
        _currentFriend = selectedFriend;
        CurrentSelectedFriend = _currentFriend;
        _currentFriend.Calendar = null;
        CurrentSelectedFriend.Calendar = null;

        friendNameText.text = selectedFriend.DisplayName;

        if (avatarImage != null && selectedFriend.AvatarSprite != null)
            avatarImage.sprite = selectedFriend.AvatarSprite;

        Debug.Log($"[FriendView] Loading view for friend: {selectedFriend.PlayFabId}");

        if (playCalendarButton != null)
            playCalendarButton.interactable = false;

        configureCalendarButton.interactable = false;

        NetworkCalendarManager.Instance.FetchFriendCalendar(
            selectedFriend.PlayFabId,
            (success, friendToMe, meToFriend) =>
            {
                if (success)
                {
                    bool bFriendToMe = friendToMe != null;
                    bool bMeToFriend = meToFriend != null;
                    Debug.Log("[FriendView] Calendar data loaded: " +
                              $"Friend→Me: {bFriendToMe}, Me→Friend: {bMeToFriend}");

                    if (playCalendarButton != null)
                        playCalendarButton.interactable = bFriendToMe;

                    if (bFriendToMe)
                    {
                        _currentFriend.Calendar = friendToMe;
                        CurrentSelectedFriend.Calendar = friendToMe;
                        OnFriendToMeCalendarLoaded?.Invoke(friendToMe);
                    }

                    if (bMeToFriend)
                        OnMeToFriendCalendarLoaded?.Invoke(meToFriend);
                }

                OnFriendLoaded?.Invoke(selectedFriend);

                configureCalendarButton.interactable = true;
            });
    }

    private async void OnPlayCalendarPressed()
    {
        if (_currentFriend == null)
        {
            Debug.LogWarning("[FriendView] Cannot play calendar, no friend selected.");
            return;
        }

        if (_isFetchingCalendar)
        {
            Debug.Log("[FriendView] Calendar play already in progress.");
            return;
        }

        var calendar = _currentFriend.Calendar;
        if (calendar == null)
        {
            Debug.LogWarning($"[FriendView] {_currentFriend.DisplayName} does not have a playable calendar.");
            return;
        }

        Debug.Log($"[FriendView] Playing friend's calendar: {_currentFriend.DisplayName}");

        var network = NetworkStateMachine.Instance;
        if (network == null)
        {
            Debug.LogError("[FriendView] NetworkStateMachine instance not available.");
            return;
        }

        ConfigureNetworkEventWindow(network, calendar);

        OnCalendarPlayRequested?.Invoke(calendar);

        _isFetchingCalendar = true;
        bool restoreInteractable = false;

        if (playCalendarButton != null)
        {
            restoreInteractable = playCalendarButton.interactable;
            playCalendarButton.interactable = false;
        }

        try
        {
            await network.FetchServerTimeAsync();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[FriendView] Failed to fetch server time: {ex.Message}");
        }
        finally
        {
            _isFetchingCalendar = false;

            if (playCalendarButton != null)
                playCalendarButton.interactable = restoreInteractable;
        }
    }

    private void ConfigureNetworkEventWindow(NetworkStateMachine network, CalendarDescription calendar)
    {
        if (calendar == null || network == null)
            return;

        string startString = "01.12";
        string endString = "24.12";

        bool startParsed = TryParseCalendarDate(calendar.StartDate, out DateTime startDate);
        bool endParsed = TryParseCalendarDate(calendar.EndDate, out DateTime endDate);

        if (startParsed)
        {
            startString = startDate.ToString("dd.MM", CultureInfo.InvariantCulture);
        }
        else
        {
            Debug.LogWarning("[FriendView] Unable to parse calendar start date, using default 01.12.");
        }

        if (endParsed)
        {
            endString = endDate.ToString("dd.MM", CultureInfo.InvariantCulture);
        }
        else if (startParsed && calendar.Doors != null && calendar.Doors.Length > 0)
        {
            endString = startDate.AddDays(calendar.Doors.Length - 1).ToString("dd.MM", CultureInfo.InvariantCulture);
            Debug.LogWarning("[FriendView] Calendar end date missing or invalid, derived from door count.");
        }
        else
        {
            Debug.LogWarning("[FriendView] Unable to parse calendar end date, using default 24.12.");
        }

        network.ConfigureEventWindow(startString, endString);
    }

    private static bool TryParseCalendarDate(string rawValue, out DateTime date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(rawValue))
            return false;

        if (DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out date))
            return true;

        if (DateTime.TryParse(rawValue, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out date))
            return true;

        return false;
    }

    private void OnConfigureCalendarPressed()
    {
        Debug.Log($"[FriendView] Configuring calendar for {_currentFriend.DisplayName}");
        OnCalenderConfigClicked?.Invoke();
    }

    public void OnBackButtonPressed()
    {
        Debug.Log("[FriendView] Returning to Friend List");
        OnBackPressed?.Invoke();
    }

    private void OnDeleteFriendPressed()
    {
        Debug.Log($"[FriendView] Delete friend: {_currentFriend.DisplayName}");
        NetworkFriendManager.Instance.RemoveFriend(_currentFriend.PlayFabId, (success) =>
        {
            if (success)
            {
                Debug.LogWarning("[FriendView] Delete friend successfully, returning to friend list...");
                OnBackPressed?.Invoke();
            }
            else
            {
                Debug.LogWarning("[FriendView] Could not delete friend.");
            }
        });
    }
}

/// <summary>
/// Example data container for friend info.
/// </summary>
[Serializable]
public sealed class FriendData
{
    public string PlayFabId;
    public string DisplayName;
    public Sprite AvatarSprite;
}
