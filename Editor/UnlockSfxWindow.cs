using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnlockSfx
{
    /// <summary>
    /// The UnlockSFX editor panel, built with UIToolkit so it matches the website:
    /// deep-indigo cards, rounded teal CTA, purple focus accents. Describe a sound,
    /// generate it (or a variation bank), and have the clip(s) imported straight into
    /// the project. The look lives in UnlockSfxWindow.uss; all the networking, import,
    /// preview, bank, and provenance logic is shared with the rest of the package.
    /// </summary>
    public class UnlockSfxWindow : EditorWindow
    {
        const string AccountUrl = "https://www.unlocksfx.com/app/settings?ref=unity";
        const string CreditsUrl = "https://www.unlocksfx.com/app/credits?ref=unity";

        // --- persisted/edited state ---
        string _apiKey = "";
        string _saveFolder = UnlockSfxSettings.DefaultSaveFolder;
        string _prompt = "";
        float _duration = 2f;
        bool _isWav;
        bool _loop;
        int _variants = 1;

        bool _busy;
        int _credits = -1;
        bool _connected;
        bool _editingKey;
        bool _light;

        AudioClip _lastClip;
        bool _lastLoop;
        bool _showLicense;
        string _licenseSubtitle = "";

        // --- element refs ---
        VisualElement _disconnectedBox, _connectedBox;
        TextField _keyField, _promptField, _folderField;
        Label _keyHint, _creditsNum;
        Button _openSettingsBtn;
        SliderInt _variantsSlider;
        Slider _durationSlider;
        Label _variantsValue, _durationValue;
        Button _mp3Btn, _wavBtn;
        VisualElement _loopBox;
        Button _generateBtn;
        Label _bankNote, _statusBox;
        Button _listenBtn;
        VisualElement _licenseCard;
        Label _licenseSub;
        Button _themeBtn;

        [MenuItem("Window/UnlockSFX")]
        public static void Open()
        {
            ShowWindow();
        }

        static UnlockSfxWindow ShowWindow()
        {
            var window = GetWindow<UnlockSfxWindow>();
            window.titleContent = new GUIContent("UnlockSFX");
            window.minSize = new Vector2(320, 480);
            window.Show();
            return window;
        }

        public void CreateGUI()
        {
            _apiKey = UnlockSfxSettings.ApiKey;
            _saveFolder = UnlockSfxSettings.SaveFolder;
            _editingKey = string.IsNullOrEmpty(_apiKey);
            // Default to the editor's own skin until the user picks a theme.
            _light = UnlockSfxSettings.HasTheme
                ? UnlockSfxSettings.LightTheme
                : !EditorGUIUtility.isProSkin;

            var root = rootVisualElement;
            root.AddToClassList("usfx-root");
            var sheet = LoadStyleSheet();
            if (sheet != null) root.styleSheets.Add(sheet);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("usfx-scroll");
            root.Add(scroll);

            var header = Row();
            header.Add(MakeLabel("UnlockSFX", "usfx-title"));
            header.Add(Spacer());
            _themeBtn = MakeButton(_light ? "Light" : "Dark", "usfx-btn-ghost usfx-theme-btn", ToggleTheme);
            header.Add(_themeBtn);
            scroll.Add(header);
            scroll.Add(MakeLabel("Generate game-ready SFX from a text prompt.", "usfx-subtitle"));

            BuildAccount(scroll);
            BuildSound(scroll);
            BuildGenerate(scroll);
            BuildResult(scroll);

            scroll.Add(MakeLabel(
                "Every clip is royalty-free, cleared for commercial use, no attribution.",
                "usfx-subtitle"));

            SetFormat(_isWav);
            ApplyTheme();
            RefreshUi();
            if (!string.IsNullOrEmpty(_apiKey)) RefreshCredits();
        }

        void ToggleTheme()
        {
            _light = !_light;
            UnlockSfxSettings.LightTheme = _light;
            ApplyTheme();
        }

        void ApplyTheme()
        {
            rootVisualElement.EnableInClassList("usfx-light", _light);
            if (_themeBtn != null) _themeBtn.text = _light ? "Light" : "Dark";
        }

        public void ReloadSaveFolder()
        {
            _saveFolder = UnlockSfxSettings.SaveFolder;
            if (_folderField != null) _folderField.SetValueWithoutNotify(_saveFolder);
        }

        // --- build: account ---

        void BuildAccount(VisualElement parent)
        {
            var card = Card();
            parent.Add(card);
            card.Add(MakeLabel("Account", "usfx-section"));

            // (A) disconnected — paste the key
            _disconnectedBox = new VisualElement();
            card.Add(_disconnectedBox);
            _disconnectedBox.Add(MakeLabel("API key", "usfx-label"));
            _keyField = new TextField { isPasswordField = true, value = _apiKey };
            _keyField.RegisterValueChangedCallback(evt => OnKeyChanged(evt.newValue));
            _disconnectedBox.Add(_keyField);
            _keyHint = MakeLabel("", "usfx-hint");
            _disconnectedBox.Add(_keyHint);
            _openSettingsBtn = MakeButton("Open API key settings", "usfx-btn-ghost",
                () => Application.OpenURL(AccountUrl));
            _openSettingsBtn.style.marginTop = 6;
            _disconnectedBox.Add(_openSettingsBtn);

            // (B) connected — status + credits
            _connectedBox = new VisualElement();
            card.Add(_connectedBox);

            var krow = Row();
            krow.Add(MakeLabel("API key", "usfx-label"));
            var change = MakeButton("change", "usfx-btn-ghost", () => { _editingKey = true; RefreshUi(); });
            change.style.marginLeft = 8;
            krow.Add(change);
            krow.Add(Spacer());
            krow.Add(MakeLabel("✓ Connected", "usfx-badge"));
            _connectedBox.Add(krow);

            _connectedBox.Add(Rule());

            var crow = Row();
            _creditsNum = MakeLabel("—", "usfx-credits-num");
            crow.Add(_creditsNum);
            crow.Add(MakeLabel("credits", "usfx-credits-word"));
            crow.Add(Spacer());
            var refresh = MakeButton("Refresh", "usfx-btn-ghost", RefreshCredits);
            refresh.style.marginRight = 6;
            crow.Add(refresh);
            crow.Add(MakeButton("Get more", "usfx-btn-ghost", () => Application.OpenURL(CreditsUrl)));
            _connectedBox.Add(crow);
        }

        void OnKeyChanged(string value)
        {
            _apiKey = value;
            UnlockSfxSettings.ApiKey = _apiKey;
            _credits = -1;
            _connected = false;

            var key = (_apiKey ?? "").Trim();
            if (key.StartsWith("usfx_") && key.Length > 24) RefreshCredits();
            RefreshUi();
        }

        // --- build: sound ---

        void BuildSound(VisualElement parent)
        {
            var card = Card();
            parent.Add(card);
            card.Add(MakeLabel("Sound", "usfx-section"));

            card.Add(MakeLabel("Describe the sound", "usfx-label"));
            _promptField = new TextField { multiline = true, value = _prompt };
            _promptField.AddToClassList("usfx-prompt");
            _promptField.RegisterValueChangedCallback(evt => _prompt = evt.newValue);
            card.Add(_promptField);

            // Variants
            var vrow = Row();
            vrow.style.marginTop = 10;
            vrow.Add(RowLabel("Variants"));
            _variantsSlider = new SliderInt(1, 8) { value = _variants };
            _variantsSlider.style.flexGrow = 1;
            _variantsSlider.style.marginLeft = 8;
            _variantsSlider.style.marginRight = 8;
            _variantsSlider.RegisterValueChangedCallback(evt =>
            {
                _variants = evt.newValue;
                _variantsValue.text = _variants.ToString();
                RefreshGenerate();
            });
            vrow.Add(_variantsSlider);
            _variantsValue = MakeLabel(_variants.ToString(), "usfx-value");
            vrow.Add(_variantsValue);
            card.Add(vrow);

            // Duration
            var drow = Row();
            drow.style.marginTop = 6;
            drow.Add(RowLabel("Duration"));
            _durationSlider = new Slider(0.5f, 20f) { value = _duration };
            _durationSlider.style.flexGrow = 1;
            _durationSlider.style.marginLeft = 8;
            _durationSlider.style.marginRight = 8;
            _durationSlider.RegisterValueChangedCallback(evt =>
            {
                _duration = Mathf.Round(evt.newValue * 10f) / 10f;
                _durationValue.text = DurationText();
                RefreshGenerate();
            });
            drow.Add(_durationSlider);
            _durationValue = MakeLabel(DurationText(), "usfx-value");
            drow.Add(_durationValue);
            card.Add(drow);

            // Format
            var fmtLabel = MakeLabel("Format", "usfx-label");
            fmtLabel.style.marginTop = 8;
            card.Add(fmtLabel);
            var seg = new VisualElement();
            seg.AddToClassList("usfx-seg");
            _mp3Btn = MakeButton("MP3", "usfx-seg-btn", () => SetFormat(false));
            _mp3Btn.AddToClassList("usfx-seg-btn--first");
            _wavBtn = MakeButton("WAV", "usfx-seg-btn", () => SetFormat(true));
            _wavBtn.AddToClassList("usfx-seg-btn--last");
            seg.Add(_mp3Btn);
            seg.Add(_wavBtn);
            card.Add(seg);

            // Loop — custom toggle so the check mark is brand-teal on a dark box.
            var loopRow = Row();
            loopRow.AddToClassList("usfx-toggle-row");
            loopRow.style.marginTop = 8;
            _loopBox = new VisualElement();
            _loopBox.AddToClassList("usfx-check");
            _loopBox.Add(MakeLabel("✓", "usfx-check-mark"));
            loopRow.Add(_loopBox);
            loopRow.Add(MakeLabel("Seamless loop", "usfx-toggle-label"));
            loopRow.RegisterCallback<ClickEvent>(_ => SetLoop(!_loop));
            card.Add(loopRow);
            SetLoop(_loop);

            // Save folder
            var folderLabel = MakeLabel("Save folder", "usfx-label");
            folderLabel.style.marginTop = 8;
            card.Add(folderLabel);
            var frow = Row();
            _folderField = new TextField { value = _saveFolder };
            _folderField.style.flexGrow = 1;
            _folderField.RegisterValueChangedCallback(evt =>
            {
                _saveFolder = evt.newValue;
                UnlockSfxSettings.SaveFolder = _saveFolder;
            });
            frow.Add(_folderField);
            var browse = MakeButton("Browse…", "usfx-btn-ghost", BrowseFolder);
            browse.style.marginLeft = 6;
            frow.Add(browse);
            card.Add(frow);
        }

        void SetFormat(bool wav)
        {
            _isWav = wav;
            _mp3Btn.EnableInClassList("usfx-seg-btn--active", !wav);
            _wavBtn.EnableInClassList("usfx-seg-btn--active", wav);
        }

        void SetLoop(bool on)
        {
            _loop = on;
            _loopBox.EnableInClassList("usfx-check--on", on);
        }

        // --- build: generate + result ---

        void BuildGenerate(VisualElement parent)
        {
            _generateBtn = new Button(OnGenerateClicked) { text = "Generate sound" };
            _generateBtn.AddToClassList("usfx-btn-primary");
            parent.Add(_generateBtn);

            _bankNote = MakeLabel(
                "Creates a prefab with an UnlockSFX Random Player — drop it in a scene and call Play().",
                "usfx-note");
            parent.Add(_bankNote);

            _statusBox = MakeLabel("", "usfx-status");
            _statusBox.style.display = DisplayStyle.None;
            parent.Add(_statusBox);
        }

        void BuildResult(VisualElement parent)
        {
            _listenBtn = MakeButton("Listen", "usfx-btn-ghost", () =>
            {
                if (_lastClip != null) UnlockSfxPreview.Play(_lastClip, _lastLoop);
            });
            _listenBtn.style.marginTop = 8;
            _listenBtn.SetEnabled(false);
            parent.Add(_listenBtn);

            _licenseCard = Card();
            _licenseCard.style.marginTop = 10;
            _licenseCard.style.display = DisplayStyle.None;
            parent.Add(_licenseCard);

            _licenseCard.Add(MakeLabel("Cleared to ship", "usfx-section"));
            _licenseSub = MakeLabel("", "usfx-subtitle");
            _licenseCard.Add(_licenseSub);

            var pills = new VisualElement();
            pills.AddToClassList("usfx-pills");
            pills.Add(MakeLabel("✓ Commercial", "usfx-pill usfx-pill-teal"));
            pills.Add(MakeLabel("✓ Royalty-free", "usfx-pill usfx-pill-teal"));
            pills.Add(MakeLabel("✓ No attribution", "usfx-pill usfx-pill-teal"));
            pills.Add(MakeLabel("AI-generated", "usfx-pill usfx-pill-amber"));
            pills.Add(MakeLabel("✕ Don't resell raw files", "usfx-pill usfx-pill-pink"));
            _licenseCard.Add(pills);

            var brow = Row();
            var copy = MakeButton("Copy Steam AI disclosure", "usfx-btn-ghost", () =>
            {
                EditorGUIUtility.systemCopyBuffer = UnlockSfxProvenance.DisclosureText;
                SetStatus("Steam AI disclosure copied to clipboard.", false);
            });
            copy.style.flexGrow = 1;
            brow.Add(copy);
            var view = MakeButton("View license", "usfx-btn-ghost",
                () => Application.OpenURL(UnlockSfxProvenance.LicenseUrl + "?ref=unity"));
            view.style.marginLeft = 6;
            brow.Add(view);
            _licenseCard.Add(brow);
        }

        // --- refresh ---

        void RefreshUi()
        {
            bool showField = _editingKey || !_connected;
            _disconnectedBox.style.display = showField ? DisplayStyle.Flex : DisplayStyle.None;
            _connectedBox.style.display = showField ? DisplayStyle.None : DisplayStyle.Flex;

            bool noKey = string.IsNullOrEmpty(_apiKey);
            _openSettingsBtn.style.display = noKey ? DisplayStyle.Flex : DisplayStyle.None;
            if (noKey)
                _keyHint.text = "Create an API key at unlocksfx.com → Settings → API keys, then paste it above.";
            else if (!_connected && _keyHint.text == "")
                _keyHint.text = "Checking…";

            _creditsNum.text = _credits >= 0 ? _credits.ToString() : "—";

            _listenBtn.SetEnabled(_lastClip != null);
            _listenBtn.text = _lastClip == null ? "Listen" : "▶ Listen  " + _lastClip.name;

            _licenseCard.style.display = _showLicense ? DisplayStyle.Flex : DisplayStyle.None;
            _licenseSub.text = _licenseSubtitle;

            RefreshGenerate();
        }

        void RefreshGenerate()
        {
            int variants = Mathf.Clamp(_variants, 1, 8);
            int cost = CostFor(variants);
            bool insufficient = _credits >= 0 && cost > _credits;

            _generateBtn.RemoveFromClassList("usfx-btn-primary");
            _generateBtn.RemoveFromClassList("usfx-btn-warn");

            if (_busy)
            {
                _generateBtn.text = "Generating…";
                _generateBtn.AddToClassList("usfx-btn-primary");
                _generateBtn.SetEnabled(false);
            }
            else
            {
                _generateBtn.SetEnabled(true);
                if (insufficient)
                {
                    _generateBtn.text = "Need " + (cost - _credits) + " more credits · Get more";
                    _generateBtn.AddToClassList("usfx-btn-warn");
                }
                else
                {
                    _generateBtn.AddToClassList("usfx-btn-primary");
                    _generateBtn.text = variants <= 1
                        ? "Generate sound · " + cost + " credit" + (cost == 1 ? "" : "s")
                        : "Generate bank · " + variants + " variants · " + cost + " credits";
                }
            }

            _bankNote.style.display = variants > 1 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void SetStatus(string message, bool isError)
        {
            if (_statusBox == null) return;
            if (string.IsNullOrEmpty(message))
            {
                _statusBox.style.display = DisplayStyle.None;
                return;
            }
            _statusBox.text = message;
            _statusBox.RemoveFromClassList("usfx-status-info");
            _statusBox.RemoveFromClassList("usfx-status-error");
            _statusBox.AddToClassList(isError ? "usfx-status-error" : "usfx-status-info");
            _statusBox.style.display = DisplayStyle.Flex;
        }

        // --- actions ---

        void RefreshCredits()
        {
            UnlockSfxClient.FetchCredits(
                credits => { _credits = credits; _connected = true; _editingKey = false; RefreshUi(); },
                _ =>
                {
                    _connected = false;
                    if (!string.IsNullOrEmpty(_apiKey))
                        _keyHint.text = "Couldn't verify that key. Check it and try again.";
                    RefreshUi();
                });
        }

        bool IsInsufficient()
        {
            int cost = CostFor(Mathf.Clamp(_variants, 1, 8));
            return _credits >= 0 && cost > _credits;
        }

        void OnGenerateClicked()
        {
            if (IsInsufficient()) { Application.OpenURL(CreditsUrl); return; }
            Generate();
        }

        void BrowseFolder()
        {
            var start = AssetDatabase.IsValidFolder(_saveFolder)
                ? Path.Combine(Directory.GetParent(Application.dataPath).FullName, _saveFolder)
                : Application.dataPath;
            var chosen = EditorUtility.OpenFolderPanel("Choose a save folder (inside Assets)", start, "");
            if (string.IsNullOrEmpty(chosen)) return;

            var relative = ToAssetsRelative(chosen);
            if (relative == null)
            {
                SetStatus("Pick a folder inside this project's Assets/ folder.", true);
                return;
            }
            _saveFolder = relative;
            UnlockSfxSettings.SaveFolder = _saveFolder;
            _folderField.SetValueWithoutNotify(_saveFolder);
        }

        static string ToAssetsRelative(string absolute)
        {
            var data = Application.dataPath.Replace('\\', '/');
            var p = absolute.Replace('\\', '/');
            if (p == data) return "Assets";
            if (p.StartsWith(data + "/")) return "Assets" + p.Substring(data.Length);
            return null;
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

            int variants = Mathf.Clamp(_variants, 1, 8);
            _busy = true;
            _showLicense = false;
            _lastLoop = _loop;
            SetStatus("Generating…", false);
            RefreshUi();

            if (variants <= 1) GenerateSingle();
            else GenerateBank(variants);
        }

        void GenerateSingle()
        {
            var request = new GenerateRequest
            {
                originalPrompt = _prompt.Trim(),
                durationSeconds = _duration,
                loopable = _loop,
                outputFormat = CurrentFormat(),
                promptInfluence = 0.3f
            };

            UnlockSfxClient.GenerateSound(
                request,
                (meta, bytes) =>
                {
                    _busy = false;
                    try
                    {
                        var relative = SaveClip(meta.title, meta.format, bytes);
                        _credits = meta.creditsRemaining;
                        _lastClip = AssetDatabase.LoadAssetAtPath<AudioClip>(relative);
                        ShowLicense(Path.GetFileName(relative));
                        SetStatus("Saved " + Path.GetFileName(relative) + "  ·  " +
                                  meta.creditsRemaining + " credits left", false);
                    }
                    catch (Exception e)
                    {
                        SetStatus("Generated, but couldn't import it: " + e.Message, true);
                    }
                    RefreshUi();
                },
                error => { _busy = false; SetStatus(error, true); RefreshUi(); });
        }

        void GenerateBank(int variants)
        {
            var request = new BankRequest
            {
                originalPrompt = _prompt.Trim(),
                durationSeconds = _duration,
                loopable = _loop,
                outputFormat = CurrentFormat(),
                promptInfluence = 0.3f,
                count = variants
            };

            UnlockSfxClient.GenerateBank(
                request,
                (meta, clipBytes) =>
                {
                    _busy = false;
                    try
                    {
                        SaveBank(meta, clipBytes);
                        _credits = meta.creditsRemaining;
                    }
                    catch (Exception e)
                    {
                        SetStatus("Generated, but couldn't import the bank: " + e.Message, true);
                    }
                    RefreshUi();
                },
                error => { _busy = false; SetStatus(error, true); RefreshUi(); });
        }

        // --- saving ---

        string SaveClip(string title, string format, byte[] bytes)
        {
            var folder = EnsuredFolder(_saveFolder);
            var ext = (format == "wav") ? "wav" : "mp3";
            var baseName = Slug(string.IsNullOrEmpty(title) ? _prompt : title);
            var relative = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + baseName + "." + ext);

            var absolute = ToAbsolute(relative);
            File.WriteAllBytes(absolute, bytes);
            UnlockSfxProvenance.WriteSidecar(absolute, _prompt.Trim(), _duration, _loop);

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

        void SaveBank(BankResponse meta, byte[][] clipBytes)
        {
            var parent = EnsuredFolder(_saveFolder);
            var bankName = Slug(string.IsNullOrEmpty(meta.bankName) ? _prompt : meta.bankName);
            var bankFolder = parent + "/" + bankName;
            if (!AssetDatabase.IsValidFolder(bankFolder))
                AssetDatabase.CreateFolder(parent, bankName);

            var imported = new List<AudioClip>();
            int index = 1;
            for (int i = 0; i < meta.clips.Length; i++)
            {
                var bytes = clipBytes[i];
                if (bytes == null || bytes.Length == 0) continue;

                var ext = (meta.clips[i].format == "wav") ? "wav" : "mp3";
                var name = string.Format("{0}_{1:00}.{2}", bankName, index, ext);
                var relative = AssetDatabase.GenerateUniqueAssetPath(bankFolder + "/" + name);
                var absolute = ToAbsolute(relative);
                File.WriteAllBytes(absolute, bytes);
                UnlockSfxProvenance.WriteSidecar(absolute, _prompt.Trim(), _duration, _loop);
                AssetDatabase.ImportAsset(relative, ImportAssetOptions.ForceUpdate);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(relative);
                if (clip != null) imported.Add(clip);
                index++;
            }

            AssetDatabase.Refresh();

            if (imported.Count == 0)
            {
                SetStatus("The bank returned no usable clips.", true);
                return;
            }

            var prefabPath = AssetDatabase.GenerateUniqueAssetPath(bankFolder + "/" + bankName + ".prefab");
            var prefab = UnlockSfxBank.BuildPrefab(prefabPath, bankName, imported);

            _lastClip = imported[0];
            ShowLicense(bankName + " · " + imported.Count + " variants");

            if (prefab != null)
            {
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
                SetStatus("Saved " + imported.Count + " variants + " + bankName +
                          ".prefab — drop it in a scene and call Play()  ·  " +
                          meta.creditsRemaining + " credits left", false);
            }
            else
            {
                SetStatus("Saved " + imported.Count + " variants to " + bankFolder, false);
            }
        }

        // --- helpers ---

        string DurationText()
        {
            return _duration.ToString("0.0", CultureInfo.InvariantCulture) + "s";
        }

        string CurrentFormat()
        {
            return _isWav ? "wav" : "mp3";
        }

        int CostFor(int variants)
        {
            int per = Mathf.Max(1, Mathf.CeilToInt(_duration / 5f));
            if (variants <= 1) return per;
            return Mathf.CeilToInt(variants * per * 0.75f);
        }

        string EnsuredFolder(string folder)
        {
            var target = string.IsNullOrEmpty(folder) ? UnlockSfxSettings.DefaultSaveFolder : folder;
            EnsureFolder(target);
            return target;
        }

        static string ToAbsolute(string assetsRelative)
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetsRelative);
        }

        void ShowLicense(string subtitle)
        {
            _showLicense = true;
            _licenseSubtitle = subtitle;
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

        // --- UIToolkit element factories ---

        static StyleSheet LoadStyleSheet()
        {
            var asm = typeof(UnlockSfxWindow).Assembly;
            var pkg = UnityEditor.PackageManager.PackageInfo.FindForAssembly(asm);
            if (pkg != null)
            {
                var atPath = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                    pkg.assetPath + "/Editor/UnlockSfxWindow.uss");
                if (atPath != null) return atPath;
            }
            foreach (var guid in AssetDatabase.FindAssets("UnlockSfxWindow t:StyleSheet"))
            {
                var ss = AssetDatabase.LoadAssetAtPath<StyleSheet>(AssetDatabase.GUIDToAssetPath(guid));
                if (ss != null) return ss;
            }
            return null;
        }

        static Label MakeLabel(string text, string classes)
        {
            var label = new Label(text);
            AddClasses(label, classes);
            return label;
        }

        static Label RowLabel(string text)
        {
            var label = MakeLabel(text, "usfx-label");
            label.style.width = 62;
            label.style.marginBottom = 0;
            return label;
        }

        static Button MakeButton(string text, string classes, Action onClick)
        {
            var button = new Button(() => onClick()) { text = text };
            AddClasses(button, classes);
            return button;
        }

        static VisualElement Row()
        {
            var ve = new VisualElement();
            ve.AddToClassList("usfx-row");
            return ve;
        }

        static VisualElement Spacer()
        {
            var ve = new VisualElement();
            ve.AddToClassList("usfx-spacer");
            return ve;
        }

        static VisualElement Card()
        {
            var ve = new VisualElement();
            ve.AddToClassList("usfx-card");
            return ve;
        }

        static VisualElement Rule()
        {
            var ve = new VisualElement();
            ve.AddToClassList("usfx-rule");
            return ve;
        }

        static void AddClasses(VisualElement element, string classes)
        {
            if (string.IsNullOrEmpty(classes)) return;
            foreach (var cls in classes.Split(' '))
                if (!string.IsNullOrEmpty(cls)) element.AddToClassList(cls);
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
