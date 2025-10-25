using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Data-centric Mahjong-like game model that encapsulates tile generation,
/// bank logic, and blocked tile detection without any Unity dependencies.
/// </summary>
public sealed class MajongModel
{
    /// <summary>
    /// Immutable snapshot describing a single tile instance.
    /// </summary>
    public readonly struct TileData
    {
        public int Id { get; }
        public string Symbol { get; }
        public int GridX { get; }
        public int GridY { get; }
        public int Layer { get; }
        public float PositionX { get; }
        public float PositionY { get; }
        public bool IsBlocked { get; }
        public int BlockDepth { get; }
        public bool IsInBank { get; }
        public bool IsRemoved { get; }

        internal TileData(
            int id,
            string symbol,
            int gridX,
            int gridY,
            int layer,
            float positionX,
            float positionY,
            bool isBlocked,
            int blockDepth,
            bool isInBank,
            bool isRemoved)
        {
            Id = id;
            Symbol = symbol;
            GridX = gridX;
            GridY = gridY;
            Layer = layer;
            PositionX = positionX;
            PositionY = positionY;
            IsBlocked = isBlocked;
            BlockDepth = blockDepth;
            IsInBank = isInBank;
            IsRemoved = isRemoved;
        }
    }

    /// <summary>
    /// Snapshot representing the current state of the model for observers.
    /// </summary>
    public sealed class MajongModelState
    {
        public IReadOnlyList<TileData> Tiles { get; }
        public IReadOnlyList<int> BankOrder { get; }
        public int TotalLayers { get; }

        public MajongModelState(IReadOnlyList<TileData> tiles, IReadOnlyList<int> bankOrder, int totalLayers)
        {
            Tiles = tiles;
            BankOrder = bankOrder;
            TotalLayers = totalLayers;
        }
    }

    private sealed class TileRuntime
    {
        public int Id;
        public string Symbol = string.Empty;
        public int GridX;
        public int GridY;
        public int Layer;
        public float PositionX;
        public float PositionY;
        public bool IsBlocked;
        public int BlockDepth;
        public bool IsInBank;
        public bool IsRemoved;
    }

    private readonly Dictionary<int, TileRuntime> _tilesById = new();
    private readonly List<int> _tileOrder = new();
    private readonly List<int> _bankOrder = new();

    private readonly int _maxBankSlots;

    private float _tileSize = 125f;
    private int _rows = 4;
    private int _cols = 4;
    private int _currentLayerCount = 1;
    private int _nextTileId = 0;
    private Random _random = new Random();

    public MajongModel(int maxBankSlots)
    {
        if (maxBankSlots <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxBankSlots), "Bank size must be positive.");

        _maxBankSlots = maxBankSlots;
    }

    public event Action<IReadOnlyList<TileData>>? OnTilesGenerated;
    public event Action<MajongModelState>? OnStateChanged;
    public event Action? OnGameOver;

    public int TotalLayers => _currentLayerCount;

    /// <summary>
    /// Generates a new board using the provided settings.
    /// </summary>
    public void Generate(int level, float difficultyCurve, IReadOnlyList<string> availableSymbols, float tileSize, int? randomSeed = null)
    {
        if (availableSymbols == null || availableSymbols.Count == 0)
            throw new ArgumentException("At least one symbol is required to generate tiles.", nameof(availableSymbols));

        _random = randomSeed.HasValue ? new Random(randomSeed.Value) : new Random();
        _tileSize = tileSize;

        _tilesById.Clear();
        _tileOrder.Clear();
        _bankOrder.Clear();
        _nextTileId = 0;

        float diff = Clamp01(InverseLerp(1f, 500f, level));
        float curve = Pow(diff, difficultyCurve);

        int gridSize = RoundToInt(Lerp(4f, 9f, curve));
        _rows = gridSize;
        _cols = gridSize;
        _currentLayerCount = RoundToInt(Lerp(1f, 10f, curve));

        int uniqueSymbols = Clamp(RoundToInt(Lerp(3f, availableSymbols.Count, curve)), 3, availableSymbols.Count);

        int gridCapacity = _rows * _cols * _currentLayerCount;
        int tileAmount = Clamp(RoundToInt(Lerp(30f, gridCapacity, curve)), 3, gridCapacity);
        tileAmount -= tileAmount % 3;
        int triplets = tileAmount / 3;

        var assignedSymbols = BuildSymbolPool(availableSymbols, triplets, uniqueSymbols);

        GenerateTiles(assignedSymbols, tileAmount);
        DetectBlockedTiles();
        RaiseTilesGenerated();
    }

    /// <summary>
    /// Attempts to move the specified tile into the bank.
    /// </summary>
    public bool TrySelectTile(int tileId)
    {
        if (!_tilesById.TryGetValue(tileId, out var tile))
            return false;

        if (tile.IsRemoved || tile.IsInBank)
            return false;

        if (tile.IsBlocked)
            return false;

        if (_bankOrder.Count >= _maxBankSlots)
        {
            OnGameOver?.Invoke();
            return false;
        }

        tile.IsInBank = true;
        _bankOrder.Add(tileId);

        HandleTriplets(tile.Symbol);
        DetectBlockedTiles();
        RaiseStateChanged();
        return true;
    }

    private void HandleTriplets(string symbol)
    {
        int matchingCount = 0;
        foreach (int id in _bankOrder)
        {
            if (_tilesById[id].Symbol == symbol)
                matchingCount++;
        }

        if (matchingCount < 3)
            return;

        var toRemove = new List<int>();
        foreach (int id in _bankOrder)
        {
            if (_tilesById[id].Symbol == symbol)
                toRemove.Add(id);
        }

        foreach (int id in toRemove)
        {
            var runtime = _tilesById[id];
            runtime.IsRemoved = true;
            runtime.IsInBank = false;
        }

        _bankOrder.RemoveAll(id => _tilesById[id].Symbol == symbol);
    }

    private List<string> BuildSymbolPool(IReadOnlyList<string> availableSymbols, int triplets, int uniqueSymbols)
    {
        var pool = new List<string>(triplets * 3);
        for (int i = 0; i < triplets; i++)
        {
            string symbol = availableSymbols[i % uniqueSymbols];
            pool.Add(symbol);
            pool.Add(symbol);
            pool.Add(symbol);
        }

        Shuffle(pool);
        return pool;
    }

    private void GenerateTiles(IReadOnlyList<string> symbols, int tileAmount)
    {
        _tilesById.Clear();
        _tileOrder.Clear();

        var weights = new List<float>(_currentLayerCount);
        for (int i = 0; i < _currentLayerCount; i++)
            weights.Add(_currentLayerCount - i);

        float totalWeight = weights.Sum();
        int remaining = tileAmount;
        int symbolIndex = 0;

        for (int layer = 0; layer < _currentLayerCount; layer++)
        {
            int layerTiles = RoundToInt(tileAmount * (weights[layer] / totalWeight));
            if (layer == _currentLayerCount - 1)
            {
                layerTiles = remaining;
            }
            else
            {
                remaining -= layerTiles;
            }

            layerTiles = Math.Min(layerTiles, _rows * _cols);

            float offsetX = NextBool() ? _tileSize * 0.5f * (NextBool() ? 1f : -1f) : 0f;
            float offsetY = NextBool() ? _tileSize * 0.5f * (NextBool() ? 1f : -1f) : 0f;

            var allPositions = new List<(int x, int y)>(_rows * _cols);
            for (int y = 0; y < _rows; y++)
            {
                for (int x = 0; x < _cols; x++)
                {
                    allPositions.Add((x, y));
                }
            }

            Shuffle(allPositions);

            int count = Math.Min(layerTiles, allPositions.Count);
            for (int i = 0; i < count && symbolIndex < symbols.Count; i++)
            {
                var pos = allPositions[i];
                string symbol = symbols[symbolIndex++];

                float anchorX = (pos.x - (_cols - 1) / 2f) * _tileSize + offsetX;
                float anchorY = (pos.y - (_rows - 1) / 2f) * _tileSize + offsetY;

                var tile = new TileRuntime
                {
                    Id = _nextTileId++,
                    Symbol = symbol,
                    GridX = pos.x,
                    GridY = pos.y,
                    Layer = layer,
                    PositionX = anchorX,
                    PositionY = anchorY,
                    IsBlocked = false,
                    BlockDepth = 0,
                    IsInBank = false,
                    IsRemoved = false
                };

                _tilesById.Add(tile.Id, tile);
                _tileOrder.Add(tile.Id);
            }
        }
    }

    private void DetectBlockedTiles()
    {
        float overlapThreshold = _tileSize * 0.25f;

        foreach (int tileId in _tileOrder)
        {
            var tile = _tilesById[tileId];
            if (tile.IsRemoved || tile.IsInBank)
            {
                tile.IsBlocked = false;
                tile.BlockDepth = 0;
                continue;
            }

            tile.IsBlocked = false;
            tile.BlockDepth = 0;

            foreach (int otherId in _tileOrder)
            {
                if (tileId == otherId)
                    continue;

                var other = _tilesById[otherId];
                if (other.IsRemoved || other.IsInBank)
                    continue;

                if (other.Layer <= tile.Layer)
                    continue;

                float dx = Math.Abs(tile.PositionX - other.PositionX);
                float dy = Math.Abs(tile.PositionY - other.PositionY);

                bool overlapX = dx < (_tileSize - overlapThreshold);
                bool overlapY = dy < (_tileSize - overlapThreshold);

                if (overlapX && overlapY)
                {
                    tile.IsBlocked = true;
                    tile.BlockDepth++;
                }
            }
        }
    }

    private void RaiseTilesGenerated()
    {
        var tiles = CreateTileSnapshots();
        OnTilesGenerated?.Invoke(tiles);
        OnStateChanged?.Invoke(new MajongModelState(new List<TileData>(tiles), new List<int>(_bankOrder), _currentLayerCount));
    }

    private void RaiseStateChanged()
    {
        var tiles = CreateTileSnapshots();
        OnStateChanged?.Invoke(new MajongModelState(tiles, new List<int>(_bankOrder), _currentLayerCount));
    }

    private List<TileData> CreateTileSnapshots()
    {
        var snapshot = new List<TileData>(_tileOrder.Count);
        foreach (int id in _tileOrder)
        {
            var tile = _tilesById[id];
            snapshot.Add(new TileData(
                tile.Id,
                tile.Symbol,
                tile.GridX,
                tile.GridY,
                tile.Layer,
                tile.PositionX,
                tile.PositionY,
                tile.IsBlocked,
                tile.BlockDepth,
                tile.IsInBank,
                tile.IsRemoved));
        }

        return snapshot;
    }

    private void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private bool NextBool() => _random.NextDouble() > 0.5;

    private static float Clamp01(float value) => value < 0f ? 0f : (value > 1f ? 1f : value);

    private static float Clamp(float value, float min, float max) => Math.Min(Math.Max(value, min), max);

    private static int Clamp(int value, int min, int max) => Math.Min(Math.Max(value, min), max);

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static float InverseLerp(float a, float b, float value)
    {
        if (Math.Abs(b - a) < float.Epsilon)
            return 0f;
        return (value - a) / (b - a);
    }

    private static float Pow(float value, float power) => (float)Math.Pow(value, power);

    private static int RoundToInt(float value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
