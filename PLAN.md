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

## Phase 1 — Game access (Core) — DONE

- [x] `GameLocator` — Steam autodetect by probing standard roots and parsing
      `libraryfolders.vdf`; manual override always available. Found the install in 19 ms.
- [x] `GameContainerSet` — enumerate `*.utoc` and load into one `FZenPackageContext` in
      priority order. Tracks container index to file name for provenance.
- [x] `WorkspaceExtractor` — resolve the winning `MarvelHeroVoiceData` package, convert via
      `ZenToLegacyConverter`, write `.uasset`/`.uexp` under
      `workspace/vanilla/<build>/<container path>` plus `snapshot.json`
- [x] `EffectCatalog` — enumerate `effect_vo` packages from the container index only,
      nothing extracted. JSON save/load round-trips.

Verified against the live install (build 3805839):

- Extracted `.uasset` (12175 B) and `.uexp` (5693 B) are **byte-identical** to the known-good
  `UAssetToolTUI/extracted` copy. The whole extraction path is correct.
- **39 effect objects in 15 families**, exactly the set the vanilla table references — the game
  currently ships no unused `effect_vo` filters. Scanning the folder is still right, since a
  future patch may add some.
- Oodle auto-downloaded on first use (637952 B), as expected. No shipping needed.

Findings that matter later:

- **Snapshot build and source container are different things, deliberately.** The table
  currently resolves from `Patch_-Windows_1.1.3791970_P.utoc` even though the game is on
  `3805839` — patches only carry assets they changed. `Build` labels the game version;
  `SourceContainer` records where this asset actually came from. Both go in `snapshot.json`.
- Load order is the override mechanism: `FZenPackageContext` lets later loads replace earlier
  ones, so base containers load first and patch containers ascending by build.
- `GameContainerSet.Open` costs **~9 s** (40 containers, 570722 packages indexed). Fine for an
  explicit "Refresh from game", too slow to do on every launch — cache the workspace and only
  re-open on demand. Narrowing the container set is the optimisation if it ever matters.
- Bug found and fixed during verification: the patch-build regex required `_` before the digits,
  but the real names are `Patch_-Windows_1.1.3805839_P.utoc` with a **dot**. Every build parsed
  as null, so the snapshot label fell back to `base`. Ordering had been accidentally correct via
  the filename tie-breaker; it is now correct by construction.

Family grouping rule — strip a trailing `_\d+`. Uniform across every naming style present:

| object | family | ordinal |
|---|---|---|
| `effect_vo_tech_mask_01_slot_0` | `effect_vo_tech_mask_01_slot` | 0 |
| `effect_vo_tech_mask_default_02_slot_2` | `effect_vo_tech_mask_default_02_slot` | 2 |
| `effect_vo_adam_god_03` | `effect_vo_adam_god` | 3 |
| `effect_vo_symbiote_1041_0` | `effect_vo_symbiote_1041` | 0 |

---

## Phase 2 — Table model (Core) — DONE

Verified end to end against the extracted vanilla table (58 entries, 35 with filters,
23 all-None). Every check below passes:

| test | result |
|---|---|
| No-op save | `.uasset` and `.uexp` both **byte-identical** to source |
| Edit existing entry, already-imported effect | works, **0** imports appended |
| Add new entry, family-filled (4 slots) | works, 59 entries, survives reload |
| Effect not yet imported (synthetic) | **2** imports appended, resolves back correctly |
| Clear a slot to None | works, neighbouring slots untouched |
| Remove an entry | works, 57 entries |
| Unrelated entries after an edit | unchanged, order preserved |

**Two bugs found by running it, both invisible to inspection:**

1. **FName number suffixes.** UE folds a canonical trailing `_N` into `FName.Number`, storing
   `effect_vo_x_slot` in the name map with the index alongside. Reading `FName.Value` alone
   collapsed `_slot_0`, `_slot_1` and `_slot_2` onto one key — three distinct effects became
   one, silently. `adam_god_01`–`_04` survived only because a leading zero blocks the split.
   Fixed by using `FName.ToString()` to read and `FName.FromString()` to write, which is also
   how the game stores these names.

2. **Stored unversioned headers are replayed on write.** UAssetTool modified UAssetAPI so
   `NormalExport` and `StructPropertyData` always re-emit the header captured at read time
   (`StructPropertyData.cs:216`). That is right for its JSON round-trips, but an editor that
   flips `IsZero` gets a header describing the *old* zero-mask while the body writes the new
   value — the reader then desynced and failed with a bogus schema index. `Clone()` copies that
   header too, so new entries inherited the template's mask. Fixed by dropping
   `_originalStructHeader` on structs we actually touch, which leaves untouched ones
   byte-exact.

- [x] Load asset with usmap `Mappings`; locate export `MarvelHeroVoiceData`,
      property `SkinBusEffects`
- [x] **Read**: `ObjectProperty.Value < 0` to `Imports[-v-1]` for the object name,
      follow `OuterIndex` to the `Package` import for the full `/Game/...` path.
      `0` means `None`.
- [x] Model: `SkinBusEntry { int SkinId; EffectRef?[4] Slots }`,
      `EffectRef { PackagePath, ObjectName }`
- [x] **`ImportResolver`** — reuse an existing import pair, or append:

```
name map  += "/Game/Marvel/Wwise/Assets/Effects/effect_vo/<dir>/<obj>"
name map  += "<obj>"
import -N  = { ObjectName: "<pkg path>", OuterIndex: 0,
               ClassPackage: /Script/CoreUObject, ClassName: Package }
import -M  = { ObjectName: "<obj>",      OuterIndex: -N,
               ClassPackage: /Script/AkAudio,     ClassName: AkEffectShareSet }
```

- [x] Write back int keys + struct slots, preserving `StructType`
      `MarvelAudioBusEffectSlots`, `SerializeNone: true`, zero GUID, and the
      `IsZero` flag semantics (`IsZero: true` means value 0 / `None`).
      New entries clone an existing pair, so all struct metadata is inherited rather than
      hand-built; `StructPropertyData.HandleCloned` deep-clones the four slot children.

**Decision — import ordering.** Appending breaks the vanilla objects-then-packages
grouping. UE does not care about import order, and `create_mod_iostore` rebuilds
import/dependency maps from the legacy asset at pack time. So: append, and prove it
with the Phase 6 round-trip rather than renumbering the whole table.

**Decision — orphans.** Imports orphaned by clearing a slot are left in place.
Harmless, and pruning would force renumbering.

---

## Phase 3 — Delta project + replay — DONE

- [x] `.rhvfp` schema — effects referenced by **package path**, so the file survives
      import renumbering across game patches
- [x] `ReplayEngine` — apply onto freshly extracted vanilla, per-entry status
- [x] Conflicts reported before anything is written; `ConflictPolicy.Skip` (default) leaves
      vanilla alone, `Overwrite` forces the edit

**Design refinement over the original sketch:** each entry also records `BaseSlots`, the vanilla
state when the edit was authored. Without it, "already applied" and "the game changed underneath
us" are indistinguishable. Removals carry `BaseSlots` too, so the UI can show what was dropped,
and omit `Slots` entirely.

```json
{ "Schema": 1, "AuthoredAgainstBuild": "3805839",
  "Entries": [ { "SkinId": 1015503, "Op": "Upsert",
                 "Slots": ["/Game/.../effect_vo_symbiote_1047_slot_0", null, null, null],
                 "BaseSlots": null } ] }
```

Statuses: `Applied`, `Added`, `AlreadyMatches`, `Conflict`, `MissingEffect`, `Removed`,
`RemoveTargetMissing`. The last three plus `Conflict` set `NeedsAttention`.

Verified:

| test | result |
|---|---|
| Author project from 3 edits + 1 removal | 4 entries, correct ops and baselines |
| **Replay onto fresh vanilla vs editing directly** | **byte-identical output** |
| Replay twice (idempotency) | 3 AlreadyMatches, 1 RemoveTargetMissing, nothing rewritten |
| Vanilla changed underneath, `Skip` | Conflict reported, vanilla left intact |
| Vanilla changed underneath, `Overwrite` | Conflict reported, edit applied |
| Unrelated entries during a conflict | still applied |
| Effect missing from the game | MissingEffect, nothing written |
| Schema newer than supported | rejected with a clear message |

The byte-identical replay-vs-direct-edit result is the one that matters: it proves the delta
captures the full intent of an editing session, so re-applying after a patch cannot silently
drift from what was authored.

---

## Phase 4 — Metadata services — DONE

- [x] `UsmapService` — fetch `Mappings.json`, compare against cache, download to
      `%LocalAppData%/HeroVoiceFilterEditor/usmap/`, load via `new Usmap(path)`.
      Manual override in settings.
- [x] `HeroSkinCatalog` — fetch + parse `MarvelRivalsCharacterIDs.md`, cached as raw markdown
- [x] `SettingsService` — `%AppData%/HeroVoiceFilterEditor/config.json`
- [x] Fully functional offline from cache; every remote failure degrades to the cache

`CacheStatus` is shared by both services: `UpToDate`, `UpdateAvailable`, `Downloaded`,
`Offline`, `Unavailable`.

Verified:

| test | result |
|---|---|
| Usmap: empty cache, no remote | `Unavailable`, no crash |
| Usmap: first fetch | `Downloaded` S9.5, 1272719 bytes |
| Usmap: second fetch | `UpToDate`, no re-download, stable path |
| Usmap: cached, remote disabled | serves from cache |
| Hero ids: first fetch | `Downloaded`, 114 heroes / 643 skins |
| Hero ids: remote disabled | serves from cache |
| Settings: autodetect | found the Steam Paks dir |
| Settings: round-trip, corrupt file | survives, falls back to defaults |

**Markdown raggedness handled** (all present in the live file):

- `????` placeholder hero ids (`Upcoming Characters`, `Captain Marvel`) — skipped
- rows with a skin name but no skin id (line 703, `God Of Stories`) — skipped
- a two-cell row (`Gorr The God Butcher`) and many missing trailing pipes
- heroes with no skins at all (`UltronTrackedBomber`) — parse to an empty list
- one hero row with a blank name (`1069`) — falls back to `Hero 1069`
- **13 duplicate hero ids.** The file reassigns ids across sections and lists the superseded
  owner as `Name (Old)` with no skins — `1057 Deadpool` then `1057 Professor X (Old)`. The
  first parser crashed on `ToDictionary`. Rows are now merged per id, first non-empty name
  winning, so 127 hero rows become 114 unique heroes with skins intact.

**Naming coverage on the real table: 49/58 fully named, 9 hero-only, 0 unknown.** The nine
unnamed are almost all `*001` ids (`1021001`, `1026001`, `1030001`…) that the markdown does not
list, plus one newer skin. Every one still resolves its hero via the leading four digits, so the
worst case in the UI is `Hawkeye — skin 1021001`, never a bare number.

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
