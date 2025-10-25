using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// View responsible for translating model state into Unity UI objects.
/// </summary>
public sealed class MajongView : MonoBehaviour
{
    public static readonly List<string> SymbolNames = new()
    {
        "CandyCane",
        "GiftBox",
        "ChristmasTree",
        "Snowflake",
        "SantaHat",
        "Bell",
        "Star",
        "Gingerbread",
        "Snowman",
        "Mistletoe",
        "Bauble",
        "Wreath",
        "Stocking",
        "Sleigh",
        "Pinecone",
        "HotCocoa"
    };

    [Header("References")]
    [SerializeField] private RectTransform boardParent;
    [SerializeField] private RectTransform bankParent;
    [SerializeField] private GameObject tilePrefab;

    [Header("Visual Settings")]
    [SerializeField] private float tileSize = 125f;
    [SerializeField, Range(0.25f, 0.8f)] private float maxDarken = 0.8f;
    [SerializeField, Range(0.25f, 0.8f)] private float minDarken = 0.25f;
    [SerializeField] private float bankSpacing = 130f;

    private readonly Dictionary<int, TileView> _tileViews = new();
    private readonly Dictionary<string, Sprite> _spriteLookup = new(StringComparer.Ordinal);
    private readonly List<string> _symbolNames = new();

    private int _lastKnownLayerCount = 1;

    public event Action<int>? TileClicked;

    public float TileSize => tileSize;

    private void Awake()
    {
        LoadAllSymbols();
    }

    private void LoadAllSymbols()
    {
        _symbolNames.Clear();
        _spriteLookup.Clear();

        foreach (var name in SymbolNames)
        {
            var sprite = Resources.Load<Sprite>($"MajongSymbols/{name}");
            if (sprite != null && !_spriteLookup.ContainsKey(name))
            {
                _spriteLookup.Add(name, sprite);
            }
            else
            {
                Debug.LogWarning($"[SymbolLoader] Missing sprite: Symbols/{name}.png");
            }
        }

        Debug.Log($"[SymbolLoader] Loaded {_spriteLookup.Count}/{SymbolNames.Count} symbols.");
    }

    /// <summary>
    /// Returns the unique symbol names available to the model.
    /// </summary>
    public IReadOnlyList<string> GetSymbolNames()
    {
        if (_symbolNames.Count == 0 && _spriteLookup != null)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in _spriteLookup)
            {
                if (string.IsNullOrWhiteSpace(entry.Key))
                    continue;

                if (seen.Add(entry.Key))
                    _symbolNames.Add(entry.Key);
            }
        }

        return _symbolNames;
    }

    /// <summary>
    /// Instantiates tile prefabs for the generated tiles.
    /// </summary>
    public void BuildBoard(IReadOnlyList<MajongModel.TileData> tiles, int totalLayers)
    {
        ClearBoard();
        _lastKnownLayerCount = Mathf.Max(1, totalLayers);

        if (tiles == null)
            return;

        foreach (var tile in tiles)
        {
            CreateTileView(tile);
        }
    }

    /// <summary>
    /// Applies the latest model state to the instantiated tiles.
    /// </summary>
    public void RefreshState(MajongModel.MajongModelState state)
    {
        if (state == null)
            return;

        _lastKnownLayerCount = Mathf.Max(1, state.TotalLayers);

        var tileDataById = new Dictionary<int, MajongModel.TileData>();
        foreach (var tile in state.Tiles)
        {
            tileDataById[tile.Id] = tile;
        }

        var bankIndexById = new Dictionary<int, int>();
        for (int i = 0; i < state.BankOrder.Count; i++)
        {
            bankIndexById[state.BankOrder[i]] = i;
        }

        var idsToRemove = new List<int>();
        foreach (var pair in _tileViews)
        {
            if (!tileDataById.TryGetValue(pair.Key, out var data) || data.IsRemoved)
                idsToRemove.Add(pair.Key);
        }

        foreach (int id in idsToRemove)
        {
            if (_tileViews.TryGetValue(id, out var view))
            {
                DestroyTileView(view);
                _tileViews.Remove(id);
            }
        }

        foreach (var data in state.Tiles)
        {
            if (data.IsRemoved)
                continue;

            if (!_tileViews.TryGetValue(data.Id, out var view))
            {
                view = CreateTileView(data);
            }

            UpdateTileTransform(view, data, bankIndexById);
            UpdateTileColor(view, data);
            view.SetInteractable(!data.IsInBank);
        }
    }

    /// <summary>
    /// Displays a simple log entry when the game is over.
    /// </summary>
    public void ShowGameOver()
    {
        Debug.Log("[MajongView] GAME OVER — Bank overflowed.");
    }

    private TileView CreateTileView(MajongModel.TileData tile)
    {
        if (tilePrefab == null || boardParent == null)
        {
            Debug.LogError("[MajongView] Missing tile prefab or board parent.");
            return null;
        }

        var instance = Instantiate(tilePrefab, boardParent);
        instance.transform.localScale = Vector3.one;

        var rect = instance.GetComponent<RectTransform>();
        if (rect == null)
            rect = instance.AddComponent<RectTransform>();

        rect.sizeDelta = new Vector2(tileSize, tileSize);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition3D = new Vector3(tile.PositionX, tile.PositionY, tile.Layer);

        Sprite sprite = null;
        if (!string.IsNullOrEmpty(tile.Symbol) && _spriteLookup.TryGetValue(tile.Symbol, out var found))
            sprite = found;

        var tileView = instance.GetComponent<TileView>();
        if (tileView == null)
            tileView = instance.AddComponent<TileView>();

        tileView.Initialize(tile.Id, tile.Symbol, sprite, $"{tile.Symbol} : {tile.Layer}");
        tileView.Clicked += HandleTileClicked;

        _tileViews[tile.Id] = tileView;
        return tileView;
    }

    private void UpdateTileTransform(TileView view, MajongModel.TileData data, Dictionary<int, int> bankIndexById)
    {
        if (view == null)
            return;

        var rect = view.RectTransform;
        if (rect == null)
            return;

        if (data.IsInBank)
        {
            if (view.transform.parent != bankParent)
                view.transform.SetParent(bankParent, false);

            if (bankIndexById.TryGetValue(data.Id, out int index))
            {
                rect.anchoredPosition = new Vector2(index * bankSpacing, 0f);
            }
        }
        else
        {
            if (view.transform.parent != boardParent)
                view.transform.SetParent(boardParent, false);

            rect.anchoredPosition3D = new Vector3(data.PositionX, data.PositionY, data.Layer);
        }
    }

    private void UpdateTileColor(TileView view, MajongModel.TileData data)
    {
        if (view == null)
            return;

        float brightness = 1f;
        if (!data.IsInBank && data.IsBlocked)
        {
            float normalizedDepth = Mathf.Clamp01((float)data.BlockDepth / Mathf.Max(1, _lastKnownLayerCount));
            float min = 1f - minDarken;
            float max = 1f - maxDarken;
            brightness = Mathf.Lerp(min, max, normalizedDepth);
        }

        view.SetBrightness(brightness);
    }

    private void HandleTileClicked(int tileId)
    {
        TileClicked?.Invoke(tileId);
    }

    private void ClearBoard()
    {
        foreach (var view in _tileViews.Values)
        {
            if (view != null)
            {
                view.Clicked -= HandleTileClicked;
                DestroyTileView(view);
            }
        }

        _tileViews.Clear();
    }

    private void DestroyTileView(TileView view)
    {
        if (view == null)
            return;

        if (Application.isPlaying)
            Destroy(view.gameObject);
        else
            DestroyImmediate(view.gameObject);
    }
}
