# MPP Viewer — Design Spec

**Date:** 2026-05-29
**Status:** Approved

---

## Goal

Portable, read-only Windows desktop application for viewing MS Project `.mpp` files. Runs as a single `.exe` — no installation, no elevated permissions required. Built automatically via GitHub Actions.

---

## Constraints

- Single `.exe`, no installer, no admin rights
- Windows only
- Read-only (no editing)
- GitHub Actions builds the artifact

---

## Technology Stack

- **Runtime:** .NET 8, self-contained, `PublishSingleFile=true`
- **UI framework:** WinForms
- **MPP parser:** MPXJ.NET (native .NET port, no JVM required)
- **Target:** `win-x64`
- **Expected exe size:** 50–80 MB

---

## Architecture

```
mpp-viewer/
├── src/
│   └── MppViewer/
│       ├── MainForm.cs
│       ├── Controls/
│       │   ├── TaskGridView.cs
│       │   └── GanttControl.cs
│       ├── Models/
│       │   └── ProjectData.cs
│       ├── Services/
│       │   └── MppReader.cs
│       └── MppViewer.csproj
├── .github/
│   └── workflows/
│       └── build.yml
└── README.md
```

**Three layers:**
- **UI** (`MainForm`, controls) — operates on `ProjectData`, no MPXJ dependency
- **Model** (`ProjectData`) — plain C# records, tasks/dependencies
- **Parser** (`MppReader`) — single place where MPXJ.NET is used

**Data flow:**
```
User → "Open file" → MppReader (MPXJ.NET) → ProjectData → TaskGridView + GanttControl
```

---

## UI Layout

```
┌─────────────────────────────────────────────────────────┐
│ Plik                                                     │
├──────────┬──────────────────────────────────────────────┤
│          │                                              │
│  Tabela  │           Wykres Gantta                      │
│  zadań   │                                              │
│          │                                              │
├──────────┴──────────────────────────────────────────────┤
│ plik.mpp  │  127 zadań  │  01.01.2025 – 31.12.2025      │
└─────────────────────────────────────────────────────────┘
```

**Menu:** Plik → Otwórz, Wyjście

**TaskGridView columns:** `ID` | `Nazwa zadania` (indented by WBS level) | `Czas trwania` | `Start` | `Koniec` | `% ukończenia`

**GanttControl (custom UserControl):**
- X axis: timeline (days/weeks/months — auto-scaled to project range)
- Y axis: task rows synchronized with table scroll
- Task bars colored by `% complete`
- Dependency arrows (Finish-to-Start)
- Horizontal scrollbar under the Gantt

**SplitContainer:** draggable divider between table and Gantt

---

## Data Model

```csharp
record ProjectData(
    string FilePath,
    DateTime ProjectStart,
    DateTime ProjectFinish,
    IReadOnlyList<TaskItem> Tasks
);

record TaskItem(
    int Id,
    string Name,
    int OutlineLevel,
    DateTime? Start,
    DateTime? Finish,
    TimeSpan Duration,
    int PercentComplete,
    IReadOnlyList<int> PredecessorIds
);
```

---

## Error Handling

| Situation | Behavior |
|---|---|
| File not found / access denied | MessageBox with message, window stays open |
| Corrupt / unsupported format | MessageBox "Nie można odczytać pliku" |
| Large file (>500 tasks) | Wait cursor + async parsing (no UI block) |

---

## Out of Scope (v1)

- Resource sheet / resource assignments
- Filtering and grouping
- Export to PDF/Excel
- Any editing
- Calendar view

---

## GitHub Actions Build

```yaml
name: Build

on:
  push:
    branches: [main]
  workflow_dispatch:

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - run: dotnet publish src/MppViewer/MppViewer.csproj
               -c Release
               -r win-x64
               --self-contained true
               /p:PublishSingleFile=true
               /p:EnableCompressionInSingleFile=true
               -o publish/
      - uses: actions/upload-artifact@v4
        with:
          name: mpp-viewer
          path: publish/MppViewer.exe
```

Artifact available directly from GitHub Actions tab — download and run, no installation.
