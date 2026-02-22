#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

/// <summary>
/// NereonPlayerPrefabBuilder — menu item: NEREON → Build Player Prefab
///
/// Uses Unity Starter Assets – Third Person as the character base:
///   • Finds the StarterAssets ThirdPersonController prefab automatically
///   • Adds all NEREON networking + HUD components on top
///   • Saves result to Assets/_NEREON/Prefabs/HubPlayer.prefab
///
/// PREREQUISITES
///   1. Import "Starter Assets – Third Person" from the Unity Asset Store
///   2. Make sure MapMagic, NGO packages are installed (already done)
///
/// AFTER RUNNING
///   1. Open HomeScene
///   2. Run NEREON → Build HomeScene (if not done already)
///   3. Drag HubPlayer.prefab into [Player] in the Hierarchy
///   4. Wire NereonNetworkManager._hubPlayerPrefab to HubPlayer.prefab
///   5. Wire PlayerSetup._spawnPoint to [Districts/CentralPlaza/SpawnPoint]
///   6. Press Play ▶
/// </summary>
public static class NereonPlayerPrefabBuilder
{
    const string PrefabOutputPath = "Assets/_NEREON/Prefabs/HubPlayer.prefab";

    // Known Starter Assets prefab locations (Unity changes them between versions)
    static readonly string[] StarterAssetsPrefabSearchPaths =
    {
        "Assets/StarterAssets/ThirdPersonController/Prefabs/NestedParent_Unpack.prefab",
        "Assets/StarterAssets/ThirdPersonController/Prefabs/PlayerCapsule.prefab",
        "Assets/StarterAssets/ThirdPersonController/Characters/Humanoid/Character Animated.prefab",
        "Assets/StarterAssets/ThirdPersonController/Characters/Humanoid/PlayerArmature.prefab",
        "Assets/StarterAssets/ThirdPersonController/Characters/Humanoid/Character.prefab",
    };

    [MenuItem("NEREON/Build Player Prefab")]
    public static void BuildPlayerPrefab()
    {
        EnsureDir("Assets/_NEREON/Prefabs");

        // ── Find the Starter Assets base prefab ───────────────────────────────
        GameObject starterPrefab = FindStarterAssetsPrefab();

        if (starterPrefab == null)
        {
            EditorUtility.DisplayDialog(
                "Starter Assets Not Found",
                "Could not find the Starter Assets – Third Person Controller prefab.\n\n" +
                "Please import 'Starter Assets – Third Person' from the Unity Asset Store " +
                "then run NEREON → Build Player Prefab again.",
                "OK");
            return;
        }

        Debug.Log($"[NereonPlayerPrefabBuilder] Using base: {AssetDatabase.GetAssetPath(starterPrefab)}");

        // ── Instantiate in scene (needed to save as prefab) ───────────────────
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(starterPrefab);
        instance.name = "HubPlayer";

        // ── Tag the player ────────────────────────────────────────────────────
        if (!TagExists("Player"))
            Debug.LogWarning("[NereonPlayerPrefabBuilder] 'Player' tag not found. Add it via Edit → Project Settings → Tags.");
        else
            instance.tag = "Player";

        // ── Add NEREON components ─────────────────────────────────────────────

        // NGO: NetworkObject + NetworkTransform (must be on root)
        if (instance.GetComponent<NetworkObject>() == null)
            instance.AddComponent<NetworkObject>();

        if (instance.GetComponent<NetworkTransform>() == null)
            instance.AddComponent<NetworkTransform>();

        // PlayerSetup — handles spawn positioning + cursor lock
        if (instance.GetComponent<PlayerSetup>() == null)
            instance.AddComponent<PlayerSetup>();

        // HubPlayerNetwork — syncs avatar id, level, username + chat
        if (instance.GetComponent<HubPlayerNetwork>() == null)
            instance.AddComponent<HubPlayerNetwork>();

        // BubbleChat — world-space speech bubble renderer
        if (instance.GetComponent<BubbleChat>() == null)
            instance.AddComponent<BubbleChat>();

        // BubbleContainer child for chat UI positioning (above the character head)
        EnsureChild(instance.transform, "BubbleContainer", new Vector3(0f, 2.2f, 0f));

        // ── Save as prefab ────────────────────────────────────────────────────
        bool success;
        PrefabUtility.SaveAsPrefabAsset(instance, PrefabOutputPath, out success);
        Object.DestroyImmediate(instance);

        if (success)
        {
            AssetDatabase.Refresh();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabOutputPath);
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"[NereonPlayerPrefabBuilder] ✔ HubPlayer.prefab saved at {PrefabOutputPath}");
            EditorUtility.DisplayDialog(
                "HubPlayer Prefab Created",
                "HubPlayer.prefab has been saved to:\n" + PrefabOutputPath +
                "\n\nNext steps:\n" +
                "1. Drag it into [Player] in HomeScene Hierarchy\n" +
                "2. Wire NereonNetworkManager._hubPlayerPrefab → HubPlayer.prefab\n" +
                "3. Wire PlayerSetup._spawnPoint → CentralPlaza/SpawnPoint\n" +
                "4. Press ▶ Play",
                "OK");
        }
        else
        {
            Debug.LogError("[NereonPlayerPrefabBuilder] Failed to save prefab.");
        }
    }

    // ── Find Starter Assets prefab ────────────────────────────────────────────

    static GameObject FindStarterAssetsPrefab()
    {
        // 1. Check known paths first
        foreach (var path in StarterAssetsPrefabSearchPaths)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (p != null) return p;
        }

        // 2. Search by GUID/name across entire project
        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/StarterAssets" });
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var name = Path.GetFileNameWithoutExtension(path).ToLower();
            if (name.Contains("player") || name.Contains("character") || name.Contains("armature"))
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (p != null) return p;
            }
        }

        // 3. Broader search anywhere in Assets
        guids = AssetDatabase.FindAssets("PlayerArmature t:Prefab");
        if (guids.Length > 0)
            return AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));

        guids = AssetDatabase.FindAssets("PlayerCapsule t:Prefab");
        if (guids.Length > 0)
            return AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));

        return null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static Transform EnsureChild(Transform parent, string name, Vector3 localPos)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing;
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        return go.transform;
    }

    static bool TagExists(string tag)
    {
        foreach (var t in UnityEditorInternal.InternalEditorUtility.tags)
            if (t == tag) return true;
        return false;
    }

    static void EnsureDir(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parts   = path.Split('/');
        var current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif
