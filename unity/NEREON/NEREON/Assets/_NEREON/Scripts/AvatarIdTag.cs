// AvatarIdTag.cs
// Lightweight component stamped onto every placeholder/real avatar prefab.
// Maps the on-chain avatar_id byte so AvatarManager can read which class is spawned.
using UnityEngine;

namespace Nereon
{
    public class AvatarIdTag : MonoBehaviour
    {
        [Range(0, 3)]
        public byte avatarId;
    }
}
