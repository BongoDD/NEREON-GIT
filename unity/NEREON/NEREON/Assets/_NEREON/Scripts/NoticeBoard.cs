using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

/// <summary>
/// NoticeBoard — world-space leaderboard display mounted in Central Hub Plaza.
///
/// Fetches the top-5 entries from all GameLeaderboard PDAs every RefreshInterval
/// seconds and renders them in a world-space Canvas so players can walk up and
/// read this month's rankings without opening any menu.
///
/// SCENE SETUP
/// ────────────
/// 1. Place a NoticeBoardGO inside [Districts/CentralPlaza].
/// 2. Add a child: WorldCanvas (Canvas component, Render Mode = World Space,
///    scale 0.01, width 1200, height 900, sorted above terrain).
/// 3. Inside WorldCanvas:
///    ├── TabBar          — 5 TMP_Text tab buttons (one per building), same array order as WorldConfig
///    └── LeaderboardPanel
///        └── EntryContainer  — parent for 5 rows
///            ├── Row0 … Row4 — each is a TMP_Text: "#1  PlayerName  99,999"
/// 4. Wire _worldConfig, _tabLabels[0..4], _entryLabels[0..4], _headerLabel.
/// </summary>
public class NoticeBoard : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private WorldConfig _worldConfig;
    [SerializeField] private float       _refreshInterval = 60f;

    [Header("UI References")]
    [SerializeField] private TMP_Text[]  _tabLabels;    // length == worldConfig.Buildings.Length
    [SerializeField] private TMP_Text[]  _entryLabels;  // length 5 (rank 1-5)
    [SerializeField] private TMP_Text    _headerLabel;  // "THE ARENA — March 2026"

    private int   _selectedGame;

    // Cached data per game: gameId → sorted (player short, score) pairs
    private readonly Dictionary<int, List<(string name, uint score)>> _cache = new();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        PopulateTabLabels();
        SelectGame(0);
        StartCoroutine(RefreshLoop());
    }

    // ── Tab logic ─────────────────────────────────────────────────────────────

    private void PopulateTabLabels()
    {
        if (_worldConfig == null || _tabLabels == null) return;
        var buildings = _worldConfig.Buildings;
        for (int i = 0; i < _tabLabels.Length && i < buildings.Length; i++)
            _tabLabels[i].text = buildings[i].DisplayName;
    }

    /// <summary>Called by UI tab buttons via OnClick.</summary>
    public void SelectGame(int gameId)
    {
        _selectedGame = gameId;
        RenderLeaderboard(gameId);
    }

    // ── Refresh loop ──────────────────────────────────────────────────────────

    private IEnumerator RefreshLoop()
    {
        while (true)
        {
            yield return FetchAllLeaderboards();
            RenderLeaderboard(_selectedGame);
            yield return new WaitForSeconds(_refreshInterval);
        }
    }

    private IEnumerator FetchAllLeaderboards()
    {
        if (_worldConfig == null) yield break;

        byte  month = (byte)DateTime.UtcNow.Month;
        ushort year = (ushort)DateTime.UtcNow.Year;

        foreach (var building in _worldConfig.Buildings)
            yield return FetchLeaderboard(building.GameId, month, year);
    }

    private IEnumerator FetchLeaderboard(byte gameId, byte month, ushort year)
    {
        var entries = new List<(string, uint)>();
        bool done   = false;

        var task = NereonClient.FetchLeaderboardAsync(gameId, month, year).AsTask();
        task.ContinueWith(t =>
        {
            if (t.Result.HasValue)
            {
                foreach (var e in t.Result.Value.TopEntries)
                    if (!string.IsNullOrEmpty(e.Player))
                        entries.Add((e.Player.Length > 8 ? e.Player[..6] + "…" : e.Player, e.Score));
            }
            done = true;
        });

        float timeout = 10f;
        while (!done && timeout > 0f) { timeout -= Time.deltaTime; yield return null; }

        _cache[gameId] = entries;
    }

    // ── Render ────────────────────────────────────────────────────────────────

    private void RenderLeaderboard(int gameId)
    {
        if (_worldConfig == null || _entryLabels == null) return;

        // Header
        if (_headerLabel != null)
        {
            string monthName = DateTime.UtcNow.ToString("MMMM yyyy").ToUpper();
            BuildingDefinition? found = null;
            foreach (var b in _worldConfig.Buildings)
                if (b.GameId == (byte)gameId) { found = b; break; }
            _headerLabel.text = found.HasValue
                ? $"{found.Value.DisplayName.ToUpper()}\n<size=60%>{monthName}</size>"
                : monthName;
        }

        // Entries
        if (_cache.TryGetValue(gameId, out var cached))
        {
            for (int i = 0; i < _entryLabels.Length; i++)
            {
                if (_entryLabels[i] == null) continue;
                _entryLabels[i].text = i < cached.Count
                    ? $"<mspace=0.6em>#{i + 1}</mspace>  {cached[i].name,-18}  {cached[i].score:N0}"
                    : $"<mspace=0.6em>#{i + 1}</mspace>  ——";
            }
        }
        else
        {
            foreach (var label in _entryLabels)
                if (label != null) label.text = "Fetching…";
        }
    }
}
