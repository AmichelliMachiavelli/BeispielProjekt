using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central lookup for all available mini-games, their sprites, and descriptions.
/// Loads sprites automatically from Resources/GameSprites using file name.
/// </summary>
public sealed class GameLibrary : MonoBehaviour
{
    [Serializable]
    private sealed class GameEntry
    {
        public DoorGameType GameType;

        [Tooltip("File name of the sprite inside Resources/GameSprites (without extension).")]
        public string FileName;

        [TextArea]
        public string Description;
    }

    private readonly Dictionary<DoorGameType, GameDefinition> _lookup = new();

    [SerializeField] private GameEntry[] entries;

    public static GameLibrary Instance { get; private set; }

    private const string SpriteFolder = "GameSprites";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[GameLibrary] Duplicate instance detected. Destroying the newest instance.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildLookup();
    }

    private void BuildLookup()
    {
        _lookup.Clear();

        if (entries == null || entries.Length == 0)
        {
            Debug.LogWarning("[GameLibrary] No game entries configured.");
            return;
        }

        foreach (var entry in entries)
        {
            if (entry == null)
                continue;

            Sprite sprite = LoadSprite(entry.FileName);
            if (sprite == null)
            {
                Debug.LogWarning($"[GameLibrary] Could not load sprite '{entry.FileName}' from Resources/{SpriteFolder}");
            }

            var def = new GameDefinition(entry.GameType, entry.Description, sprite);
            _lookup[entry.GameType] = def;
        }

        Debug.Log($"[GameLibrary] Loaded {_lookup.Count} game definitions from Resources/{SpriteFolder}.");
    }

    private Sprite LoadSprite(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        string path = $"{SpriteFolder}/{fileName}";
        Sprite sprite = Resources.Load<Sprite>(path);
        return sprite;
    }

    public bool TryGetGame(DoorGameType type, out GameDefinition definition)
    {
        return _lookup.TryGetValue(type, out definition);
    }

    public sealed class GameDefinition
    {
        public DoorGameType GameType { get; }
        public string Description { get; }
        public Sprite Sprite { get; }

        public GameDefinition(DoorGameType type, string description, Sprite sprite)
        {
            GameType = type;
            Description = description;
            Sprite = sprite;
        }
    }
}
