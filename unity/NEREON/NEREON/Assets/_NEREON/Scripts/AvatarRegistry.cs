using System;
using UnityEngine;

/// <summary>
/// ScriptableObject that maps on-chain avatar_id bytes (0–255) to Unity prefabs.
///
/// CREATE ONE INSTANCE AT:
///   Assets/_NEREON/Data/AvatarRegistry.asset
///   Right-click → Create → NEREON → Avatar Registry
///
/// ADDING AN AVATAR:
///   1. Import the character model (FBX/GLB) into Assets/_NEREON/Models/Avatars/
///   2. Create a prefab with the character + Animator + AvatarProgressionEffects component.
///   3. Add an AvatarEntry here with the next available AvatarId.
///   4. Update WelcomeInitScene's AvatarSelector to show the new option.
///
/// STARTER ASSETS CHARACTER:
///   The Starter Assets Third Person prefab can be used as Avatar ID 0.
///   Duplicate it, strip its PlayerInput component (we add our own), save as prefab.
///
/// QUATERNIUS RPG CHARACTERS:
///   After importing quaternius.com/packs/rpgcharacters.html :
///   Each character GLB becomes its own avatar entry.
/// </summary>
[CreateAssetMenu(menuName = "NEREON/Avatar Registry", fileName = "AvatarRegistry")]
public class AvatarRegistry : ScriptableObject
{
    [Tooltip("All registered avatars. AvatarId must be unique and match on-chain data.")]
    public AvatarEntry[] Avatars = Array.Empty<AvatarEntry>();

    /// <summary>Returns the prefab for the given on-chain avatar_id, or null if not found.</summary>
    public GameObject GetPrefab(byte avatarId)
    {
        foreach (var entry in Avatars)
            if (entry.AvatarId == avatarId) return entry.Prefab;

        Debug.LogWarning($"[AvatarRegistry] No prefab registered for avatar_id {avatarId}. Using fallback.");
        return Avatars.Length > 0 ? Avatars[0].Prefab : null;
    }

    /// <summary>Returns the display name shown during avatar selection in WelcomeInitScene.</summary>
    public string GetDisplayName(byte avatarId)
    {
        foreach (var entry in Avatars)
            if (entry.AvatarId == avatarId) return entry.DisplayName;
        return "Unknown";
    }
}

[Serializable]
public struct AvatarEntry
{
    [Tooltip("On-chain avatar_id byte. Must match what is stored in UserProfile.avatar_id.")]
    [Range(0, 255)]
    public byte AvatarId;

    [Tooltip("Display name shown in avatar selection UI.")]
    public string DisplayName;

    [Tooltip("Short description shown under the avatar preview.")]
    [TextArea(1, 2)]
    public string Description;

    [Tooltip("The 3D character prefab to spawn in the scene. " +
             "Must have an Animator with a Humanoid rig.")]
    public GameObject Prefab;

    [Tooltip("Preview thumbnail shown in the WelcomeInitScene avatar carousel.")]
    public Sprite PreviewImage;
}
