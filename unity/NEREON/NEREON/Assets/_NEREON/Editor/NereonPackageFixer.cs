// NereonPackageFixer.cs
// ─────────────────────────────────────────────────────────────────────────────
// AUTO-RUN on every Unity Editor start (InitializeOnLoad).
//
// ROOT CAUSE
// The Solana Unity SDK ships TWO websocket DLLs inside the same package:
//   • websocket-sharp.dll       (assembly: websocket-sharp)
//   • websocket-sharp-latest.dll(assembly: websocket-sharp-latest)
// Both define the WebSocketSharp namespace/types, causing CS0121 ambiguity
// and CS0433 duplicate-type errors in the Solana SDK's own source files.
//
// FIX
// Disable websocket-sharp-latest.dll via its PluginImporter.  Only
// websocket-sharp.dll is kept; this is the version the SDK's source code
// actually references.
//
// The fix is re-applied every domain reload so it survives Library clears
// (which reset the PackageCache).  It is idempotent: if the DLL is already
// disabled, nothing happens and no asset database write occurs.
// ─────────────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Nereon.Editor
{
    [InitializeOnLoad]
    public static class NereonPackageFixer
    {
        const string DllGuid         = "762e736a4e08642f6b9543cd120c1b1d";   // websocket-sharp-latest.dll
        const string FallbackPattern = "websocket-sharp-latest.dll";

        static NereonPackageFixer()
        {
            // Defer until after AssetDatabase is ready
            EditorApplication.delayCall += ApplyFix;
        }

        static void ApplyFix()
        {
            string path = AssetDatabase.GUIDToAssetPath(DllGuid);

            if (string.IsNullOrEmpty(path))
            {
                // GUID lookup failed (fresh PackageCache clone changes path).
                // Fall back to a search.
                string[] guids = AssetDatabase.FindAssets(FallbackPattern);
                if (guids.Length == 0) return;   // not installed at all — nothing to fix
                path = AssetDatabase.GUIDToAssetPath(guids[0]);
            }

            var importer = AssetImporter.GetAtPath(path) as PluginImporter;
            if (importer == null) return;

            if (!importer.GetCompatibleWithAnyPlatform())
                return;   // already disabled — no-op

            importer.SetCompatibleWithAnyPlatform(false);
            importer.SetCompatibleWithEditor(false);
            importer.SaveAndReimport();

            Debug.Log("[NEREON] ✔ websocket-sharp-latest.dll disabled — websocket ambiguity fixed.");
        }
    }
}
#endif
