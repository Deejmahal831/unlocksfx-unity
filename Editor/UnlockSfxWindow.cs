using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnlockSfx
{
    /// <summary>
    /// The UnlockSFX editor panel: describe a sound, generate it, and have the
    /// clip imported straight into the project. Mirrors the Godot dock.
    /// </summary>
    public class UnlockSfxWindow : EditorWindow
    {
        static readonly string[] Categories =
        {
            "Weapons", "Magic", "Monsters", "Footsteps", "UI", "Impacts", "Ambience",
            "Sci-Fi", "Horror", "Doors / Chests", "Loot / Pickups", "Environment",
            "Vehicles", "Action"
        };
        static readonly string[] Formats = { "MP3", "WAV" };

        const string AccountUrl = "https://www.unlocksfx.com/app/settings";
        const string CreditsUrl = "https://www.unlocksfx.com/app/credits";
        const string LicenseUrl = "https://www.unlocksfx.com/license";

        string _apiKey = "";
        string _saveFolder = UnlockSfxSettings.DefaultSaveFolder;
        string _prompt = "";
        int _categoryIndex = 4; // UI
        float _duration = 2f;
        int _formatIndex = 0; // MP3
        bool _loop;

        bool _busy;
        string _status = "";
        bool _isError;
        int _credits = -1;
        Vector2 _scroll;

        [MenuItem("Window/UnlockSFX")]
        public static void Open()
        {
            ShowWindow();
        }

        static UnlockSfxWindow ShowWindow()
        {
            var window = GetWindow<UnlockSfxWindow>();
            window.titleContent = new GUIContent("UnlockSFX");
            window.minSize = new Vector2(320, 420);
            window.Show();
            return window;
        }

        void OnEnable()
        {
            _apiKey = UnlockSfxSettings.ApiKey;
            _saveFolder = UnlockSfxSettings.SaveFolder;
            if (!string.IsNullOrEmpty(_apiKey)) RefreshCredits();
        }

        public void ReloadSaveFolder()
        {
            _saveFolder = UnlockSfxSettings.SaveFolder;
            Repaint();
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(
                "Generate game-ready SFX from a text prompt.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(6);

            DrawAccount();
            EditorGUILayout.Space(8);
            DrawSound();
            EditorGUILayout.Space(6);
            DrawGenerate();
            DrawFooter();

            EditorGUILayout.EndScrollView();
        }

        void DrawAccount()
        {
            EditorGUILayout.LabelField("Account", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _apiKey = EditorGUILayout.PasswordField("API key", _apiKey);
            if (EditorGUI.EndChangeCheck())
            {
                UnlockSfxSettings.ApiKey = _apiKey;
                _credits = -1;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    _credits >= 0 ? ("Credits: " + _credits) : "Credits: —",
                    EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(_busy || string.IsNullOrEmpty(_apiKey)))
                {
                    if (GUILayout.Button("Refresh", EditorStyles.miniButton, GUILayout.Width(70)))
                        RefreshCredits();
                }
                if (GUILayout.Button("Get more", EditorStyles.miniButton, GUILayout.Width(70)))
                    Application.OpenURL(CreditsUrl);
            }

            if (string.IsNullOrEmpty(_apiKey))
            {
                EditorGUILayout.HelpBox(
                    "Create an API key at unlocksfx.com → Settings → API keys, then paste it above.",
                    MessageType.Info);
                if (GUILayout.Button("Open API key settings"))
                    Application.OpenURL(AccountUrl);
            }
        }

        void DrawSound()
        {
            EditorGUILayout.LabelField("Sound", EditorStyles.boldLabel);

            EditorGUILayout.LabelField("Describe the sound");
            _prompt = EditorGUILayout.TextArea(_prompt, GUILayout.MinHeight(56));

            _categoryIndex = EditorGUILayout.Popup("Category", _categoryIndex, Categories);

            _duration = EditorGUILayout.Slider("Duration (s)", _duration, 0.5f, 20f);
            _duration = Mathf.Round(_duration * 10f) / 10f;

            EditorGUILayout.LabelField("Format");
            _formatIndex = GUILayout.Toolbar(_formatIndex, Formats);

            _loop = EditorGUILayout.ToggleLeft("Seamless loop", _loop);

            EditorGUI.BeginChangeCheck();
            _saveFolder = EditorGUILayout.TextField("Save folder", _saveFolder);
            if (EditorGUI.EndChangeCheck())
                UnlockSfxSettings.SaveFolder = _saveFolder;
        }

        void DrawGenerate()
        {
            int cost = Mathf.Max(1, Mathf.CeilToInt(_duration / 5f));
            using (new EditorGUI.DisabledScope(_busy))
            {
                var label = _busy
                    ? "Generating…"
                    : "Generate sound (" + cost + " credit" + (cost == 1 ? "" : "s") + ")";
                if (GUILayout.Button(label, GUILayout.Height(32)))
                    Generate();
            }

            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.HelpBox(_status, _isError ? MessageType.Error : MessageType.Info);
            }
        }

        void DrawFooter()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(
                "Every clip is royalty-free, cleared for commercial use, no attribution.",
                EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("View license", EditorStyles.miniButton, GUILayout.Width(90)))
                Application.OpenURL(LicenseUrl);
        }

        void RefreshCredits()
        {
            UnlockSfxClient.FetchCredits(
                credits => { _credits = credits; Repaint(); },
                _ => Repaint());
        }

        void Generate()
        {
            if (_busy) return;
            if (string.IsNullOrEmpty(UnlockSfxSettings.ApiKey))
            {
                SetStatus("Add your API key first.", true);
                return;
            }
            if (_prompt == null || _prompt.Trim().Length < 3)
            {
                SetStatus("Describe the sound in at least 3 characters.", true);
                return;
            }

            _busy = true;
            SetStatus("Generating…", false);

            var request = new GenerateRequest
            {
                originalPrompt = _prompt.Trim(),
                category = Categories[Mathf.Clamp(_categoryIndex, 0, Categories.Length - 1)],
                durationSeconds = _duration,
                loopable = _loop,
                outputFormat = _formatIndex == 1 ? "wav" : "mp3",
                promptInfluence = 0.3f
            };

            UnlockSfxClient.GenerateSound(
                request,
                (meta, bytes) =>
                {
                    _busy = false;
                    try
                    {
                        var path = SaveClip(meta, bytes);
                        _credits = meta.creditsRemaining;
                        SetStatus(
                            "Saved " + Path.GetFileName(path) + "  ·  " + meta.creditsRemaining + " credits left",
                            false);
                    }
                    catch (Exception e)
                    {
                        SetStatus("Generated, but couldn't import it: " + e.Message, true);
                    }
                    Repaint();
                },
                error =>
                {
                    _busy = false;
                    SetStatus(error, true);
                    Repaint();
                });
        }

        string SaveClip(GenerateResponse meta, byte[] bytes)
        {
            var folder = string.IsNullOrEmpty(_saveFolder) ? UnlockSfxSettings.DefaultSaveFolder : _saveFolder;
            EnsureFolder(folder);

            var ext = (meta.format == "wav") ? "wav" : "mp3";
            var baseName = Slug(string.IsNullOrEmpty(meta.title) ? _prompt : meta.title);
            var relative = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + baseName + "." + ext);

            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var absolute = Path.Combine(projectRoot, relative);
            File.WriteAllBytes(absolute, bytes);

            AssetDatabase.ImportAsset(relative, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(relative);
            if (clip != null)
            {
                Selection.activeObject = clip;
                EditorGUIUtility.PingObject(clip);
            }
            return relative;
        }

        static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;

            var parts = folder.Replace('\\', '/').Split('/');
            if (parts.Length == 0 || parts[0] != "Assets") return;

            var current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i])) continue;
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        static string Slug(string source)
        {
            if (string.IsNullOrEmpty(source)) return "sfx";

            var sb = new StringBuilder();
            foreach (var c in source.ToLowerInvariant().Trim())
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
                else if (c == ' ' || c == '-' || c == '_') sb.Append('_');
            }

            var slug = sb.ToString().Trim('_');
            while (slug.Contains("__")) slug = slug.Replace("__", "_");
            if (slug.Length > 40) slug = slug.Substring(0, 40).Trim('_');
            return string.IsNullOrEmpty(slug) ? "sfx" : slug;
        }

        void SetStatus(string message, bool isError)
        {
            _status = message;
            _isError = isError;
        }

        // Right-click a folder in the Project window → generate straight into it.
        [MenuItem("Assets/UnlockSFX: Generate here…", false, 20)]
        static void GenerateHere()
        {
            var folder = GetSelectedFolder();
            if (!string.IsNullOrEmpty(folder)) UnlockSfxSettings.SaveFolder = folder;
            var window = ShowWindow();
            window.ReloadSaveFolder();
        }

        [MenuItem("Assets/UnlockSFX: Generate here…", true)]
        static bool GenerateHereValidate()
        {
            return GetSelectedFolder() != null;
        }

        static string GetSelectedFolder()
        {
            var obj = Selection.activeObject;
            if (obj == null) return null;

            var path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) return null;
            if (AssetDatabase.IsValidFolder(path)) return path;

            var parent = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(parent)) return null;
            parent = parent.Replace('\\', '/');
            return parent.StartsWith("Assets") ? parent : null;
        }
    }
}
