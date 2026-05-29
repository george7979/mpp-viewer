# MPP Viewer

![Early version](https://img.shields.io/badge/status-early%20version-orange)
![Platform](https://img.shields.io/badge/platform-Windows%20x64-blue)
![.NET 8](https://img.shields.io/badge/.NET-8-512BD4)

> **Early version (1.0.0)** — a young project under active development. Core viewing works, but the UI and behaviour may change between releases, and some `.mpp` features are not yet rendered. Feedback is welcome.

A portable, **read-only** desktop viewer for Microsoft Project `.mpp` files. It shows the task list and a synchronized Gantt chart side by side — no Microsoft Project installation required.

- **Single `.exe`, no installation** — download, run, done. No admin rights, no setup.
- **Read-only** — opens and displays files; never modifies them.
- **Self-contained** — bundles the .NET runtime; nothing else to install.

## Features

- **Task table** — ID, name (indented by WBS outline level), duration, start, finish, and % complete.
- **Gantt chart** — task bars on a months timeline, progress fill, and Finish-to-Start dependency arrows.
- **Synchronized view** — the table and the chart share row bands and scroll together, so every row lines up with its bar.
- **Summary tasks** — parent (roll-up) tasks rendered as bracketed bars.

## Requirements

- Windows 10 / 11 (x64)
- No other dependencies — the .NET 8 runtime is bundled in the executable.

## Download & Run

1. Download `MppViewer.exe` from the [latest release](https://github.com/george7979/mpp-viewer/releases/latest).
2. Double-click to run. There is no installer.

### ⚠️ Windows will warn that this app is "unrecognized" — this is expected

`MppViewer.exe` is **not code-signed** (a code-signing certificate is a paid, identity-verified product this hobby project does not have). Windows therefore treats it as an unknown publisher and shows scary-looking warnings. **They do not mean the file is malware** — only that Windows cannot verify who published it. Because the project is open source, you can read every line and [build the exe yourself](#building-from-source) if you prefer not to trust the prebuilt binary.

You will likely hit one or two of these:

**1. Browser download warning.** Edge/Chrome may say the file *"isn't commonly downloaded"* or *"can harm your computer"*. Choose **Keep** / **Keep anyway** (in Edge: click the **···** next to the download → **Keep**).

**2. SmartScreen blue dialog on first launch.** A full-screen blue window appears:

> **Windows protected your PC**
> Microsoft Defender SmartScreen prevented an unrecognized app from starting. Running this app might put your PC at risk.

By default it only shows a **Don't run** button. To run it anyway:

1. Click **More info** (the small link in the dialog).
2. A **Run anyway** button appears — click it.

The app then starts normally, and Windows remembers your choice for that file. (The exact wording may differ depending on your Windows display language.)

## Usage

1. **File → Open…** (or `Ctrl+O`) and pick a `.mpp` file.
2. The task table fills the left pane; the Gantt chart fills the right pane.
3. Scroll the table vertically — the chart follows. Use the chart's bottom scrollbar to pan the timeline. The mouse wheel over the chart scrolls both panes.
4. Drag the splitter between the panes to resize them.

The status bar shows the file name, task count, and the project's date range.

## What it does not do (yet)

This is an early, focused release. Out of scope for now:

- Any editing — the viewer is strictly read-only.
- Resource sheets and resource assignments.
- Filtering, grouping, and sorting.
- Export to PDF / Excel / image.
- Calendar view and baseline comparison.

## Building from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download).

```bash
# Build and run
dotnet run --project src/MppViewer/MppViewer.csproj

# Run the tests
dotnet test MppViewer.sln

# Produce a portable single-file exe (as CI does)
dotnet publish src/MppViewer/MppViewer.csproj -c Release -r win-x64 \
  --self-contained true /p:PublishSingleFile=true /p:EnableCompressionInSingleFile=true -o publish/
```

## Tech stack

- **.NET 8 + WinForms** — desktop UI, published as a self-contained single file.
- **[MPXJ](https://www.mpxj.org/)** (`net.sf.mpxj`) — reads the `.mpp` format.
- **GitHub Actions** — builds and publishes the `win-x64` executable on every push.

## License

MIT — see [LICENSE](LICENSE).
