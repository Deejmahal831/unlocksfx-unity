# Testing & publishing the UnlockSFX Unity package

## 1. Generate `.meta` files + test (do this first)

This folder is the package source, but it has **no `.meta` files yet** — Unity
creates those on import, and they must be committed so GUIDs are stable for
everyone who installs via git.

1. In any Unity 2021.3+ project, **Window → Package Manager → + → Add package from
   disk…** and pick this folder's `package.json` (or copy the folder into the
   project's `Packages/`).
2. Unity imports it and generates `.meta` files **inside this folder**.
3. Test it: **Window → UnlockSFX**, paste an API key, generate a sound, confirm it
   imports into `Assets/SFX` as an `AudioClip`. Also try right-click a folder →
   **UnlockSFX: Generate here…**.
4. Commit the generated `.meta` files (see step 2).

## 2. Push to GitHub (enables the UPM git URL)

This folder is already a git repo with an initial commit. After step 1 generates
the `.meta` files:

```bash
cd C:/dev/unlocksfx-unity
git add .
git commit -m "Add Unity-generated .meta files"
# create an EMPTY public repo named unlocksfx-unity on github.com, then:
git remote add origin https://github.com/<your-username>/unlocksfx-unity.git
git push -u origin main
```

Now anyone can install via **Package Manager → Add package from git URL →**
`https://github.com/<your-username>/unlocksfx-unity.git`.

## 3. Submit to the Unity Asset Store

> Free asset → no 30% cut, simpler review. But Unity's review is stricter and
> slower than Godot's (often a couple of weeks). Submit early; lean on the git
> URL in the meantime.

1. Create a **Publisher account** at https://publisher.unity.com.
2. In Unity, install the **Asset Store Tools** package (from the Asset Store) — it
   uploads packages from inside the editor.
3. Create a new **draft** in the Publisher Portal:
   - **Category:** Tools → Audio (or Editor Extensions).
   - **Price:** **Free.**
   - **Supported Unity version:** 2021.3.
4. In the description, be **transparent** that it's free but requires a free
   UnlockSFX account + credits + internet (Unity rejects surprise paywalls — same
   rule as Godot). Suggested:
   > Generate game-ready sound effects from a text prompt, right inside the Unity
   > Editor — they import straight into your project as AudioClips. MP3/WAV,
   > variation-friendly, royalty-free and cleared to ship. Requires a free
   > UnlockSFX account and credits (unlocksfx.com).
5. Add **screenshots** (the UnlockSFX window, a generated clip in the Project,
   the right-click menu) — at least one.
6. Upload the package with Asset Store Tools, then **submit for review**.

## 4. After it's approved

Tell me the listing URL and I'll wire the website's `/integrations` page to flip
Unity from "In development" to "Available now" with a "Get it on the Asset Store"
button — same as the Godot flow.

## Notes

- MIT covers the **package code**; generated audio has its own royalty-free
  content license (https://www.unlocksfx.com/license) — keep them distinct in the
  listing.
- This is the MVP (single-clip generate + import). Variation banks, seamless-loop
  parity, and a license/provenance card are the next additions.
