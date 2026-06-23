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

        public const string DefaultSaveFolder = "Assets/SFX";

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
