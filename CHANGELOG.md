# Changelog

## [0.2.0] - 2026-06-24

Feature parity with the Godot plugin. Supports **Unity 2021.3 LTS** and newer.

- **Variation banks** — set Variants 2–8 to generate a bank: N clips in a
  subfolder plus a `.prefab` carrying an **UnlockSFX Random Player** component
  (clips pre-assigned, no-repeat + pitch variance). Drop it in a scene and call
  `Play()` for instant non-repeating playback.
- **Listen** — preview the generated clip in-editor without entering Play mode.
- **Cleared-to-ship card** — rights at a glance + one-click **Copy Steam AI
  disclosure**. Each clip also gets a hidden `.<name>.unlocksfx.json` provenance
  sidecar (prompt, license, AI-generated).
- **Connected/disconnected account states** — keys auto-verify on paste and show a
  ✓ Connected badge; "change" reveals the field again.
- **Smart Generate button** — switches to "Need X more credits · Get more" when the
  balance is short, instead of erroring after the click.
- **Browse…** folder picker for the save location.

## [0.1.0] - 2026-06-22

Initial release.

- Editor window (**Window → UnlockSFX**): describe a sound, pick category,
  duration, and format (MP3/WAV), and generate it into your project as an
  `AudioClip`.
- Right-click a Project folder → **UnlockSFX: Generate here…**.
- API key stored in EditorPrefs; live credit balance.
- Calls the UnlockSFX public API (`/api/v1`); royalty-free, cleared-to-ship audio.
