using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Central utility for parsing and calculating calendar timing from server or local data.
/// </summary>
public static class CalendarTimeUtility
{
    private const string DateFormat = "dd.MM.yyyy";

    public static void GetCalendarProgress(
        CalendarDescription calendar,
        out int currentDay,
        out int totalDays)
    {
        currentDay = 0;
        totalDays = 0;

        if (calendar == null)
        {
            Debug.LogError("[CalendarTimeUtility] Calendar is null.");
            return;
        }

        if (NetworkStateMachine.Instance != null &&
            NetworkStateMachine.Instance.TryGetLastSnapshot(out var snapshot) &&
            snapshot.IsValid)
        {
            ComputeFromSnapshot(calendar, snapshot, out currentDay, out totalDays);
        }
        else
        {
            ComputeFromLocal(calendar, out currentDay, out totalDays);
        }
    }

    private static void ComputeFromSnapshot(
        CalendarDescription calendar,
        ServerClockSnapshot snapshot,
        out int currentDay,
        out int totalDays)
    {
        DateTime start = ParseDateOrFallback(calendar.StartDate, snapshot.StartLocal.Year);
        DateTime end = ParseDateOrFallback(calendar.EndDate, snapshot.EndLocal.Year);

        if (end < start)
            end = end.AddYears(1);

        totalDays = Mathf.Max(1, (int)(end - start).TotalDays + 1);

        int dayOffset = (int)(snapshot.LocalNow.Date - start.Date).TotalDays + 1;

        if (snapshot.LocalNow.Date > end.Date)
            currentDay = -1; // ended
        else if (snapshot.LocalNow.Date < start.Date)
            currentDay = 0; // not started yet
        else
            currentDay = Mathf.Clamp(dayOffset, 1, totalDays);

        Debug.Log($"[CalendarTimeUtility] ServerTime={snapshot.LocalNow:dd.MM.yyyy} " +
                  $"Start={start:dd.MM.yyyy} End={end:dd.MM.yyyy} Day={currentDay}/{totalDays}");
    }

    private static void ComputeFromLocal(
        CalendarDescription calendar,
        out int currentDay,
        out int totalDays)
    {
        DateTime now = DateTime.Now.Date;
        DateTime start = ParseDateOrFallback(calendar.StartDate, now.Year);
        DateTime end = ParseDateOrFallback(calendar.EndDate, now.Year);

        if (end < start)
            end = end.AddYears(1);

        totalDays = Mathf.Max(1, (int)(end - start).TotalDays + 1);

        int dayOffset = (int)(now - start).TotalDays + 1;

        if (now > end)
            currentDay = -1;
        else if (now < start)
            currentDay = 0;
        else
            currentDay = Mathf.Clamp(dayOffset, 1, totalDays);

        Debug.Log($"[CalendarTimeUtility] Local fallback: {currentDay}/{totalDays} days.");
    }

    private static DateTime ParseDateOrFallback(string dateStr, int year)
    {
        if (string.IsNullOrWhiteSpace(dateStr))
            return new DateTime(year, 12, 1, 0, 0, 0, DateTimeKind.Local);

        if (DateTime.TryParseExact(dateStr, DateFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal, out var fullDate))
            return DateTime.SpecifyKind(fullDate, DateTimeKind.Local);

        if (DateTime.TryParseExact($"{dateStr}.{year}", DateFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal, out var partialDate))
            return DateTime.SpecifyKind(partialDate, DateTimeKind.Local);

        Debug.LogWarning($"[CalendarTimeUtility] Invalid date format '{dateStr}', using 01.12.{year}");
        return new DateTime(year, 12, 1, 0, 0, 0, DateTimeKind.Local);
    }

    public static bool TryGetDoorDate(CalendarDescription calendar, int doorDay, out DateTime date)
    {
        date = DateTime.MinValue;

        if (calendar == null)
        {
            Debug.LogError("[CalendarTimeUtility] Cannot resolve door date without calendar data.");
            return false;
        }

        int referenceYear = DateTime.Now.Year;
        if (NetworkStateMachine.Instance != null &&
            NetworkStateMachine.Instance.TryGetLastSnapshot(out var snapshot) &&
            snapshot.IsValid)
        {
            referenceYear = snapshot.StartLocal.Year;
        }

        DateTime start = ParseDateOrFallback(calendar.StartDate, referenceYear);
        DateTime end = ParseDateOrFallback(calendar.EndDate, referenceYear);

        if (end < start)
            end = end.AddYears(1);

        int totalDays = Mathf.Max(1, (int)(end - start).TotalDays + 1);
        int clampedIndex = Mathf.Clamp(doorDay - 1, 0, totalDays - 1);
        date = start.AddDays(clampedIndex);
        return true;
    }

    /// <summary>
    /// Returns true if the event is currently live (between start and end date inclusive).
    /// </summary>
    public static bool IsEventLive(CalendarDescription calendar, ServerClockSnapshot? snapshot = null)
    {
        if (calendar == null)
            return false;

        return EvaluateCountdown(calendar, snapshot).IsLive;
    }

    /// <summary>
    /// Returns a countdown string to event start, or a message if it is live or ended.
    /// </summary>
    public static string GetCountdownString(CalendarDescription calendar)
    {
        CalendarCountdownState state = EvaluateCountdown(calendar);

        if (!state.HasCountdown)
            return string.IsNullOrEmpty(state.StatusMessage)
                ? string.Empty
                : state.StatusMessage;

        DateTime target = state.CountdownTargetLocal ?? state.CurrentLocalTime;
        TimeSpan remaining = target - state.CurrentLocalTime;
        string countdown = (state.CountdownPrefix ?? string.Empty) + FormatCountdown(remaining);

        if (string.IsNullOrEmpty(state.StatusMessage))
            return countdown;

        return string.IsNullOrEmpty(countdown)
            ? state.StatusMessage
            : $"{state.StatusMessage}\n{countdown}".Trim();
    }

    public static CalendarCountdownState EvaluateCountdown(ServerClockSnapshot snapshot)
    {
        if (!snapshot.IsValid)
        {
            DateTime now = DateTime.Now;
            return new CalendarCountdownState(
                now,
                "Awaiting event schedule...",
                false,
                null,
                string.Empty,
                false,
                snapshot.CurrentDay,
                Mathf.Max(0, snapshot.EventLengthDays),
                Mathf.Max(0, snapshot.EventLengthDays),
                snapshot.HasStarted,
                snapshot.HasEnded);
        }

        int eventLength = Mathf.Max(1, snapshot.EventLengthDays);
        return EvaluateCountdown(snapshot.LocalNow, snapshot.StartLocal, snapshot.EndLocal, eventLength);
    }

    public static CalendarCountdownState EvaluateCountdown(CalendarDescription calendar, ServerClockSnapshot? snapshot = null)
    {
        if (calendar == null)
        {
            DateTime nowFallback = snapshot?.LocalNow ?? DateTime.Now;
            return new CalendarCountdownState(
                nowFallback,
                "No calendar configured.",
                false,
                null,
                string.Empty,
                false,
                0,
                0,
                0,
                false,
                false);
        }

        DateTime currentTime;
        int referenceYear;

        if (snapshot.HasValue && snapshot.Value.IsValid)
        {
            currentTime = snapshot.Value.LocalNow;
            referenceYear = snapshot.Value.StartLocal.Year;
        }
        else
        {
            currentTime = DateTime.Now;
            referenceYear = DateTime.Now.Year;
        }

        DateTime start = ParseDateOrFallback(calendar.StartDate, referenceYear);
        DateTime end = ParseDateOrFallback(calendar.EndDate, referenceYear);

        if (end < start)
            end = end.AddYears(1);

        int eventLength = Mathf.Max(1, (int)(end.Date - start.Date).TotalDays + 1);
        return EvaluateCountdown(currentTime, start, end, eventLength);
    }

    public static CalendarCountdownState EvaluateCountdown(
        DateTime currentLocalTime,
        DateTime startLocal,
        DateTime endLocal,
        int eventLengthDays)
    {
        if (endLocal < startLocal)
            endLocal = endLocal.AddYears(1);

        int totalDays = eventLengthDays > 0
            ? Mathf.Max(1, eventLengthDays)
            : Mathf.Max(1, (int)(endLocal.Date - startLocal.Date).TotalDays + 1);

        bool hasStarted = currentLocalTime >= startLocal;
        bool hasEnded = currentLocalTime > endLocal;

        if (!hasStarted)
        {
            int daysRemaining = CalculateDaysRemaining(currentLocalTime, startLocal);
            string status = BuildStartStatus(daysRemaining, totalDays);
            return new CalendarCountdownState(
                currentLocalTime,
                status,
                true,
                startLocal,
                "Time until start: ",
                true,
                0,
                totalDays,
                totalDays,
                false,
                false);
        }

        if (!hasEnded)
        {
            int currentDay = Mathf.Clamp((currentLocalTime.Date - startLocal.Date).Days + 1, 1, totalDays);
            string status = $"Day {currentDay} of {totalDays}.";
            return new CalendarCountdownState(
                currentLocalTime,
                status,
                false,
                null,
                string.Empty,
                false,
                currentDay,
                totalDays,
                totalDays,
                true,
                false);
        }

        DateTime christmas = new DateTime(currentLocalTime.Year, 12, 25, 0, 0, 0, DateTimeKind.Local);
        if (currentLocalTime < christmas)
        {
            return new CalendarCountdownState(
                currentLocalTime,
                "Countdown until Christmas:",
                true,
                christmas,
                "Time remaining: ",
                false,
                totalDays,
                totalDays,
                totalDays,
                true,
                true);
        }

        return new CalendarCountdownState(
            currentLocalTime,
            "Merry Christmas!",
            false,
            null,
            string.Empty,
            false,
            totalDays,
            totalDays,
            totalDays,
            true,
            true);
    }

    public static string FormatCountdown(TimeSpan span)
    {
        if (span.TotalSeconds < 0)
            span = TimeSpan.Zero;

        return $"{span.Days:D2}d {span.Hours:D2}h {span.Minutes:D2}m {span.Seconds:D2}s";
    }

    public static int CalculateDaysRemaining(DateTime currentTime, DateTime targetTime)
    {
        if (targetTime <= currentTime)
            return 0;

        return (int)Math.Ceiling((targetTime - currentTime).TotalDays);
    }

    public static string BuildStartStatus(int daysUntilStart, int eventLengthDays)
    {
        if (daysUntilStart <= 0)
            return eventLengthDays > 0
                ? $"Event duration: {eventLengthDays} days."
                : "Event starting.";

        string dayLabel = daysUntilStart == 1 ? "1 day" : $"{daysUntilStart} days";
        if (eventLengthDays > 0)
            return $"{dayLabel} until the event begins. Event duration: {eventLengthDays} days.";

        return $"{dayLabel} until the event begins.";
    }

    /// <summary>
    /// Returns the current date (server or local) and the reference year used for parsing.
    /// </summary>
    private static DateTime GetReferenceNow(out int referenceYear)
    {
        if (NetworkStateMachine.Instance != null &&
            NetworkStateMachine.Instance.TryGetLastSnapshot(out var snapshot) &&
            snapshot.IsValid)
        {
            referenceYear = snapshot.StartLocal.Year;
            return snapshot.LocalNow.Date;
        }

        referenceYear = DateTime.Now.Year;
        return DateTime.Now.Date;
    }
}

public readonly struct CalendarCountdownState
{
    public CalendarCountdownState(
        DateTime currentLocalTime,
        string statusMessage,
        bool showCountdown,
        DateTime? countdownTargetLocal,
        string countdownPrefix,
        bool updateStatusWithDays,
        int currentDay,
        int totalDays,
        int eventLengthDays,
        bool hasStarted,
        bool hasEnded)
    {
        CurrentLocalTime = currentLocalTime;
        StatusMessage = statusMessage ?? string.Empty;
        ShowCountdown = showCountdown && countdownTargetLocal.HasValue;
        CountdownTargetLocal = countdownTargetLocal;
        CountdownPrefix = countdownPrefix ?? string.Empty;
        UpdateStatusWithDays = updateStatusWithDays;
        CurrentDay = Mathf.Max(0, currentDay);
        TotalDays = Mathf.Max(0, totalDays);
        EventLengthDays = Mathf.Max(0, eventLengthDays);
        HasStarted = hasStarted;
        HasEnded = hasEnded;
    }

    public DateTime CurrentLocalTime { get; }
    public string StatusMessage { get; }
    public bool ShowCountdown { get; }
    public DateTime? CountdownTargetLocal { get; }
    public string CountdownPrefix { get; }
    public bool UpdateStatusWithDays { get; }
    public int CurrentDay { get; }
    public int TotalDays { get; }
    public int EventLengthDays { get; }
    public bool HasStarted { get; }
    public bool HasEnded { get; }
    public bool IsLive => HasStarted && !HasEnded;
    public bool HasCountdown => ShowCountdown && CountdownTargetLocal.HasValue;
}
