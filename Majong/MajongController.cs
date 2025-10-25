using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Coordinates communication between the Mahjong model and view.
/// </summary>
public sealed class MajongController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MajongView view;

    [Header("Level Settings")]
    [SerializeField, Range(1, 500)] private int level = 1;
    [SerializeField, Range(0.1f, 3f)] private float difficultyCurve = 2f;
    [SerializeField] private int maxBankSlots = 6;
    [SerializeField] private bool autoGenerateOnStart = true;

    private MajongModel _model;

    private void Awake()
    {
        _model = new MajongModel(Mathf.Max(1, maxBankSlots));
        _model.OnTilesGenerated += HandleTilesGenerated;
        _model.OnStateChanged += HandleStateChanged;
        _model.OnGameOver += HandleGameOver;

        if (view != null)
        {
            view.TileClicked += HandleTileClicked;
        }
    }

    private void OnDestroy()
    {
        if (_model != null)
        {
            _model.OnTilesGenerated -= HandleTilesGenerated;
            _model.OnStateChanged -= HandleStateChanged;
            _model.OnGameOver -= HandleGameOver;
        }

        if (view != null)
        {
            view.TileClicked -= HandleTileClicked;
        }
    }

    private void Start()
    {
        if (autoGenerateOnStart)
        {
            GenerateLevel();
        }
    }

    /// <summary>
    /// Triggers board generation with the configured parameters.
    /// </summary>
    public void GenerateLevel()
    {
        if (_model == null || view == null)
        {
            Debug.LogError("[MajongController] Missing model or view reference.");
            return;
        }

        IReadOnlyList<string> symbols = view.GetSymbolNames();
        if (symbols.Count == 0)
        {
            Debug.LogError("[MajongController] No symbols configured on the view.");
            return;
        }

        _model.Generate(level, difficultyCurve, symbols, view.TileSize);
    }

    private void HandleTileClicked(int tileId)
    {
        _model?.TrySelectTile(tileId);
    }

    private void HandleTilesGenerated(IReadOnlyList<MajongModel.TileData> tiles)
    {
        if (view == null)
            return;

        view.BuildBoard(tiles, _model.TotalLayers);
    }

    private void HandleStateChanged(MajongModel.MajongModelState state)
    {
        if (view == null)
            return;

        view.RefreshState(state);
    }

    private void HandleGameOver()
    {
        if (view == null)
            return;

        view.ShowGameOver();
    }
}
