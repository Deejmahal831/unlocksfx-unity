# UnlockSFX — Unity package (Unity 2021.3 LTS+)

Generate game-ready sound effects from a text prompt, right inside the Unity
Editor. Describe a sound and a clean, imported `AudioClip` drops straight into
your project — no asset hunting, no leaving the editor.

> Requires a free [UnlockSFX](https://www.unlocksfx.com) account and credits.
> The plugin calls `https://www.unlocksfx.com/api/v1` with your API key.

## Install

### Package Manager — git URL (recommended)
1. In Unity: **Window → Package Manager**.
2. **+ → Add package from git URL…**
3. Paste:
   ```
   https://github.com/Deejmahal831/unlocksfx-unity.git
   ```

### Asset Store
Search for **UnlockSFX** in the Unity Asset Store and add it to your project.
*(Listing pending — use the git URL above in the meantime.)*

### Manual
Copy this repo's `Editor/` folder and `package.json` into a folder under your
project's `Packages/` (or drop `Editor/` anywhere under `Assets/`).

## Setup

1. Open the panel: **Window → UnlockSFX**.
2. Create an API key at **unlocksfx.com → Settings → API keys** and paste it into
   the **API key** field (it's remembered between sessions).
3. Set a **Save folder** (defaults to `Assets/SFX`).

## Use

- **Window → UnlockSFX** — describe a sound, pick a category, duration, and format
  (MP3/WAV), then **Generate**. The clip is downloaded and imported into your save
  folder as an `AudioClip`, and selected in the Project window.
- **Right-click a folder** in the Project window → **UnlockSFX: Generate here…** —
  opens the panel pointed at that folder.
- **Variants 1** → a single clip. **Variants 2–8** → a variation bank: N clips in a
  subfolder plus a `<name>.prefab` carrying an **UnlockSFX Random Player** component
  (clips pre-assigned, no-repeat + pitch variance). Drop the prefab into a scene and
  call `Play()` for instant non-repeating playback.
- **Loop** → generates a seamless clip (no audible start/end) for ambience and beds.
- **Listen** → preview the generated clip without entering Play mode.
- **Cleared to ship** → after a generate, the panel shows the license rights and a
  one-click **Copy Steam AI disclosure** button. A hidden `.<name>.unlocksfx.json`
  sidecar is written next to each clip recording the prompt, license, and that it's
  AI-generated.

Pricing: 1 credit per 5 seconds (≤5s = 1, ≤10s = 2, …); banks get ~25% off and only
charge for clips that succeed. You get free credits to start.

## License

This package's source code is released under the **MIT License** (see
[`LICENSE.md`](LICENSE.md)).

The **sound effects** you generate are covered separately by the UnlockSFX
content license — royalty-free, commercial use, no attribution, with the only
limit being that you don't resell the raw audio files. Full terms:
https://www.unlocksfx.com/license

Sounds are AI-generated. The in-app tool provides a one-click Steam AI-disclosure
snippet.

## Requirements

- Unity **2021.3 LTS** or newer (compile-verified on 2021.3.45f2 and 6000.5)
- A free UnlockSFX account + credits
- Internet access (the editor calls `https://www.unlocksfx.com/api/v1`)

## Links

- Website: https://www.unlocksfx.com
- Unity guide: https://www.unlocksfx.com/integrations
