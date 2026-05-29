# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

MPP Viewer is a portable, **read-only** Windows desktop app (.NET 8 / WinForms) that opens Microsoft Project `.mpp` files and shows a task table next to a synchronized Gantt chart. It ships as a single self-contained `win-x64` `.exe` — no installer, no admin rights.

## Environment: dotnet is NOT native in WSL

This repo is developed under WSL2, where `dotnet` on PATH does not exist. Use the Windows SDK explicitly for every command:

```bash
DOTNET="/mnt/c/Program Files/dotnet/dotnet.exe"

"$DOTNET" build  MppViewer.sln -c Release            # build everything
"$DOTNET" test   MppViewer.sln -c Release            # run all tests (xUnit)
"$DOTNET" run    --project src/MppViewer/MppViewer.csproj   # launch the app

# Run a single test by fully-qualified name (or substring):
"$DOTNET" test MppViewer.sln --filter "FullyQualifiedName~GanttMetricsTests.DateToX_AtProjectStart_ReturnsZero"

# Produce the portable single-file exe exactly as CI does:
"$DOTNET" publish src/MppViewer/MppViewer.csproj -c Release -r win-x64 \
  --self-contained true /p:PublishSingleFile=true /p:EnableCompressionInSingleFile=true -o publish/
```

Tests target `net8.0-windows` (the app references WinForms), so they must run via the Windows dotnet, not a Linux-native runtime.

## Architecture: three layers, one direction

```
User → File→Open → MppReader (MPXJ) → ProjectData/TaskItem → TaskGridView + GanttControl
```

- **`Services/MppReader.cs`** — the **only** file that touches MPXJ. It converts the parsed file into plain C# records. Keep all MPXJ types out of every other file.
- **`Models/ProjectData.cs`** — immutable `record`s (`ProjectData`, `TaskItem`). The UI knows only these.
- **`Controls/` + `MainForm.cs`** — WinForms UI over `ProjectData`, no parser dependency.

## The critical invariant: the Gantt is slaved to the grid's row geometry

`GanttControl` does **not** compute its own row positions. It reads the exact per-row rectangle from the `TaskGridView` via `GetRowDisplayRectangle(i, false)` and draws each bar, row band, and dependency arrow at that Y. This is why table and chart stay pixel-aligned during smooth scrolling. Consequences when editing:

- **Shared header height.** `GanttControl.HeaderHeight` (44) MUST equal the grid's `ColumnHeadersHeight` (set in `TaskGridView` to `GanttControl.HeaderHeight`). Both panes sit in a `SplitContainer`, so equal header height makes row 0 begin at the same Y in both. Change one → change both, or alignment breaks.
- **Matching alternating bands.** Both controls shade odd rows with the same `Color.FromArgb(245,245,248)` and the same even/odd parity, so a row and its bar share one continuous color band. Keep the parity identical on both sides.
- **Scroll is one source of truth.** The grid's own vertical scrollbar drives everything; `AttachGrid` subscribes to the grid's `Scroll`/`RowsAdded`/`Resize` and just calls `Invalidate()`. The chart has only a horizontal scrollbar (timeline). Don't reintroduce a separate row counter — that was the original bug.
- **Off-screen rows return height 0** from `GetRowDisplayRectangle`; `TryRowBounds` skips them, which is also how dependency arrows avoid drawing "to nowhere".
- Pure geometry that *is* unit-testable (no WinForms types) lives in `Controls/GanttMetrics.cs` (`DateToX`, `TotalWidth`).

## MPXJ API gotchas (in MppReader.cs)

- NuGet package is **`net.sf.mpxj`**, not `MPXJ`. It is an IKVM port of the Java library, so the API is **Java-style**: `task.getName()`, `task.getID()`, `task.getStart()` — not C# properties.
- Dates are **`java.time.LocalDateTime`** (JSR-310), not `java.util.Date`. Convert via the `ToDateTime` helper.
- Java collections (`task.getPredecessors()`, `projectFile.getTasks()`) do **not** implement `IEnumerable`. Call `.toArray()` and iterate that.
- `UniversalProjectReader().read(path)` can return `null` for unsupported formats — already guarded with a throw.

## WinForms specifics worth knowing before you touch them

- **`Program.cs` uses an explicit `[STAThread] Main` on purpose.** Do not "simplify" it back to top-level statements — the synthesized entry point loses `[STAThread]`, and `OpenFileDialog` (a COM `IFileDialog`) then deadlocks the UI in an MTA apartment.
- **Control add-order in `MainForm` matters.** The `Dock.Fill` `SplitContainer` is added to `Controls` *first* (lowest z-order) so the menu (Top) and status bar (Bottom) reserve their edges instead of being covered. `SplitterDistance` is set in `OnLoad`, not the constructor, because the container has no real size yet during construction.
- **`TaskGridView` sets `DoubleBuffered` via reflection** — that property is `protected` on `DataGridView`. `Panel.DoubleBuffered` (used by `GanttControl`) is settable directly.

## Branch workflow & release

- **dev-first:** commit to `dev`, let CI build a testable `.exe` artifact, smoke-test on Windows, then `git merge dev --ff-only` into `main`. CI (`.github/workflows/build.yml`) builds on pushes to **both** `dev` and `main` and uploads the `mpp-viewer-win-x64` artifact (30-day retention).
- **Releases** attach the CI-built binary (reproducible) rather than a local build, **code-signed locally before upload**: download the artifact from the `main` run → sign it on a local Windows machine (Certum Open Source cloud cert via SimplySign Desktop) → `gh release create vX.Y.Z --target main <signed-exe>` (this creates the tag remotely). Bump `<Version>`/`<FileVersion>` in `MppViewer.csproj` for each release. The full signed-release procedure (purchase, one-time setup, `signtool` command, verification) is in [`docs/RELEASE.md`](docs/RELEASE.md).
- **Signing is local, not in CI.** Certum SimplySign has no headless API — the key lives in Certum's cloud HSM, reached only through the SimplySign Desktop app (manual TOTP, 2-hour session). The build stays reproducible in CI; the signature is applied by hand. Releases up to **v1.2.2 were unsigned** (Windows SmartScreen warnings, documented in README); signing is introduced from the next release, and the README's "unsigned"/"on the roadmap" wording is only updated once a signed binary actually ships.

## Scope (intentionally out, do not add without a request)

Editing, resource sheets, filtering/grouping, export (PDF/Excel), calendar view, baselines. The full design and task breakdown live in `docs/superpowers/specs/` and `docs/superpowers/plans/`.
