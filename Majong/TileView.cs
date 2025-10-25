using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Individual tile presentation component responsible for relaying pointer events.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public sealed class TileView : MonoBehaviour, IPointerDownHandler
{
    [SerializeField] private Image image;
    [SerializeField] private TextMeshProUGUI label;

    private RectTransform _rectTransform;
    private int _tileId = -1;
    private bool _isInteractable = true;

    public event Action<int>? Clicked;

    public RectTransform RectTransform
    {
        get
        {
            if (_rectTransform == null)
                _rectTransform = GetComponent<RectTransform>();
            return _rectTransform;
        }
    }

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        if (image == null)
            image = GetComponent<Image>();
        if (label == null)
            label = GetComponentInChildren<TextMeshProUGUI>();
    }

    /// <summary>
    /// Initializes this view with model driven data.
    /// </summary>
    public void Initialize(int tileId, string symbol, Sprite sprite, string labelText)
    {
        _tileId = tileId;
        _isInteractable = true;

        if (image == null)
            image = GetComponent<Image>();
        if (image != null)
        {
            image.sprite = sprite;
            image.color = Color.white;
        }

        if (label == null)
            label = GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
            label.text = labelText;
    }

    public void SetBrightness(float brightness)
    {
        if (image == null)
            return;

        float value = Mathf.Clamp01(brightness);
        image.color = new Color(value, value, value, 1f);
    }

    public void SetInteractable(bool interactable)
    {
        _isInteractable = interactable;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!_isInteractable || _tileId < 0)
            return;

        Clicked?.Invoke(_tileId);
    }
}
