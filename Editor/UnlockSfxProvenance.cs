using System.Globalization;
using System.IO;
using System.Text;

namespace UnlockSfx
{
    /// <summary>
    /// Writes a hidden provenance sidecar next to each generated clip — the same
    /// record the Godot plugin writes. It travels with the file as the AI-disclosure
    /// source of truth (and, later, the re-roll source). The leading dot keeps Unity
    /// from importing or listing it as an asset.
    /// </summary>
    internal static class UnlockSfxProvenance
    {
        public const string LicenseUrl = "https://www.unlocksfx.com/license";

        // Kept in sync with the Godot plugin so disclosures read identically.
        public const string DisclosureText =
            "Some sound effects in this game were generated with AI (UnlockSFX, " +
            "powered by ElevenLabs) and are licensed for commercial use.";

        /// <summary>
        /// Write `.&lt;name&gt;.unlocksfx.json` beside the clip. `absoluteAudioPath`
        /// is a real filesystem path (not an Assets-relative one).
        /// </summary>
        public static void WriteSidecar(
            string absoluteAudioPath, string prompt,
            float durationSeconds, bool loopable)
        {
            var dir = Path.GetDirectoryName(absoluteAudioPath);
            if (string.IsNullOrEmpty(dir)) return;

            var baseName = Path.GetFileNameWithoutExtension(absoluteAudioPath);
            var ext = Path.GetExtension(absoluteAudioPath).TrimStart('.');
            var sidecar = Path.Combine(dir, "." + baseName + ".unlocksfx.json");

            // Small, fixed shape — JsonUtility can't serialize anonymous types, so
            // we build the JSON by hand (escaping the only free-text fields).
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("\t\"tool\": \"UnlockSFX\",\n");
            sb.Append("\t\"prompt\": \"").Append(Escape(prompt)).Append("\",\n");
            sb.Append("\t\"ai_generated\": true,\n");
            sb.Append("\t\"provider\": \"elevenlabs\",\n");
            sb.Append("\t\"license\": \"commercial-royalty-free-no-attribution-no-resale\",\n");
            sb.Append("\t\"license_url\": \"").Append(LicenseUrl).Append("\",\n");
            sb.Append("\t\"duration_seconds\": ")
              .Append(durationSeconds.ToString("0.0", CultureInfo.InvariantCulture)).Append(",\n");
            sb.Append("\t\"loopable\": ").Append(loopable ? "true" : "false").Append(",\n");
            sb.Append("\t\"format\": \"").Append(Escape(ext)).Append("\"\n");
            sb.Append("}\n");

            File.WriteAllText(sidecar, sb.ToString());
        }

        static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                    .Replace("\n", "\\n").Replace("\r", "").Replace("\t", " ");
        }
    }
}
