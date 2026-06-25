using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnlockSfx
{
    /// <summary>
    /// Plays a clip inside the editor without entering Play mode. Unity has no
    /// public editor-preview API, so we call the internal UnityEditor.AudioUtil
    /// via reflection (the long-standing standard approach). Resolved members are
    /// cached; if a future Unity renames them, preview no-ops instead of throwing.
    /// </summary>
    internal static class UnlockSfxPreview
    {
        static MethodInfo _play;
        static MethodInfo _stop;
        static bool _resolved;

        public static bool Available
        {
            get { Resolve(); return _play != null; }
        }

        public static void Play(AudioClip clip, bool loop)
        {
            if (clip == null) return;
            Resolve();
            if (_play == null) return;

            try
            {
                Stop();
                // PlayPreviewClip(AudioClip clip, int startSample, bool loop)
                _play.Invoke(null, new object[] { clip, 0, loop });
            }
            catch { /* preview is best-effort */ }
        }

        public static void Stop()
        {
            Resolve();
            if (_stop == null) return;
            try { _stop.Invoke(null, null); }
            catch { /* ignore */ }
        }

        static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;

            var audioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            if (audioUtil == null) return;

            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            _play = audioUtil.GetMethod(
                "PlayPreviewClip",
                flags, null,
                new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
            _stop = audioUtil.GetMethod("StopAllPreviewClips", flags, null, Type.EmptyTypes, null);
        }
    }
}
