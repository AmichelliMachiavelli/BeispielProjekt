using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class GiftCenter : MonoBehaviour
{
    [SerializeField] Button backButton;
    [SerializeField] private bool supportsReroll = true;


    public event Action OnBackButtonClicked;
    public static GiftCenter Instance { get; private set; }

    public bool SupportsReroll => supportsReroll;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[GiftCenter] Duplicate instance detected. Destroying the newest instance.");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (backButton)
            backButton.onClick.AddListener(CloseGiftCenter);
    }

private void OnDisable()
{
    if (backButton)
        backButton.onClick.RemoveListener(CloseGiftCenter);
}

public void ClaimReward(CalendarDescription calendar, CalendarDoorDescription door, DoorSaveData saveData)
    {
        Debug.Log($"[GiftCenter] Claim reward for calendar '{calendar?.CalendarID}' door {door?.DoorDay}.");
        // TODO: Implement reward distribution logic.
    }

    public void RerollReward(CalendarDescription calendar, CalendarDoorDescription door, DoorSaveData saveData)
    {
        Debug.Log($"[GiftCenter] Reroll reward for calendar '{calendar?.CalendarID}' door {door?.DoorDay}.");
        // TODO: Implement reroll logic.
    }


    public void CloseGiftCenter()
    {
        OnBackButtonClicked?.Invoke();
    }
}
