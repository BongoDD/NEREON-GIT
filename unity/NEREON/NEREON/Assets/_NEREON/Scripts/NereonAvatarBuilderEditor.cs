// NereonAvatarBuilderEditor.cs
// Builds 4 humanoid placeholder prefabs with proper bone hierarchy + Animator.
// Menu: NEREON → Build Placeholder Avatars
// Prefabs saved to: Assets/_NEREON/Prefabs/Avatars/Placeholder/
//
// Bone hierarchy per prefab:
//   Root (Animator + Rigidbody + CapsuleCollider + AvatarIdTag)
//    └─ Hips
//        ├─ Spine → Chest → Neck → Head
//        ├─ Chest → L_Shoulder → L_Elbow → L_Hand
//        ├─ Chest → R_Shoulder → R_Elbow → R_Hand
//        ├─ Hips  → L_Thigh   → L_Knee  → L_Foot
//        └─ Hips  → R_Thigh   → R_Knee  → R_Foot
//
// Assign any Generic Animator Controller to the Animator component to start
// testing locomotion, attack, and idle animations immediately.

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nereon.Editor
{
    public static class NereonAvatarBuilderEditor
    {
        private const string PrefabOutDir = "Assets/_NEREON/Prefabs/Avatars/Placeholder";

        // ── Class definitions ──────────────────────────────────────────────
        private static readonly (string name, Color body, Color accent, Color skin)[] Classes =
        {
            ("Warrior", new Color(0.72f, 0.12f, 0.12f), new Color(0.9f,  0.3f, 0.1f),  new Color(0.93f, 0.78f, 0.68f)),
            ("Mage",    new Color(0.13f, 0.18f, 0.72f), new Color(0.5f,  0.2f, 0.95f), new Color(0.87f, 0.80f, 0.95f)),
            ("Rogue",   new Color(0.10f, 0.42f, 0.15f), new Color(0.2f,  0.72f,0.3f),  new Color(0.88f, 0.76f, 0.65f)),
            ("Paladin", new Color(0.72f, 0.60f, 0.10f), new Color(0.95f, 0.85f,0.2f),  new Color(0.95f, 0.82f, 0.72f)),
        };

        [MenuItem("NEREON/Build Placeholder Avatars")]
        public static void BuildAll()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_NEREON/Prefabs"))
                AssetDatabase.CreateFolder("Assets/_NEREON", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/_NEREON/Prefabs/Avatars"))
                AssetDatabase.CreateFolder("Assets/_NEREON/Prefabs", "Avatars");
            if (!AssetDatabase.IsValidFolder(PrefabOutDir))
                AssetDatabase.CreateFolder("Assets/_NEREON/Prefabs/Avatars", "Placeholder");

            for (int i = 0; i < Classes.Length; i++)
            {
                var (name, body, accent, skin) = Classes[i];
                var root = BuildHumanoid(name, body, accent, skin, (byte)i);
                string path = $"{PrefabOutDir}/{name}_Placeholder.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);
                Debug.Log($"[NEREON] Saved: {path}");
            }

            AssetDatabase.Refresh();
            Debug.Log("[NEREON] ✔ All 4 placeholder avatars built.");
        }

        // ── Humanoid builder ───────────────────────────────────────────────
        private static GameObject BuildHumanoid(string className, Color bodyCol, Color accentCol, Color skinCol, byte avatarId)
        {
            var root = new GameObject($"{className}_Placeholder");
            root.tag = "Player";

            // Animator — Generic rig; assign controller in Inspector
            root.AddComponent<Animator>().applyRootMotion = true;

            // Physics
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 80f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            var cc = root.AddComponent<CapsuleCollider>();
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0, 0.9f, 0);

            // Avatar class tag (read by AvatarManager)
            var tag = root.AddComponent<AvatarIdTag>();
            tag.avatarId = avatarId;

            // ── Bone hierarchy ─────────────────────────────────────────────
            var hips     = Bone("Hips",      root,      new Vector3( 0,      0.90f,  0));
            var spine    = Bone("Spine",     hips,      new Vector3( 0,      0.15f,  0));
            var chest    = Bone("Chest",     spine,     new Vector3( 0,      0.15f,  0));
            var neck     = Bone("Neck",      chest,     new Vector3( 0,      0.25f,  0));
            var head     = Bone("Head",      neck,      new Vector3( 0,      0.12f,  0));

            var lShoulder = Bone("L_Shoulder", chest,    new Vector3(-0.22f,  0.12f,  0));
            var lElbow    = Bone("L_Elbow",    lShoulder,new Vector3(-0.28f,  0,      0));
            var lHand     = Bone("L_Hand",     lElbow,   new Vector3(-0.25f,  0,      0));

            var rShoulder = Bone("R_Shoulder", chest,    new Vector3( 0.22f,  0.12f,  0));
            var rElbow    = Bone("R_Elbow",    rShoulder,new Vector3( 0.28f,  0,      0));
            var rHand     = Bone("R_Hand",     rElbow,   new Vector3( 0.25f,  0,      0));

            var lThigh  = Bone("L_Thigh",  hips,      new Vector3(-0.12f, -0.05f,  0));
            var lKnee   = Bone("L_Knee",   lThigh,    new Vector3( 0,     -0.42f,  0));
            var lFoot   = Bone("L_Foot",   lKnee,     new Vector3( 0,     -0.42f,  0));

            var rThigh  = Bone("R_Thigh",  hips,      new Vector3( 0.12f, -0.05f,  0));
            var rKnee   = Bone("R_Knee",   rThigh,    new Vector3( 0,     -0.42f,  0));
            var rFoot   = Bone("R_Foot",   rKnee,     new Vector3( 0,     -0.42f,  0));

            // Suppress "unused variable" for leaf bones referenced by accessories below
            _ = lFoot; _ = rFoot;

            // ── Mesh visuals ───────────────────────────────────────────────
            var matBody   = CreateMat(bodyCol);
            var matAccent = CreateMat(accentCol);
            var matSkin   = CreateMat(skinCol);

            // Torso
            Mesh(chest,     PrimitiveType.Cube,     matBody,   new Vector3(0.46f,0.28f,0.22f), new Vector3( 0,     0.14f,  0));
            // Pelvis
            Mesh(hips,      PrimitiveType.Cube,     matBody,   new Vector3(0.38f,0.20f,0.20f), new Vector3( 0,     0.10f,  0));
            // Head
            Mesh(head,      PrimitiveType.Sphere,   matSkin,   new Vector3(0.28f,0.28f,0.28f), new Vector3( 0,     0.14f,  0));
            // Neck
            Mesh(neck,      PrimitiveType.Cylinder, matSkin,   new Vector3(0.10f,0.10f,0.10f), new Vector3( 0,     0.06f,  0));
            // Arms
            Mesh(lShoulder, PrimitiveType.Cylinder, matAccent, new Vector3(0.08f,0.14f,0.08f), new Vector3(-0.14f, 0,      0), Quaternion.Euler(0,0,90));
            Mesh(lElbow,    PrimitiveType.Cylinder, matBody,   new Vector3(0.07f,0.12f,0.07f), new Vector3(-0.12f, 0,      0), Quaternion.Euler(0,0,90));
            Mesh(rShoulder, PrimitiveType.Cylinder, matAccent, new Vector3(0.08f,0.14f,0.08f), new Vector3( 0.14f, 0,      0), Quaternion.Euler(0,0,90));
            Mesh(rElbow,    PrimitiveType.Cylinder, matBody,   new Vector3(0.07f,0.12f,0.07f), new Vector3( 0.12f, 0,      0), Quaternion.Euler(0,0,90));
            // Legs
            Mesh(lThigh,    PrimitiveType.Cylinder, matBody,   new Vector3(0.10f,0.20f,0.10f), new Vector3( 0,    -0.21f,  0));
            Mesh(lKnee,     PrimitiveType.Cylinder, matAccent, new Vector3(0.09f,0.20f,0.09f), new Vector3( 0,    -0.21f,  0));
            Mesh(rThigh,    PrimitiveType.Cylinder, matBody,   new Vector3(0.10f,0.20f,0.10f), new Vector3( 0,    -0.21f,  0));
            Mesh(rKnee,     PrimitiveType.Cylinder, matAccent, new Vector3(0.09f,0.20f,0.09f), new Vector3( 0,    -0.21f,  0));

            // Class-specific accessory
            switch (avatarId)
            {
                case 0: // Warrior — shield on left wrist
                    Mesh(lHand,  PrimitiveType.Cube,     matAccent, new Vector3(0.22f,0.28f,0.04f), new Vector3(-0.12f, 0, 0.06f));
                    break;
                case 1: // Mage — glowing orb on right hand
                    Mesh(rHand,  PrimitiveType.Sphere,   matAccent, new Vector3(0.14f,0.14f,0.14f), new Vector3( 0.10f, 0, 0));
                    break;
                case 2: // Rogue — dual blades
                    Mesh(lHand,  PrimitiveType.Cylinder, matAccent, new Vector3(0.03f,0.20f,0.03f), new Vector3(-0.10f, 0, 0), Quaternion.Euler(0,0,90));
                    Mesh(rHand,  PrimitiveType.Cylinder, matAccent, new Vector3(0.03f,0.20f,0.03f), new Vector3( 0.10f, 0, 0), Quaternion.Euler(0,0,90));
                    break;
                case 3: // Paladin — crown ring
                    Mesh(head,   PrimitiveType.Cylinder, matAccent, new Vector3(0.34f,0.04f,0.34f), new Vector3( 0,     0.30f, 0));
                    break;
            }

            return root;
        }

        // ── Helpers ────────────────────────────────────────────────────────

        private static GameObject Bone(string name, GameObject parent, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            return go;
        }

        private static void Mesh(GameObject bone, PrimitiveType pType,
            Material mat, Vector3 scale, Vector3 offset, Quaternion? rot = null)
        {
            var go = GameObject.CreatePrimitive(pType);
            go.name = $"{bone.name}_Mesh";
            go.transform.SetParent(bone.transform, false);
            go.transform.localPosition = offset;
            go.transform.localScale    = scale;
            if (rot.HasValue) go.transform.localRotation = rot.Value;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // root handles physics
        }

        private static Material CreateMat(Color col)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader) { color = col };
            return mat;
        }
    }
}
#endif
