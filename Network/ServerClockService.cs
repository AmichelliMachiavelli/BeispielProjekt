using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

public sealed class ServerClockService
{
    private const string DateFormat = "dd.MM.yyyy";
    private readonly TimeZoneInfo _localZone;
    private DateTime _lastFetchAttempt;

    public ServerClockService()
    {
        _localZone = TimeZoneInfo.Local;
    }

    public string StartDateString { get; private set; } = "01.12";
    public string EndDateString { get; private set; } = "24.12";

    public DateTime LastServerUtc { get; private set; }
    public DateTime LastLocalTime { get; private set; }
    public DateTime StartLocal { get; private set; }
    public DateTime EndLocal { get; private set; }
    public int DaysUntilStart { get; private set; }
    public int EventLengthDays { get; private set; }

    /// <summary>
    /// Optionally throttle how often the service can call PlayFab (to avoid rate-limit errors)
    /// </summary>
    private const double MinFetchIntervalSeconds = 10.0;

    public void ConfigureEventWindow(string startDate, string endDate)
    {
        if (!string.IsNullOrWhiteSpace(startDate))
            StartDateString = startDate.Trim();

        if (!string.IsNullOrWhiteSpace(endDate))
            EndDateString = endDate.Trim();

        if (LastLocalTime != default)
            ComputeEventWindow();
    }

    /// <summary>
    /// Tries to fetch PlayFab server time, falls back to local system time if it fails.
    /// </summary>
    public async Task<bool> FetchServerTime()
    {
        // Throttle: avoid multiple requests in a short period
        if ((DateTime.UtcNow - _lastFetchAttempt).TotalSeconds < MinFetchIntervalSeconds)
        {
            Debug.LogWarning("[ServerClock] Throttled fetch request.");
            return false;
        }

        _lastFetchAttempt = DateTime.UtcNow;

        var tcs = new TaskCompletionSource<bool>();
        bool success = false;

        var req = new ExecuteCloudScriptRequest
        {
            FunctionName = "getServerTime"
        };

        PlayFabClientAPI.ExecuteCloudScript(req,
            result =>
            {
                try
                {
                    if (result.FunctionResult is IDictionary<string, object> dict &&
                        dict.ContainsKey("utc"))
                    {
                        string utc = dict["utc"].ToString();
                        LastServerUtc = DateTime.Parse(utc, null, DateTimeStyles.AdjustToUniversal);
                        LastLocalTime = TimeZoneInfo.ConvertTimeFromUtc(LastServerUtc, _localZone);

                        Debug.Log($"[ServerClock] Server time OK: {LastLocalTime:yyyy-MM-dd HH:mm:ss}");
                        success = true;
                    }
                    else
                    {
                        Debug.LogWarning("[ServerClock] Invalid CloudScript response.");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("[ServerClock] Parse error: " + e.Message);
                }
                finally
                {
                    ComputeEventWindow();
                    tcs.SetResult(success);
                }
            },
            error =>
            {
                Debug.LogError("[ServerClock] CloudScript error: " + error.GenerateErrorReport());
                // Fallback to device time
                UseDeviceFallback();
                tcs.SetResult(false);
            });

        // Wait for PlayFab response or fallback to timeout
        var task = await Task.WhenAny(tcs.Task, Task.Delay(5000)); // 5s timeout
        if (task != tcs.Task)
        {
            Debug.LogWarning("[ServerClock] Timeout, using local device time.");
            UseDeviceFallback();
            return false;
        }

        return await tcs.Task;
    }

    /// <summary>
    /// Builds a snapshot with current event info.
    /// </summary>
    public ServerClockSnapshot CreateSnapshot()
    {
        return new ServerClockSnapshot(LastLocalTime, StartLocal, EndLocal, DaysUntilStart, EventLengthDays);
    }

    private void UseDeviceFallback()
    {
        LastServerUtc = DateTime.UtcNow;
        LastLocalTime = DateTime.Now;
        ComputeEventWindow();

        Debug.Log($"[ServerClock] Fallback to device time: {LastLocalTime:yyyy-MM-dd HH:mm:ss}");
    }

    private void ComputeEventWindow()
    {
        if (LastLocalTime == default)
        {
            StartLocal = default;
            EndLocal = default;
            DaysUntilStart = 0;
            EventLengthDays = 0;
            return;
        }

        int year = LastLocalTime.Year;

        StartLocal = ParseDayMonthToLocal(StartDateString, year);
        EndLocal = ParseDayMonthToLocal(EndDateString, year);

        if (EndLocal < StartLocal)
            EndLocal = EndLocal.AddYears(1);

        DaysUntilStart = CalculateDaysUntil(StartLocal, LastLocalTime);
        EventLengthDays = CalculateEventLength(StartLocal, EndLocal);
    }

    private DateTime ParseDayMonthToLocal(string dayMonth, int year)
    {
        if (string.IsNullOrWhiteSpace(dayMonth))
            return new DateTime(year, 12, 1, 0, 0, 0, DateTimeKind.Local);

        if (DateTime.TryParseExact($"{dayMonth}.{year}", DateFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal, out DateTime result))
            return DateTime.SpecifyKind(result, DateTimeKind.Local);

        Debug.LogWarning($"[ServerClock] Invalid date format '{dayMonth}'. Expected dd.MM.");
        return new DateTime(year, 12, 1, 0, 0, 0, DateTimeKind.Local);
    }

    private static int CalculateDaysUntil(DateTime start, DateTime now)
    {
        if (start <= now)
            return 0;

        return (int)Math.Ceiling((start - now).TotalDays);
    }

    private static int CalculateEventLength(DateTime start, DateTime end)
    {
        if (end < start)
            return 0;

        return (int)(end.Date - start.Date).TotalDays + 1;
    }
}

public readonly struct ServerClockSnapshot
{
    public ServerClockSnapshot(DateTime localNow, DateTime startLocal, DateTime endLocal, int daysUntilStart, int eventLengthDays)
    {
        LocalNow = localNow;
        StartLocal = startLocal;
        EndLocal = endLocal;
        DaysUntilStart = daysUntilStart;
        EventLengthDays = eventLengthDays;

        if (localNow.Date < startLocal.Date)
            CurrentDay = 0;
        else if (localNow.Date > endLocal.Date)
            CurrentDay = eventLengthDays;
        else
            CurrentDay = (int)(localNow.Date - startLocal.Date).TotalDays + 1;
    }

    public DateTime LocalNow { get; }
    public DateTime StartLocal { get; }
    public DateTime EndLocal { get; }
    public int DaysUntilStart { get; }
    public int EventLengthDays { get; }
    public int CurrentDay { get; }

    public bool IsValid => LocalNow != default && StartLocal != default && EndLocal != default;
    public bool HasStarted => LocalNow >= StartLocal;
    public bool HasEnded => LocalNow > EndLocal;
}
