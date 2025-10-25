using System;
using System.Threading.Tasks;
using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;

public sealed class NetworkStateMachine : MonoBehaviour
{
    public static NetworkStateMachine Instance { get; private set; }

    public NetworkState CurrentState { get; private set; } = NetworkState.Disconnected;

    public event Action<NetworkState> OnStateChanged;
    public event Action<string> OnError;
    public event Action OnLoginSuccess;
    public event Action OnLogout;
    public event Action<ServerClockSnapshot> OnServerTimeFetched;
    public event Action<ServerClockSnapshot> OnEventLive;

    private ServerClockService _serverClock;
    private ServerClockSnapshot? _lastSnapshot;
    [Header("Calendar Window")]
    [SerializeField] private string eventStartDate = "01.12";
    [SerializeField] private string eventEndDate = "24.12";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        _serverClock = new ServerClockService();
        _serverClock.ConfigureEventWindow(eventStartDate, eventEndDate);
    }

    public void ConfigureEventWindow(string startDate, string endDate)
    {
        _serverClock.ConfigureEventWindow(startDate, endDate);
    }

    public bool TryGetLastSnapshot(out ServerClockSnapshot snapshot)
    {
        if (_lastSnapshot.HasValue)
        {
            snapshot = _lastSnapshot.Value;
            return true;
        }

        snapshot = default;
        return false;
    }

    private void SetState(NetworkState newState)
    {
        if (CurrentState == newState) return;
        CurrentState = newState;
        Debug.Log($"[NetworkState] {newState}");
        OnStateChanged?.Invoke(newState);
    }

    // -------------------------------------------------------------
    // AUTH LOGIC
    // -------------------------------------------------------------
    public async Task LoginAsync(string username, string password)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            OnError?.Invoke("Username or password is empty.");
            return;
        }

        SetState(NetworkState.Connecting);

        var req = new LoginWithPlayFabRequest
        {
            Username = username,
            Password = password
        };

        var tcs = new TaskCompletionSource<bool>();

        PlayFabClientAPI.LoginWithPlayFab(req,
            result =>
            {
                Debug.Log("[Network] Login successful.");
                SetState(NetworkState.LoggedIn);
                OnLoginSuccess?.Invoke();
                tcs.SetResult(true);
            },
            error =>
            {
                Debug.LogError($"[Network] Login failed: {error.GenerateErrorReport()}");
                SetState(NetworkState.Error);
                OnError?.Invoke(error.ErrorMessage);
                tcs.SetResult(false);
            });

        await tcs.Task;
    }

    public async Task RegisterAsync(string username, string password)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            OnError?.Invoke("Username or password is empty.");
            return;
        }

        SetState(NetworkState.Connecting);

        var req = new RegisterPlayFabUserRequest
        {
            Username = username,
            Password = password,
            RequireBothUsernameAndEmail = false
        };

        var tcs = new TaskCompletionSource<bool>();

        PlayFabClientAPI.RegisterPlayFabUser(req,
            result =>
            {
                Debug.Log("[Network] Registration successful.");
                SetState(NetworkState.Disconnected); // Back to login screen
                tcs.SetResult(true);
            },
            error =>
            {
                Debug.LogError($"[Network] Register failed: {error.GenerateErrorReport()}");
                SetState(NetworkState.Error);
                OnError?.Invoke(error.ErrorMessage);
                tcs.SetResult(false);
            });

        await tcs.Task;
    }

    public void Logout()
    {
        PlayFabClientAPI.ForgetAllCredentials();
        SetState(NetworkState.Disconnected);
        OnLogout?.Invoke();
        Debug.Log("[Network] Logged out.");
    }

    // -------------------------------------------------------------
    // SERVER TIME
    // -------------------------------------------------------------
    public async Task FetchServerTimeAsync()
    {
        if (CurrentState != NetworkState.LoggedIn)
            return;

        SetState(NetworkState.FetchingServerTime);

        bool ok = await _serverClock.FetchServerTime();

        if (ok)
        {
            SetState(NetworkState.Ready);

            var snapshot = _serverClock.CreateSnapshot();
            _lastSnapshot = snapshot;
            OnServerTimeFetched?.Invoke(snapshot);

            if (snapshot.HasStarted && !snapshot.HasEnded)
            {
                OnEventLive?.Invoke(snapshot);
            }
        }
        else
        {
            SetState(NetworkState.Error);
            OnError?.Invoke("[StateMachine] Failed to fetch server time.");
        }
    }
}
