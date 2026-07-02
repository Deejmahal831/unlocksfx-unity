# UnlockSFX — Unity Package Manifesto

A plain-English brief for AI teammates (support, marketing, developer) working on
this repo. Read this before touching code, answering a user, or writing copy.

## What this is

A **Unity Editor plugin** that generates game-ready sound effects from a text
prompt, right inside Unity. You describe a sound; a clean, imported `AudioClip`
drops straight into your project — no asset hunting, no leaving the editor. It is
the Unity sibling of the UnlockSFX Godot plugin, and it deliberately keeps
**feature parity** with it (banks, seamless loops, provenance, disclosures).

This is a client, not the engine. The plugin never talks to the audio provider
(ElevenLabs) directly — it calls the UnlockSFX public API at
`https://www.unlocksfx.com/api/v1` with the user's API key. The server handles
credits, rate limits, storage, and licensing.

## Product & audience

- **Who:** Unity game developers who need SFX fast without hiring or hunting.
- **What they do:** Open **Window → UnlockSFX**, paste an API key, describe a
  sound, pick category / duration / format (MP3 or WAV), and **Generate**. Or
  right-click a Project folder → **UnlockSFX: Generate here…**.
- **Single clip** = one `AudioClip`. **Variants 2–8** = a variation bank: N clips
  in a subfolder plus a `.prefab` carrying the **UnlockSFX Random Player**
  component (no-repeat + pitch variance) — drop it in a scene and call `Play()`.
- **Loop** makes a seamless clip; **Listen** previews without entering Play mode.
- **Cleared to ship:** every clip gets a hidden `.<name>.unlocksfx.json`
  provenance sidecar, plus a one-click **Copy Steam AI disclosure** button.
- **Requires** a free UnlockSFX account + credits (1 credit per 5s; banks ~25%
  off and only charge for clips that succeed). Free credits to start.

## Tech stack & conventions

- **Language:** C#. **Target:** Unity **2021.3 LTS**+ (compile-verified on
  2021.3.45f2 and 6000.5). Distributed as a UPM package (`com.unlocksfx.sfx`).
- **Layout:** `Editor/` (the tool — window, client, settings, bank, preview,
  provenance) and `Runtime/` (only the `UnlockSfxRandomPlayer` component that must
  work in builds). Two asmdefs: `UnlockSFX.Editor` and `UnlockSFX.Runtime`.
- **Namespace:** `UnlockSfx` (root namespace on both asmdefs).
- **UI:** UIToolkit with a `.uss` stylesheet (light/dark theme). Match existing
  panel styling; don't hand-roll IMGUI.
- **Settings** persist via **EditorPrefs** (API key, save folder, theme) —
  per-machine, matching the Godot plugin.
- **Networking:** `UnityWebRequest` with a small retry-on-transient helper;
  surface the API's own `{ "error": "..." }` message to users. Callbacks run on
  the main thread.
- **Code license is MIT** (package source). The **generated audio** has a separate
  royalty-free content license (https://www.unlocksfx.com/license). Keep them
  distinct everywhere — code, listings, docs.
- Comments are conversational and explain *why*. Keep that voice.

## Current priorities

- **Asset Store submission** is the next milestone (see `ASSET_STORE_SUBMISSION.md`).
  The listing is pending; the git URL is the install path in the meantime.
- **`.meta` files must be committed** — Unity generates them on import; stable
  GUIDs matter for everyone installing via the git URL. Don't break them.
- Keep parity with the Godot plugin as features evolve.

## Be careful about

- **Never call the audio provider directly.** All traffic goes through
  `unlocksfx.com/api/v1`. Don't hardcode provider endpoints or keys.
- The **API key is a user secret** kept in EditorPrefs — don't log it, commit it,
  or send it anywhere but the UnlockSFX API.
- **Sounds are AI-generated.** Preserve the provenance sidecar and the disclosure
  copy; they exist for compliance (e.g. Steam) — don't quietly remove them.
- Runtime code must stay engine-safe (no editor-only APIs) so banks work in builds.
- Don't promise Asset Store availability until the listing is live.

*Assumption: this repo is a lean, single-purpose editor package; there is no test
suite, CI, or backend here — the backend lives at unlocksfx.com. Verify claims
against the actual API/website before stating them to users.*
