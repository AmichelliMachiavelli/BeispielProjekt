using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CalendarDoorView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text dayLabel;
    [SerializeField] private Image icon;
    [SerializeField] private GameObject lockedStateRoot;
    [SerializeField] private GameObject unlockedStateRoot;

    private Action<CalendarDoorDescription> _clickCallback;
    private CalendarDoorDescription _doorData;
    private int _dayIndex;

    public int DayIndex => _dayIndex;

    private void Awake()
    {
        if (button != null)
        {
            button.onClick.AddListener(HandleButtonClicked);
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleButtonClicked);
        }
    }

    public void Initialize(int dayIndex, CalendarDoorDescription doorData, Action<CalendarDoorDescription> clickCallback, Sprite iconSprite = null, string overrideLabel = null, bool isUnlocked = true)
    {
        _dayIndex = Mathf.Max(1, dayIndex);
        _doorData = doorData ?? new CalendarDoorDescription(_dayIndex, string.Empty, DoorGameType.FLOPPY_BIRD);
        _clickCallback = clickCallback;

        if (dayLabel != null)
        {
            string displayText = string.IsNullOrEmpty(overrideLabel)
                ? _dayIndex.ToString("00")
                : overrideLabel;
            dayLabel.text = displayText;
        }

        if (icon != null)
        {
            icon.sprite = iconSprite;
            icon.enabled = iconSprite != null;
        }

        if (button != null)
        {
            button.interactable = isUnlocked;
        }

        UpdateStateVisuals(isUnlocked);
    }

    public void SetUnlocked(bool isUnlocked)
    {
        if (button != null)
        {
            button.interactable = isUnlocked;
        }

        UpdateStateVisuals(isUnlocked);
    }

    private void UpdateStateVisuals(bool isUnlocked)
    {
        if (lockedStateRoot != null)
        {
            lockedStateRoot.SetActive(!isUnlocked);
        }

        if (unlockedStateRoot != null)
        {
            unlockedStateRoot.SetActive(isUnlocked);
        }
    }

    private void HandleButtonClicked()
    {
        _clickCallback?.Invoke(_doorData ?? new CalendarDoorDescription(_dayIndex, string.Empty, DoorGameType.FLOPPY_BIRD));
    }
}
