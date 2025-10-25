using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class PlayMode : MonoBehaviour
{
    [SerializeField] Button backButton;

    public event Action OnBackButtonClicked;
    public static PlayMode Instance { get; private set; }

    public event Action<DoorGameType> GameStarted;
    public event Action<DoorGameType, bool> GameFinished;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[PlayMode] Duplicate instance detected. Destroying the newest instance.");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (backButton)
            backButton.onClick.AddListener(CancelAndSaveGame);
    }

    private void OnDisable()
    {
        if(backButton)
            backButton.onClick.RemoveListener(CancelAndSaveGame);
    }

    public void StartGame(DoorGameType gameType, Action<bool> onCompleted)
    {
        Debug.Log($"[PlayMode] Starting game {gameType}.");
        GameStarted?.Invoke(gameType);

        // TODO: Hook into the real gameplay flow.
        bool wasSuccessful = true;
        GameFinished?.Invoke(gameType, wasSuccessful);
        onCompleted?.Invoke(wasSuccessful);
    }

    public void CancelAndSaveGame()
    {
        OnBackButtonClicked?.Invoke();
    }
}
