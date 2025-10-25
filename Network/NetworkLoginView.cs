using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;

public sealed class NetworkLoginView : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField usernameField;
    [SerializeField] private TMP_InputField passwordField;
    [SerializeField] private Button loginButton;
    [SerializeField] private Button registerButton;
    [SerializeField] private Toggle rememberToggle;
    [SerializeField] private TextMeshProUGUI statusText;

    private NetworkStateMachine net;

    // PlayerPrefs keys
    private const string KeyRemember = "RememberCredentials";
    private const string KeyUsername = "SavedUsername";
    private const string KeyPassword = "SavedPassword";

    private void Awake()
    {
        loginButton.onClick.AddListener(OnLoginClicked);
        registerButton.onClick.AddListener(OnRegisterClicked);

        LoadSavedCredentials();
    }

    private void OnEnable()
    {
    }

    private void OnDisable()
    {
        net.OnStateChanged -= HandleStateChanged;
        net.OnError -= HandleError;
    }

    private async void Start()
    {
        net = NetworkStateMachine.Instance;
        net.OnStateChanged += HandleStateChanged;
        net.OnError += HandleError;

        // Auto login if "Remember Me" was enabled
        if (PlayerPrefs.GetInt(KeyRemember, 0) == 1)
        {
            string savedUser = PlayerPrefs.GetString(KeyUsername, string.Empty);
            string savedPass = PlayerPrefs.GetString(KeyPassword, string.Empty);

            if (!string.IsNullOrEmpty(savedUser) && !string.IsNullOrEmpty(savedPass))
            {
                SetStatus("Auto logging in...");
                await net.LoginAsync(savedUser, savedPass);
                await net.FetchServerTimeAsync();
            }
        }
    }

    // -------------------------------------------------------------
    // BUTTON HANDLERS
    // -------------------------------------------------------------
    private async void OnLoginClicked()
    {
        string user = usernameField.text.Trim();
        string pass = passwordField.text.Trim();

        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
        {
            SetStatus("Please enter username and password.");
            return;
        }

        SetStatus("Logging in...");
        await net.LoginAsync(user, pass);
        await net.FetchServerTimeAsync();

        if (rememberToggle.isOn)
            SaveCredentials(user, pass);
        else
            ClearCredentials();
    }

    private async void OnRegisterClicked()
    {
        string user = usernameField.text.Trim();
        string pass = passwordField.text.Trim();

        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
        {
            SetStatus("Please enter username and password.");
            return;
        }

        SetStatus("Registering...");
        await net.RegisterAsync(user, pass);
    }

    // -------------------------------------------------------------
    // STATE EVENTS
    // -------------------------------------------------------------
    private void HandleStateChanged(NetworkState state)
    {
        switch (state)
        {
            case NetworkState.Connecting: SetStatus("Connecting..."); break;
            case NetworkState.LoggedIn: SetStatus("Login successful."); break;
            case NetworkState.FetchingServerTime: SetStatus("Syncing server time..."); break;
            case NetworkState.Ready: SetStatus("Connected and ready."); break;
            case NetworkState.Error: SetStatus("An error occurred."); break;
            case NetworkState.Disconnected: SetStatus("Logged out."); break;
        }
    }

    private void HandleError(string message) => SetStatus($"Error: {message}");

    // -------------------------------------------------------------
    // CREDENTIAL HANDLING
    // -------------------------------------------------------------
    private void SaveCredentials(string username, string password)
    {
        PlayerPrefs.SetInt(KeyRemember, 1);
        PlayerPrefs.SetString(KeyUsername, username);
        PlayerPrefs.SetString(KeyPassword, password);
        PlayerPrefs.Save();
        Debug.Log("[NetworkLoginUI] Credentials saved.");
    }

    private void ClearCredentials()
    {
        PlayerPrefs.DeleteKey(KeyRemember);
        PlayerPrefs.DeleteKey(KeyUsername);
        PlayerPrefs.DeleteKey(KeyPassword);
        Debug.Log("[NetworkLoginUI] Credentials cleared.");
    }

    private void LoadSavedCredentials()
    {
        bool remember = PlayerPrefs.GetInt(KeyRemember, 0) == 1;
        rememberToggle.isOn = remember;

        if (remember)
        {
            usernameField.text = PlayerPrefs.GetString(KeyUsername, string.Empty);
            passwordField.text = PlayerPrefs.GetString(KeyPassword, string.Empty);
        }
    }

    // -------------------------------------------------------------
    // UI UTILS
    // -------------------------------------------------------------
    private void SetStatus(string msg)
    {
        if (statusText != null)
            statusText.text = msg;
    }
}
