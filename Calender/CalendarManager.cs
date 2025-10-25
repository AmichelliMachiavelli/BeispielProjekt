using System;
using System.Collections;
using TMPro;
using UnityEngine;

public sealed class CalendarManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text countdownText;

    public static CalendarManager Instance { get; private set; }

    private CalendarDescription _defaultCalendar;
    private ServerClockSnapshot? _lastSnapshot;
    private Coroutine _countdownRoutine;
    private string _currentStatusMessage = string.Empty;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[CalendarManager] Duplicate instance detected. Destroying the newest instance.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        SetCountdownVisible(false);
    }

    private void Start()
    {
        var net = NetworkStateMachine.Instance;
        if (net == null)
        {
            Debug.LogError("[CalendarManager] NetworkStateMachine instance not found.");
            return;
        }

        net.OnServerTimeFetched += HandleServerTimeFetched;

        if (net.TryGetLastSnapshot(out var snapshot) && snapshot.IsValid)
            HandleServerTimeFetched(snapshot);
    }

    private void OnDisable()
    {
        var net = NetworkStateMachine.Instance;
        if (net != null)
            net.OnServerTimeFetched -= HandleServerTimeFetched;

        StopCountdown();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SetDefaultCalendar(CalendarDescription calendar)
    {
        _defaultCalendar = calendar;

        if (_defaultCalendar == null)
        {
            if (_lastSnapshot.HasValue && _lastSnapshot.Value.IsValid)
                ApplyCountdownState(CalendarTimeUtility.EvaluateCountdown(_lastSnapshot.Value));
            return;
        }

        if (_lastSnapshot.HasValue && _lastSnapshot.Value.IsValid)
        {
            ApplyCountdownState(CalendarTimeUtility.EvaluateCountdown(_defaultCalendar, _lastSnapshot.Value));
        }
        else
        {
            ApplyCountdownState(CalendarTimeUtility.EvaluateCountdown(_defaultCalendar));
        }
    }

    private void HandleServerTimeFetched(ServerClockSnapshot snapshot)
    {
        Debug.Log("[CalendarManager] Server time fetched.");

        if (!snapshot.IsValid)
        {
            Debug.LogWarning("[CalendarManager] Received invalid server time snapshot.");
            return;
        }

        _lastSnapshot = snapshot;

        CalendarCountdownState state = _defaultCalendar != null
            ? CalendarTimeUtility.EvaluateCountdown(_defaultCalendar, snapshot)
            : CalendarTimeUtility.EvaluateCountdown(snapshot);

        ApplyCountdownState(state);
    }

    private void ApplyCountdownState(CalendarCountdownState state)
    {
        StopCountdown();

        SetStatus(state.StatusMessage);

        bool hasCountdown = state.HasCountdown;
        string countdownValue = hasCountdown ? BuildCountdownValue(state, state.CurrentLocalTime) : string.Empty;

        if (countdownText != null)
        {
            if (hasCountdown)
            {
                if (statusText == null && !string.IsNullOrEmpty(_currentStatusMessage))
                    countdownText.text = $"{_currentStatusMessage}\n{countdownValue}".Trim();
                else
                    countdownText.text = countdownValue;
            }
            else
            {
                countdownText.text = statusText == null ? _currentStatusMessage : string.Empty;
            }

            bool shouldBeVisible = hasCountdown || (statusText == null && !string.IsNullOrEmpty(countdownText.text));
            SetCountdownVisible(shouldBeVisible);
        }

        if (hasCountdown)
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
            if (countdownText != null)
            {
                if (statusText == null && !string.IsNullOrEmpty(_currentStatusMessage))
                    countdownText.text = $"{_currentStatusMessage}\n{countdownValue}".Trim();
                else
                    countdownText.text = countdownValue;
            }

            if (state.UpdateStatusWithDays)
            {
                int daysRemaining = CalendarTimeUtility.CalculateDaysRemaining(currentTime, target);
                if (daysRemaining != previousDayValue)
                {
                    string status = CalendarTimeUtility.BuildStartStatus(daysRemaining, state.EventLengthDays);
                    SetStatus(status);
                    previousDayValue = daysRemaining;

                    if (statusText == null && countdownText != null && state.HasCountdown)
                        countdownText.text = $"{_currentStatusMessage}\n{countdownValue}".Trim();
                }
            }

            yield return new WaitForSeconds(1f);
            currentTime = currentTime.AddSeconds(1);
        }
    }

    private string BuildCountdownValue(CalendarCountdownState state, DateTime currentTime)
    {
        if (!state.HasCountdown || !state.CountdownTargetLocal.HasValue)
            return string.Empty;

        TimeSpan remaining = state.CountdownTargetLocal.Value - currentTime;
        return (state.CountdownPrefix ?? string.Empty) + CalendarTimeUtility.FormatCountdown(remaining);
    }

    private void RequestServerTimeRefresh()
    {
        var net = NetworkStateMachine.Instance;
        if (net == null)
            return;

        _ = net.FetchServerTimeAsync();
    }

    private void SetStatus(string message)
    {
        _currentStatusMessage = message ?? string.Empty;

        if (statusText != null)
        {
            statusText.text = _currentStatusMessage;
            statusText.gameObject.SetActive(!string.IsNullOrEmpty(_currentStatusMessage));
        }
    }

    private void SetCountdownVisible(bool visible)
    {
        if (countdownText != null)
            countdownText.gameObject.SetActive(visible);
    }

    private void StopCountdown()
    {
        if (_countdownRoutine != null)
        {
            StopCoroutine(_countdownRoutine);
            _countdownRoutine = null;
        }
    }
}
