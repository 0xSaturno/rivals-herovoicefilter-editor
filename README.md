# Rivals Hero Voice Filter Editor

Desktop tool for editing voice filter overrides in Marvel Rivals' `MarvelHeroVoiceData` table, and exporting the result back to a `.uasset`/`.uexp` pair you can pack in a mod.

---

## Requirements

- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

--- 

## First run

Open Settings and fill in:

- **Paks directory** — `...\MarvelGame\Marvel\Content\Paks`. Autodetect usually finds it.
- **AES key** — needed to read the game's containers.

Everything else (usmap, hero/skin names) updates itself on launch unless you turn that off.

---

## Usage

1. **Refresh from game** — reads the vanilla table from the game's containers.
2. Pick an entry (or **Add**) and set its voice filters.
3. **Save project** to keep your edits in a project file you can reopen later.
4. **Export .uasset** when you want the actual file to put in a mod.

---

## Acknowledgements

- [donutman07/MarvelRivalsCharacterIDs](https://github.com/donutman07/MarvelRivalsCharacterIDs) — hero and skin names
- [XzantGaming/UassetToolRivals](https://github.com/XzantGaming/UassetToolRivals) — the vendored fork used to read/write the game's assets


