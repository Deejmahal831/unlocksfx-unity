using UnityEditor;

namespace UnlockSfx
{
    /// <summary>
    /// Editor-persisted settings. The API key and save folder are remembered
    /// between sessions via EditorPrefs (per-machine, like the Godot plugin).
    /// </summary>
    internal static class UnlockSfxSettings
    {
        const string ApiKeyPref = "UnlockSFX.ApiKey";
        const string SaveFolderPref = "UnlockSFX.SaveFolder";
        const string LightThemePref = "UnlockSFX.LightTheme";

        public const string DefaultSaveFolder = "Assets/SFX";

        /// <summary>True once the user has explicitly chosen a theme.</summary>
        public static bool HasTheme => EditorPrefs.HasKey(LightThemePref);

        /// <summary>Light vs dark panel theme (defaults to dark until the user picks).</summary>
        public static bool LightTheme
        {
            get { return EditorPrefs.GetBool(LightThemePref, false); }
            set { EditorPrefs.SetBool(LightThemePref, value); }
        }

        public static string ApiKey
        {
            get { return EditorPrefs.GetString(ApiKeyPref, ""); }
            set { EditorPrefs.SetString(ApiKeyPref, value ?? ""); }
        }

        public static string SaveFolder
        {
            get { return EditorPrefs.GetString(SaveFolderPref, DefaultSaveFolder); }
            set
            {
                EditorPrefs.SetString(
                    SaveFolderPref,
                    string.IsNullOrEmpty(value) ? DefaultSaveFolder : value);
            }
        }
    }
}
