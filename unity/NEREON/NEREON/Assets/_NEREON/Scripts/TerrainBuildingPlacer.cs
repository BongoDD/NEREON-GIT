using UnityEngine;
using MapMagic.Terrains;
using MapMagic.Core;

/// <summary>
/// Snaps all building GameObjects and the player SpawnPoint to the MapMagic terrain
/// surface after generation completes.
///
/// HOW IT WORKS
/// ─────────────
/// MapMagic 2 generates the terrain asynchronously. Once generation is fully applied
/// (TerrainTile.OnAllComplete fires), this component iterates every building GO and
/// the spawn point, samples the terrain height at each XZ position, and sets Y.
///
/// SCENE SETUP
/// ─────────────
/// 1. Add this component to the same GO as MapMagicObject (or any active GO in HomeScene).
/// 2. Wire _worldConfig (WorldConfig asset).
/// 3. Wire _spawnPointTransform to the SpawnPoint GO in CentralPlaza.
/// 4. Populate _buildingTransforms — drag each Building_01…05 GO in order (index = GameId).
///    OR tag all building root GOs with "Building" and leave _buildingTransforms empty;
///    the script will find them automatically via tag.
/// 5. Press Play — terrain generates → everything snaps to correct height.
/// </summary>
public class TerrainBuildingPlacer : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private WorldConfig _worldConfig;

    [Header("Scene References")]
    [Tooltip("The SpawnPoint GO at Central Plaza centre. Y will be snapped to terrain.")]
    [SerializeField] private Transform _spawnPointTransform;

    [Tooltip("Building root GameObjects in GameId order (index 0 = GameId 0, etc.). " +
             "Leave empty to auto-find by 'Building' tag.")]
    [SerializeField] private Transform[] _buildingTransforms;

    [Header("Placement")]
    [Tooltip("Extra height added above the snapped terrain surface (e.g. 0 = flush).")]
    [SerializeField] private float _yOffset = 0f;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void OnEnable()  => TerrainTile.OnAllComplete += OnAllTerrainComplete;
    private void OnDisable() => TerrainTile.OnAllComplete -= OnAllTerrainComplete;

    private void OnAllTerrainComplete(MapMagicObject mmObj)
    {
        SnapAll();
    }

    // ── Public ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Manually trigger snapping (e.g. from Editor play-mode button or after
    /// hot-reloading the terrain). Safe to call at any time once terrain exists.
    /// </summary>
    [ContextMenu("Snap Now")]
    public void SnapAll()
    {
        SnapSpawnPoint();
        SnapBuildings();
    }

    // ── Private ──────────────────────────────────────────────────────────────

    private void SnapSpawnPoint()
    {
        if (_spawnPointTransform == null) return;

        var pos = _spawnPointTransform.position;
        float y = SampleHeight(pos);
        _spawnPointTransform.position = new Vector3(pos.x, y + _yOffset, pos.z);

        Debug.Log($"[TerrainBuildingPlacer] SpawnPoint snapped → y={y + _yOffset:F2}");
    }

    private void SnapBuildings()
    {
        // If explicit list provided, use it; otherwise find by tag
        Transform[] targets = (_buildingTransforms != null && _buildingTransforms.Length > 0)
            ? _buildingTransforms
            : FindBuildingsByTag();

        if (_worldConfig != null)
        {
            // Use WorldConfig positions as the canonical XZ source (authoritative)
            foreach (var def in _worldConfig.Buildings)
            {
                int idx = def.GameId;
                if (idx >= targets.Length || targets[idx] == null) continue;

                var xz  = new Vector3(def.WorldPosition.x, 0f, def.WorldPosition.z);
                float y = SampleHeight(xz);
                targets[idx].position = new Vector3(xz.x, y + _yOffset, xz.z);

                Debug.Log($"[TerrainBuildingPlacer] {def.DisplayName} → ({xz.x:F1}, {y + _yOffset:F2}, {xz.z:F1})");
            }
        }
        else
        {
            // Fallback: just snap existing XZ to terrain, don't reposition
            foreach (var t in targets)
            {
                if (t == null) continue;
                var pos = t.position;
                float y = SampleHeight(pos);
                t.position = new Vector3(pos.x, y + _yOffset, pos.z);
            }
        }
    }

    private Transform[] FindBuildingsByTag()
    {
        var gos = GameObject.FindGameObjectsWithTag("Building");
        var transforms = new Transform[gos.Length];
        for (int i = 0; i < gos.Length; i++)
            transforms[i] = gos[i].transform;
        return transforms;
    }

    /// <summary>
    /// Samples terrain height at the given world XZ position across all active terrains.
    /// Returns 0 if no terrain covers that point.
    /// </summary>
    private float SampleHeight(Vector3 worldPos)
    {
        foreach (var terrain in Terrain.activeTerrains)
        {
            var td  = terrain.terrainData;
            var tp  = terrain.transform.position;

            if (worldPos.x >= tp.x && worldPos.x <= tp.x + td.size.x &&
                worldPos.z >= tp.z && worldPos.z <= tp.z + td.size.z)
            {
                return terrain.SampleHeight(worldPos) + tp.y;
            }
        }
        return 0f;
    }
}
