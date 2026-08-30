# Vendored dependencies

## vendor/UAssetToolRivals

Source: <https://github.com/XzantGaming/UAssetToolRivals>
Vendored at commit `f0de016` ("Stamp releases with their tag, and report the build over the JSON API"), 2026-08-30.

Vendored rather than submoduled, deliberately: this project links UAssetTool and UAssetAPI
in-process and makes asset-class-specific edits to them, so the source is ours to modify.
There is no upstream sync. To compare against upstream, clone the repo at the commit above
and diff `src/`.

Removed during vendoring: `.git/`, `.github/`, `.gitattributes`, `.gitignore`,
`UAssetTool.sln`, `publish.ps1`. Kept: `src/`, `README.md`, `TECHNICAL_ANALYSIS.md`,
`LICENSE`, `NOTICE.md`.

Local modifications to date:

- `src/UAssetTool/UAssetTool.csproj` — `OutputType` changed from `Exe` to `Library`, and the
  single-file/self-contained publish properties removed, so the editor can reference it as a
  library. `Program.Main` is left intact and simply goes uncalled.
- `src/UAssetAPI/UAssetAPI.csproj` — the `BeforeBuildMigrated` target used to stamp the build by
  running `git rev-parse --short HEAD`. With no `.git` present that failed with MSB3073 on every
  build, so it now writes the pinned upstream commit `f0de016` directly. Bump this string if the
  vendored source is ever refreshed.

`LICENSE` and `NOTICE.md` are mirrored at the repository root for attribution.
