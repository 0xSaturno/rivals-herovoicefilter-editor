# HeroVoiceFilterEditor — Implementation Plan

Avalonia editor for the `SkinBusEffects` table in the Marvel Rivals asset
`MarvelHeroVoiceData.uasset`, backed by a vendored, directly-linked UAssetTool/UAssetAPI.

**Status:** planned, not started. Last updated 2026-08-30.

---

## Decisions locked in

| Question | Decision |
|---|---|
| Save output | Loose `.uasset`/`.uexp` only. No mod packing in-app. |
| Patch resilience | `.rhvfp` delta file, replayed onto freshly extracted vanilla. |
| Data source | Extract once to a managed workspace, with "Refresh from game". |
| Edit scope | `SkinBusEffects` only. Other 39 properties round-trip untouched, not shown. |
| Backend | In-process ProjectReference to vendored UAssetAPI + UAssetTool. No sidecar processes. |
| Config | Fully standalone. Never reads `UAssetToolTUI/config.json`. |
| Filter picker | 4 independent searchable slot combos + optional family quick-fill button. |
| Skin picker | Type-ahead over hero/skin catalog, raw 7-digit ID accepted as fallback. |
| Effect list | Restricted to `Marvel/Content/Marvel/Wwise/Assets/Effects/effect_vo/`. Includes unreferenced effects. |
| Repo | `git init` at project root; vendor UAssetToolRivals, delete its nested `.git`. |
| Updates | Bundled defaults, disk cache, background check on launch, works offline. |

---

## Established facts (verified against the real asset)

- `SkinBusEffects` is `TMap<int32 SkinID, MarvelAudioBusEffectSlots>`.
  The struct is 4 `ObjectProperty` slots: `Effect0`..`Effect3`.
- Slot values are negative **import indices**; `0` means `None`.
  58 entries, ~39 distinct effect objects, many entries fully `None`.
- Each effect object resolves as
  `Import(AkEffectShareSet) --OuterIndex--> Import(Package "/Game/.../effect_vo/<dir>/<obj>")`.
  One package == one `AkEffectShareSet` object.
- Slot index is **not** tied to the asset's `_N` suffix. Vanilla `1049503` mixes
  `cloth_mask_default02_slot_0` with `cloth_mask_default_slot_0` / `_1`. Free-form assignment.
- Asset is `IsUnversioned: true`, so a usmap is **mandatory** to parse it.
- `Mappings.json` is a manifest, not a mapping file:
  `[{ "url", "fileName", "uploaded" }]`. Currently `S9.5 / build 3805839`.
- Vanilla import table groups all 43 object imports first (`-1..-43`),
  then all 43 package imports (`-44..-86`).
- Game Paks dir contains `global.utoc`, `pakchunk*-Windows.utoc`,
  and `Patch_-Windows_1.1.<build>_P.utoc` overrides. Highest build wins.
  Live build `3805839` matches the current usmap build.
- `UAssetTool.Program.Main` is `public static async Task<int>` inside a normal
  `public partial class`, so flipping `OutputType` to `Library` needs no code change.
- Installed SDKs include 8.0.424, so the net8.0 target builds as-is.

---

## Target layout

```
HeroVoiceFilterEditor/                    <- git init here
  HeroVoiceFilterEditor.sln
  PLAN.md
  src/HeroVoiceFilterEditor.Core/         net8.0 classlib, no Avalonia deps
  src/HeroVoiceFilterEditor/              net8.0, Avalonia 11 + CommunityToolkit.Mvvm
  vendor/UAssetToolRivals/src/UAssetAPI/  ProjectReference
  vendor/UAssetToolRivals/src/UAssetTool/ ProjectReference, OutputType Exe -> Library
  LICENSE, NOTICE.md
```

---

## Phase 0 — Scaffolding — DONE

- [x] `git init` at project root (branch `main`), add `.gitignore` (bin/obj/workspace/cache)
- [x] Move `UAssetToolRivals/` to `vendor/UAssetToolRivals/`, delete its `.git`
- [x] Keep `LICENSE` + `NOTICE.md` at root for attribution; provenance in `VENDOR.md`
- [x] `UAssetTool.csproj`: `OutputType` Exe to Library; drop `PublishSingleFile`,
      `SelfContained`, `IncludeNativeLibrariesForSelfExtract`,
      `IncludeAllContentForSelfExtract`, `RuntimeIdentifiers`
- [x] Create `HeroVoiceFilterEditor.sln` + both `src/` projects, wire ProjectReferences
- [x] `dotnet build` green — 0 errors, 3 warnings (all pre-existing upstream)
- [x] App launches, window renders, `BackendInfo` resolves types from both vendored
      assemblies at runtime

Notes:

- **Avalonia 11.3.20**, not the newer 12.1.1. `Avalonia.Diagnostics` has no 12.x release, so
  the 11.3 line keeps the dev-tools inspector version-matched. Revisit if 12.x gains it.
- `UAssetAPI.csproj` stamped its build via `git rev-parse --short HEAD`; with `.git` removed
  that warned MSB3073 every build, so it now writes the pinned commit. See `VENDOR.md`.
- Oodle native lib (`oo2core_9_win64.dll`) is resolved by `OodleCompression`'s
  `DllImportResolver`, which downloads it on demand. Confirm this works in Phase 1.
- No initial commit made yet — repo is initialised but unstaged.

---

## Phase 1 — Game access (Core)

- [ ] `GameLocator` — Steam autodetect via `libraryfolders.vdf`, manual override
- [ ] `ContainerSet` — enumerate `*.utoc`, merge `IoStoreToc.FileMap` by mount priority
      (global + pakchunks, then `Patch_..._P` ascending by build number; last wins).
      Record which container each path resolved from, for UI provenance.
- [ ] `WorkspaceExtractor` — pull the winning `MarvelHeroVoiceData` chunk, Zen to legacy
      convert, write to `workspace/vanilla/<build>/Marvel/Content/Marvel/Audio/Voice/`,
      plus a manifest (build id, source container, timestamp)
- [ ] `EffectCatalog` — enumerate `effect_vo` package paths **from the container index
      only, nothing extracted**. Cache to workspace JSON.

Family grouping rule — strip a trailing `_\d+`. Uniform across every naming style present:

| object | family | ordinal |
|---|---|---|
| `effect_vo_tech_mask_01_slot_0` | `effect_vo_tech_mask_01_slot` | 0 |
| `effect_vo_tech_mask_default_02_slot_2` | `effect_vo_tech_mask_default_02_slot` | 2 |
| `effect_vo_adam_god_03` | `effect_vo_adam_god` | 3 |
| `effect_vo_symbiote_1041_0` | `effect_vo_symbiote_1041` | 0 |

---

## Phase 2 — Table model (Core)

- [ ] Load asset with usmap `Mappings`; locate export `MarvelHeroVoiceData`,
      property `SkinBusEffects`
- [ ] **Read**: `ObjectProperty.Value < 0` to `Imports[-v-1]` for the object name,
      follow `OuterIndex` to the `Package` import for the full `/Game/...` path.
      `0` means `None`.
- [ ] Model: `SkinBusEntry { int SkinId; EffectRef?[4] Slots }`,
      `EffectRef { PackagePath, ObjectName }`
- [ ] **`ImportResolver`** — reuse an existing import pair, or append:

```
name map  += "/Game/Marvel/Wwise/Assets/Effects/effect_vo/<dir>/<obj>"
name map  += "<obj>"
import -N  = { ObjectName: "<pkg path>", OuterIndex: 0,
               ClassPackage: /Script/CoreUObject, ClassName: Package }
import -M  = { ObjectName: "<obj>",      OuterIndex: -N,
               ClassPackage: /Script/AkAudio,     ClassName: AkEffectShareSet }
```

- [ ] Write back int keys + struct slots, preserving `StructType`
      `MarvelAudioBusEffectSlots`, `SerializeNone: true`, zero GUID, and the
      `IsZero` flag semantics (`IsZero: true` means value 0 / `None`)

**Decision — import ordering.** Appending breaks the vanilla objects-then-packages
grouping. UE does not care about import order, and `create_mod_iostore` rebuilds
import/dependency maps from the legacy asset at pack time. So: append, and prove it
with the Phase 6 round-trip rather than renumbering the whole table.

**Decision — orphans.** Imports orphaned by clearing a slot are left in place.
Harmless, and pruning would force renumbering.

---

## Phase 3 — Delta project + replay

- [ ] `.rhvfp` schema — effects referenced by **package path**, so the file survives
      import renumbering across game patches

```json
{ "schema": 1, "authoredAgainstBuild": "3805839",
  "entries": [ { "skinId": 1015503, "op": "upsert",
                 "slots": ["/Game/.../effect_vo_symbiote_1041_0", null, null, null] } ] }
```

- [ ] `ReplayEngine` — apply onto freshly extracted vanilla, per-entry status:
  - **Applied** — change written
  - **AlreadyMatches** — vanilla already has exactly this
  - **Conflict** — vanilla now ships non-`None` slots for that skin
  - **MissingEffect** — referenced package no longer exists in the game
- [ ] Surface conflicts in the UI *before* anything is written

---

## Phase 4 — Metadata services

- [ ] `UsmapService` — fetch `Mappings.json`, compare `fileName`/`uploaded` against
      cache, download to `%LocalAppData%/HeroVoiceFilterEditor/usmap/`,
      load via `new Usmap(path)`. Manual override in settings.
- [ ] `HeroSkinCatalog` — fetch + parse `MarvelRivalsCharacterIDs.md`
      (`| id | name | skinId | skinName |`; blank hero cells inherit the previous row).
      **Parser must be lenient** — the source has ragged rows with missing trailing
      pipes and trailing whitespace. Cache with timestamp.
- [ ] `SettingsService` — `%AppData%/HeroVoiceFilterEditor/config.json`:
      Paks dir, AES key (default `0x0C263D8C22DCB085894899C3A3796383E9BF9DE0CBFB08C9BF2DEF2E84F29D74`),
      workspace dir, usmap override, auto-check-on-launch toggle
- [ ] Background check at startup; fully functional offline from cache

---

## Phase 5 — UI (single window, Fluent, MVVM)

- [ ] **Top bar** — `vanilla 3805839 · from Patch_...3805839_P` ·
      `[Refresh from game]` · `[Open/Save project]` · `[Export .uasset]` · settings
- [ ] **Left** — entries grouped by hero, `1014501 · Punisher — Punisher 2099`,
      inline filter summary; search box; toggles All / Has filters / Empty / Modified;
      added + modified rows badged
- [ ] **Right** — 4 searchable slot combos over every `effect_vo` object;
      family quick-fill combo + Apply above them
      (`effect_vo_tech_mask_01_slot` fills slots 0-2, leaves 3 `None`);
      per-slot clear; add / remove entry
- [ ] **Add entry dialog** — type-ahead over the catalog, raw 7-digit ID accepted for
      unlisted skins (labelled *unknown skin*), rejects IDs already in the table
- [ ] **Log pane** — extraction and replay reports

No slot/suffix constraint is enforced anywhere — slots are free-form by design.

---

## Phase 6 — Verification

- [ ] **Round-trip** — load vanilla, save untouched, byte-compare against source.
      Any diff must be explained before moving on.
- [ ] **Edit** — add entry `1015503` with the symbiote family, save, reload,
      assert resolved package paths come back identical
- [ ] **Reference** — cross-check the parsed 58 entries against the known-good
      `UAssetToolTUI/extracted` copy

---

## Risks

- **Import append order** differs from vanilla grouping. Mitigation: Phase 6
  round-trip; fallback is a canonical import-table rebuild with full index remap.
- **Zen to legacy conversion** for this asset class — already proven to work, since the
  existing extracted copy came out of that path.
- **Effect list scoped to `effect_vo`** by choice. Filters added elsewhere in a future
  patch will not appear. Accepted.
- **IDs markdown is third-party** and may lag new skins. Raw-ID fallback covers it.

---

## Reference paths

- Sample extracted asset: `B:\MRivalsMods\Coding\UAssetToolTUI\extracted\Marvel\Content\Marvel\Audio\Voice\MarvelHeroVoiceData.uasset`
- Sample effect asset: `...\extracted\Marvel\Content\Marvel\Wwise\Assets\Effects\effect_vo\effect_vo_tech_mask_01_slot_0.uasset`
- Game Paks: `D:\Games\SteamLibrary\steamapps\common\MarvelRivals\MarvelGame\Marvel\Content\Paks`
- Usmap manifest: `https://raw.githubusercontent.com/SpaceDepot/rivals-depot/refs/heads/main/Mappings.json`
- Hero/skin IDs: `https://raw.githubusercontent.com/donutman07/MarvelRivalsCharacterIDs/refs/heads/main/MarvelRivalsCharacterIDs.md`
