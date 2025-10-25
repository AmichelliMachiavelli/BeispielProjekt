using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

public sealed class FriendManagerView : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField playFabIdInput;
    [SerializeField] private Button addFriendButton;
    [SerializeField] private RectTransform friendListContainer;
    [SerializeField] private GameObject friendListItemPrefab;

    // Event triggered when the Add Friend button is clicked
    public event Action<string> OnAddFriend;

    private void Awake()
    {
        if (addFriendButton != null)
            addFriendButton.onClick.AddListener(HandleAddFriendClicked);
    }

    private void OnDestroy()
    {
        if (addFriendButton != null)
            addFriendButton.onClick.RemoveListener(HandleAddFriendClicked);
    }

    private void HandleAddFriendClicked()
    {
        string input = playFabIdInput != null ? playFabIdInput.text.Trim() : string.Empty;

        if (string.IsNullOrEmpty(input))
        {
            Debug.LogWarning("PlayFab ID input is empty.");
            return;
        }

        Debug.Log($"Attempting to add friend with PlayFab ID: {input}");
        OnAddFriend?.Invoke(input);
    }

    /// <summary>
    /// Refreshes the friend list UI with the given friend profiles.
    /// Clears old entries and instantiates new FriendItemView components.
    /// </summary>
    public void RefreshFriendList(List<FriendProfileData> friends)
    {
        if (friendListContainer == null || friendListItemPrefab == null)
        {
            Debug.LogWarning("[FriendManagerView] Friend list UI references are missing.");
            return;
        }

        // Clear existing entries
        foreach (Transform child in friendListContainer)
            Destroy(child.gameObject);

        // Create new entries
        foreach (var friend in friends)
        {
            var itemGO = Instantiate(friendListItemPrefab, friendListContainer);
            var itemView = itemGO.GetComponent<FriendItemView>();

            if (itemView != null)
            {
                itemView.Initialize(friend);
            }
            else
            {
                Debug.LogWarning("[FriendManagerView] Missing FriendItemView component on prefab.");
            }
        }
    }

}
