using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI element representing a single friend entry in the friend list.
/// When clicked, it fires an event that the state machine can listen to.
/// </summary>
public sealed class FriendItemView : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text friendNameText;
    [SerializeField] private Image avatarImage;
    [SerializeField] private Button selectButton;

    private FriendProfileData _data;

    /// <summary>
    /// Fired when this friend item is selected.
    /// </summary>
    public static event Action<FriendProfileData> OnFriendSelectEvent;

    private void Awake()
    {
        if (selectButton != null)
            selectButton.onClick.AddListener(OnFriendSelected);
    }

    /// <summary>
    /// Initializes the UI element with the provided friend data.
    /// </summary>
    public void Initialize(FriendProfileData data)
    {
        _data = data;

        if (friendNameText != null)
            friendNameText.text = data.DisplayName;

        if (avatarImage != null && data.AvatarSprite != null)
        {
            avatarImage.sprite = data.AvatarSprite;
        }
    }

    /// <summary>
    /// Called when this friend entry is clicked.
    /// Fires a global event to notify the state machine.
    /// </summary>
    private void OnFriendSelected()
    {
        if (_data == null)
        {
            Debug.LogWarning("[FriendItemView] No data assigned to this friend item.");
            return;
        }

        Debug.Log($"[FriendItemView] Friend selected: {_data.DisplayName}");
        OnFriendSelectEvent?.Invoke(_data);
    }

    private void OnDestroy()
    {
        if (selectButton != null)
            selectButton.onClick.RemoveListener(OnFriendSelected);
    }
}
