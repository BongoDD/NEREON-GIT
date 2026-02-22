using System;
using UnityEngine;

/// <summary>
/// ScriptableObject that defines every building / mini-game in the NEREON hub town.
///
/// ONE INSTANCE of this asset should live at:
///   Assets/_NEREON/Data/WorldConfig.asset
///
/// HOW TO CREATE IT:
///   Right-click in the Project window → Create → NEREON → World Config
///
/// HOW TO USE IT:
///   • Each BuildingInteraction component has a [SerializeField] WorldConfig field.
///     Drag this asset into it in the Inspector.
///   • HomeSceneManager also reads it to build the notice-board leaderboard list.
///   • AvatarManager reads SpawnPoint to place the player on HomeScene load.
///
/// ADDING A NEW GAME:
///   Simply append a new BuildingDefinition to the Buildings array here.
///   The game ID assigned must match the u8 game_id on the Anchor program.
///   No code changes required — it's all data-driven.
/// </summary>
[CreateAssetMenu(menuName = "NEREON/World Config", fileName = "WorldConfig")]
public class WorldConfig : ScriptableObject
{
    [Tooltip("All buildings in the hub town, in game-ID order.")]
    public BuildingDefinition[] Buildings = DefaultBuildings();

    // ─── Spawn Point ────────────────────────────────────────────────────────────

    [Header("Player Spawn")]
    [Tooltip(
        "World-space position where ALL players spawn when entering HomeScene — " +
        "first-time or returning. Y is snapped to terrain height at runtime by " +
        "TerrainBuildingPlacer. Placed at the centre of Central Plaza.")]
    public Vector3 SpawnPoint = new Vector3(0f, 0f, 0f);

    [Tooltip("The direction the player faces on spawn (degrees, Y-axis rotation). " +
             "0 = faces +Z (north toward Mystic Quarter).")]
    public float SpawnFacingYaw = 0f;

    /// <summary>Finds a building definition by its on-chain game ID (byte 0-255).</summary>
    public bool TryGetBuilding(byte gameId, out BuildingDefinition def)
    {
        foreach (var b in Buildings)
        {
            if (b.GameId == gameId) { def = b; return true; }
        }
        def = default;
        return false;
    }

    private static BuildingDefinition[] DefaultBuildings() => new[]
    {
        new BuildingDefinition
        {
            GameId          = 0,
            DisplayName     = "The Coin Flip",
            Description     = "A luck-and-prediction game. Flip the on-chain coin and call it.",
            District        = DistrictType.MarketBazaar,
            MiniGameScene   = "MiniGame_CoinFlip",
            MinLevelRequired = 1,
            WorldPosition   = new Vector3(-55f, 0f, -25f),  // Market Bazaar, west side
        },
        new BuildingDefinition
        {
            GameId          = 1,
            DisplayName     = "The Card Table",
            Description     = "Skill-based card game with a luck draw element.",
            District        = DistrictType.MarketBazaar,
            MiniGameScene   = "MiniGame_CardTable",
            MinLevelRequired = 1,
            WorldPosition   = new Vector3(-35f, 0f, -55f),  // Market Bazaar, south-west
        },
        new BuildingDefinition
        {
            GameId          = 2,
            DisplayName     = "The Puzzle Tower",
            Description     = "A multilayer puzzle — fastest solves win the leaderboard.",
            District        = DistrictType.MysticQuarter,
            MiniGameScene   = "MiniGame_PuzzleTower",
            MinLevelRequired = 3,
            WorldPosition   = new Vector3(45f, 0f, 55f),    // Mystic Quarter, north-east
        },
        new BuildingDefinition
        {
            GameId          = 3,
            DisplayName     = "The Oracle",
            Description     = "A mystical on-chain VRF ritual — high risk, high score potential.",
            District        = DistrictType.MysticQuarter,
            MiniGameScene   = "MiniGame_Oracle",
            MinLevelRequired = 5,
            WorldPosition   = new Vector3(65f, 0f, 30f),    // Mystic Quarter, east
        },
        new BuildingDefinition
        {
            GameId          = 4,
            DisplayName     = "The Arena",
            Description     = "High-score action game. Only the best earn a spot on the podium.",
            District        = DistrictType.ChampionArena,
            MiniGameScene   = "MiniGame_Arena",
            MinLevelRequired = 8,
            WorldPosition   = new Vector3(55f, 0f, -50f),   // Champion Arena, south-east
        },
    };
}

// ─── Building Definition ──────────────────────────────────────────────────────

[Serializable]
public struct BuildingDefinition
{
    [Tooltip("On-chain game ID (u8). Must match the Anchor program's game_id byte.")]
    [Range(0, 255)]
    public byte GameId;

    [Tooltip("Friendly name shown on the building sign and in the notice board.")]
    public string DisplayName;

    [Tooltip("Short description shown in the entry prompt UI.")]
    [TextArea(2, 4)]
    public string Description;

    [Tooltip("Which district of the hub town this building belongs to.")]
    public DistrictType District;

    [Tooltip("Exact scene name in Build Settings for this mini-game.")]
    public string MiniGameScene;

    [Tooltip("Minimum character level required to enter. 1 = open to all.")]
    [Min(1)]
    public ushort MinLevelRequired;

    /// <summary>
    /// Fixed world-space position of this building on the MapMagic terrain.
    /// Y is the designer-intent height; TerrainBuildingPlacer will snap it to
    /// the actual terrain surface at runtime after MapMagic finishes generating.
    /// Origin (0,0,0) is the centre of the 200×200 map (Central Plaza / spawn point).
    /// </summary>
    [Tooltip("Fixed world-space XZ position. Y is snapped to terrain at runtime.")]
    public Vector3 WorldPosition;
}

// ─── District Enum ────────────────────────────────────────────────────────────

public enum DistrictType
{
    CentralPlaza,
    MarketBazaar,
    MysticQuarter,
    ChampionArena,
}
