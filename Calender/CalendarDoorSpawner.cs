using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CalendarDoorSpawner : MonoBehaviour
{
    [SerializeField] private Button backButton;
    [SerializeField] private ScrollRect scrollView;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private GameObject doorPrefab;
    [SerializeField] private TextMeshProUGUI countdownLabel;
    [SerializeField] private TMP_Text statusLabel;

    private readonly List<CalendarDoorView> _spawnedDoors = new List<CalendarDoorView>();
    private readonly Dictionary<int, CalendarDoorDescription> _doorLookup = new Dictionary<int, CalendarDoorDescription>();

    public event Action<CalendarDescription, CalendarDoorDescription> DoorClicked;
    public event Action OnBackClicked;

    private int _cachedCurrentDay = -1;
    private bool _isLive;
    private bool _networkSubscribed;
    private Coroutine _countdownRoutine;
    private ServerClockSnapshot? _lastSnapshot;
    private string _currentStatusMessage = string.Empty;

    private CalendarDescription _activeCalendar;
    public CalendarDescription ActiveCalendar => _activeCalendar;

    private void Start()
    {
        if (backButton != null)
            backButton.onClick.AddListener(HandleBackButtonClicked);

        SubscribeToNetwork();

        if (_activeCalendar != null)
            InitCalendar();
        else
            ClearCountdownText();
    }

    private void OnDisable()
    {
        if (backButton != null)
            backButton.onClick.RemoveListener(HandleBackButtonClicked);

        UnsubscribeFromNetwork();
        StopCountdown();
    }

    public void SetActiveCalendar(CalendarDescription calendar)
    {
        _activeCalendar = calendar;
        _cachedCurrentDay = -1;

        CalendarManager.Instance?.SetDefaultCalendar(calendar);
        InitCalendar();
    }

    public IReadOnlyList<CalendarDoorView> GetSpawnedDoors() => _spawnedDoors;

    private void HandleBackButtonClicked()
    {
        OnBackClicked?.Invoke();
    }

    private void SubscribeToNetwork()
    {
        if (_networkSubscribed)
            return;

        var net = NetworkStateMachine.Instance;
        if (net == null)
            return;

        net.OnServerTimeFetched += HandleServerTimeFetched;
        _networkSubscribed = true;

        if (net.TryGetLastSnapshot(out var snapshot) && snapshot.IsValid)
            HandleServerTimeFetched(snapshot);
    }

    private void UnsubscribeFromNetwork()
    {
        if (!_networkSubscribed)
            return;

        var net = NetworkStateMachine.Instance;
        if (net != null)
            net.OnServerTimeFetched -= HandleServerTimeFetched;

        _networkSubscribed = false;
    }

    private void HandleServerTimeFetched(ServerClockSnapshot snapshot)
    {
        _lastSnapshot = snapshot;

        if (_activeCalendar == null)
            return;

        CalendarCountdownState state = CalendarTimeUtility.EvaluateCountdown(_activeCalendar, snapshot);
        ApplyCountdownState(state);
    }

    private void InitCalendar()
    {
        StopCountdown();
        ClearSpawnedDoors();
        _doorLookup.Clear();

        if (_activeCalendar == null)
        {
            _isLive = false;
            ClearCountdownText();
            UpdateStatus(string.Empty);
            return;
        }

        CalendarCountdownState state;
        if (_lastSnapshot.HasValue && _lastSnapshot.Value.IsValid)
            state = CalendarTimeUtility.EvaluateCountdown(_activeCalendar, _lastSnapshot.Value);
        else
            state = CalendarTimeUtility.EvaluateCountdown(_activeCalendar);

        ApplyCountdownState(state);
    }

    private void ApplyCountdownState(CalendarCountdownState state)
    {
        bool wasLive = _isLive;
        _isLive = state.IsLive;

        UpdateStatus(state.StatusMessage);

        if (state.HasCountdown)
        {
            UpdateCountdownText(state, state.CurrentLocalTime);
            StartCountdown(state);
        }
        else
        {
            StopCountdown();
            ClearCountdownText();
        }

        if (_isLive && (!wasLive || _spawnedDoors.Count == 0))
            SpawnDoors();
    }

    private void UpdateStatus(string message)
    {
        _currentStatusMessage = message ?? string.Empty;

        if (statusLabel != null)
        {
            statusLabel.text = _currentStatusMessage;
            statusLabel.gameObject.SetActive(!string.IsNullOrEmpty(_currentStatusMessage));
        }
    }

    private void UpdateCountdownText(CalendarCountdownState state, DateTime currentTime)
    {
        if (countdownLabel == null)
            return;

        if (!state.HasCountdown || !state.CountdownTargetLocal.HasValue)
        {
            ClearCountdownText();
            return;
        }

        DateTime target = state.CountdownTargetLocal.Value;
        TimeSpan remaining = target - currentTime;
        string countdownValue = (state.CountdownPrefix ?? string.Empty) + CalendarTimeUtility.FormatCountdown(remaining);

        if (statusLabel == null && !string.IsNullOrEmpty(_currentStatusMessage))
            countdownLabel.text = $"{_currentStatusMessage}\n{countdownValue}".Trim();
        else
            countdownLabel.text = countdownValue;

        countdownLabel.gameObject.SetActive(!string.IsNullOrEmpty(countdownLabel.text));
    }

    private void ClearCountdownText()
    {
        if (countdownLabel == null)
            return;

        if (statusLabel == null)
        {
            countdownLabel.text = _currentStatusMessage;
            countdownLabel.gameObject.SetActive(!string.IsNullOrEmpty(countdownLabel.text));
        }
        else
        {
            countdownLabel.text = string.Empty;
            countdownLabel.gameObject.SetActive(false);
        }
    }

    private void StartCountdown(CalendarCountdownState state)
    {
        StopCountdown();

        if (!state.HasCountdown || !state.CountdownTargetLocal.HasValue)
            return;

        _countdownRoutine = StartCoroutine(UpdateCountdownCoroutine(state));
    }

    private IEnumerator UpdateCountdownCoroutine(CalendarCountdownState state)
    {
        if (!state.HasCountdown || !state.CountdownTargetLocal.HasValue)
        {
            _countdownRoutine = null;
            yield break;
        }

        DateTime currentTime = state.CurrentLocalTime;
        DateTime target = state.CountdownTargetLocal.Value;
        string prefix = state.CountdownPrefix ?? string.Empty;
        int previousDayValue = -1;

        while (true)
        {
            TimeSpan remaining = target - currentTime;
            if (remaining.TotalSeconds <= 0)
            {
                RequestServerTimeRefresh();
                _countdownRoutine = null;
                yield break;
            }

            string countdownValue = prefix + CalendarTimeUtility.FormatCountdown(remaining);
            if (countdownLabel != null)
            {
                if (statusLabel == null && !string.IsNullOrEmpty(_currentStatusMessage))
                    countdownLabel.text = $"{_currentStatusMessage}\n{countdownValue}".Trim();
                else
                    countdownLabel.text = countdownValue;

                countdownLabel.gameObject.SetActive(!string.IsNullOrEmpty(countdownLabel.text));
            }

            if (state.UpdateStatusWithDays)
            {
                int daysRemaining = CalendarTimeUtility.CalculateDaysRemaining(currentTime, target);
                if (daysRemaining != previousDayValue)
                {
                    string status = CalendarTimeUtility.BuildStartStatus(daysRemaining, state.EventLengthDays);
                    UpdateStatus(status);
                    previousDayValue = daysRemaining;

                    if (countdownLabel != null && state.HasCountdown && statusLabel == null)
                        countdownLabel.text = $"{_currentStatusMessage}\n{countdownValue}".Trim();
                }
            }

            yield return new WaitForSeconds(1f);
            currentTime = currentTime.AddSeconds(1);
        }
    }

    private void StopCountdown()
    {
        if (_countdownRoutine == null)
            return;

        StopCoroutine(_countdownRoutine);
        _countdownRoutine = null;
    }

    private void RequestServerTimeRefresh()
    {
        var net = NetworkStateMachine.Instance;
        if (net == null)
            return;

        _ = net.FetchServerTimeAsync();
    }

    public void SpawnDoors()
    {
        if (!_isLive)
            return;

        if (doorPrefab == null || contentRoot == null || _activeCalendar == null)
        {
            Debug.LogWarning("[CalendarDoorSpawner] Missing prefab, active calendar or content root assignment.");
            return;
        }

        CalendarTimeUtility.GetCalendarProgress(_activeCalendar, out _cachedCurrentDay, out int totalDays);

        if (scrollView != null)
        {
            scrollView.DOKill();
            scrollView.DOVerticalNormalizedPos(1, .3f, true);
        }

        CalendarDoorDescription[] doorData = _activeCalendar?.Doors;

        for (int i = 0; i < totalDays; i++)
        {
            CalendarDoorView instance = Instantiate(doorPrefab, contentRoot).GetComponent<CalendarDoorView>();
            int doorNumber = i + 1;

            CalendarDoorDescription description = ResolveDoorDescription(doorData, doorNumber);
            _doorLookup[doorNumber] = description;

            instance.Initialize(doorNumber, description, HandleDoorClicked, null, null, (doorNumber <= _cachedCurrentDay) && (_cachedCurrentDay != -1));
            _spawnedDoors.Add(instance);
        }
    }

    private void HandleDoorClicked(CalendarDoorDescription door)
    {
        if (door == null)
        {
            Debug.LogWarning("[CalendarDoorSpawner] Received null door data in HandleDoorClicked.");
            return;
        }

        if (!_doorLookup.TryGetValue(door.DoorDay, out var resolvedDoor) || resolvedDoor == null)
        {
            Debug.LogWarning($"[CalendarDoorSpawner] No cached data for door {door.DoorDay}, forwarding given instance.");
            resolvedDoor = door;
        }

        Debug.Log($"Clicked on door: {resolvedDoor.DoorDay}");
        DoorClicked?.Invoke(_activeCalendar, resolvedDoor);
    }

    private CalendarDoorDescription ResolveDoorDescription(CalendarDoorDescription[] doorData, int doorNumber)
    {
        if (doorData != null)
        {
            for (int j = 0; j < doorData.Length; j++)
            {
                CalendarDoorDescription entry = doorData[j];
                if (entry != null && entry.DoorDay == doorNumber)
                {
                    return entry;
                }
            }
        }

        return new CalendarDoorDescription(doorNumber, string.Empty, DoorGameType.FLOPPY_BIRD);
    }

    private void ClearSpawnedDoors()
    {
        for (int i = 0; i < _spawnedDoors.Count; i++)
        {
            CalendarDoorView door = _spawnedDoors[i];
            if (door != null)
            {
                Destroy(door.gameObject);
            }
        }

        _spawnedDoors.Clear();

        if (contentRoot == null)
        {
            return;
        }

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = contentRoot.GetChild(i);
            if (child != null)
            {
                Destroy(child.gameObject);
            }
        }
    }
}
