using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnlockSfx
{
    /// <summary>
    /// Assembles a ready-to-use prefab from a set of imported variation clips: a
    /// GameObject carrying an <see cref="AudioSource"/> and an
    /// <see cref="UnlockSfxRandomPlayer"/> with the clips pre-assigned. Drop the
    /// prefab into a scene and call Play() for instant non-repeating playback with
    /// pitch variance — the role Godot's AudioStreamRandomizer .tres plays.
    ///
    /// We build our own component rather than Unity's AudioRandomContainer because
    /// that type's scripting API is internal in Unity 6 (it can only be authored
    /// through the editor UI); a plain public component works in builds and across
    /// Unity versions.
    /// </summary>
    internal static class UnlockSfxBank
    {
        /// <summary>
        /// Create the prefab at <paramref name="prefabAssetPath"/> (an Assets-relative
        /// path ending in .prefab) wired to every clip in order. Returns the saved
        /// prefab asset, or null if no clips were supplied.
        /// </summary>
        public static GameObject BuildPrefab(
            string prefabAssetPath, string objectName, IList<AudioClip> clips)
        {
            if (clips == null || clips.Count == 0) return null;

            var go = new GameObject(objectName);
            try
            {
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;

                var player = go.AddComponent<UnlockSfxRandomPlayer>();
                var array = new AudioClip[clips.Count];
                clips.CopyTo(array, 0);
                player.clips = array;

                return PrefabUtility.SaveAsPrefabAsset(go, prefabAssetPath);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
