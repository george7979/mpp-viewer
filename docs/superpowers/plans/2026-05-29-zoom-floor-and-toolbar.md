# Zoom Floor + Toolbar Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the Gantt zoom-out floor equal the "fit to width" scale, and replace the menu bar with a single toolbar (Open, Zoom −/+, Fit to width, resource filter, right-aligned About/GitHub/Exit).

**Architecture:** A new pure `GanttMetrics.ClampZoom` defines the dynamic floor (= fit scale). `GanttControl`'s zoom engine is refactored into one `ApplyZoom(anchorX, notches)` core fed by the wheel (cursor anchor) and new `ZoomIn`/`ZoomOut` (center anchor), all clamped through `ClampZoom`. `MainForm` drops the menu, moves every action onto the toolbar, preserves `Ctrl+O` at the form level, and adds an About `MessageBox`.

**Tech Stack:** .NET 8, WinForms, xUnit. `dotnet` is **not** on PATH in this WSL repo — use the Windows SDK explicitly.

**Conventions for every command:**
```bash
DOTNET="/mnt/c/Program Files/dotnet/dotnet.exe"
```
- Build: `"$DOTNET" build MppViewer.sln -c Release`
- Test (all): `"$DOTNET" test MppViewer.sln -c Release`
- Test (one): `"$DOTNET" test MppViewer.sln -c Release --filter "FullyQualifiedName~<name>"`
- Repo root: `/home/jerzy/cursor/mpp-viewer`, branch `dev`. Commit on `dev`; do NOT push (owner's call).

---

## File Structure

| File | Responsibility | Change |
|---|---|---|
| `src/MppViewer/Controls/GanttMetrics.cs` | Pure timeline math | Add `ClampZoom(candidate, fitPpd)` |
| `tests/MppViewer.Tests/Controls/GanttMetricsTests.cs` | Unit tests | Add 4 `ClampZoom` tests |
| `src/MppViewer/Controls/GanttControl.cs` | Chart + interaction | `ZoomAt`→`ApplyZoom(anchorX, notches)`; floor via `ClampZoom`; `ZoomIn`/`ZoomOut`; clamp start scale in `Load`; `DefaultPixelsPerDay` const |
| `src/MppViewer/MainForm.cs` | Window + toolbar | Remove menu; rebuild toolbar; `Ctrl+O` at form level; `ShowAbout` |

---

## Task 1: ClampZoom pure function

**Files:**
- Modify: `src/MppViewer/Controls/GanttMetrics.cs`
- Test: `tests/MppViewer.Tests/Controls/GanttMetricsTests.cs`

Context: `GanttMetrics` already has `ClampPixelsPerDay` (clamps to `[Min,Max]`), `FitPixelsPerDay` (returns a value in `[Min,Max]`), `MaxPixelsPerDay = 60f`, `MinPixelsPerDay = 2f`. `ClampZoom` clamps a candidate scale into `[fitPpd, Max]` — the new zoom range whose **lower** bound is the fit scale. Since `FitPixelsPerDay` always returns `<= MaxPixelsPerDay`, `fitPpd <= Max`, so `Math.Clamp(candidate, fitPpd, Max)` always has `lo <= hi`.

- [ ] **Step 1: Write the failing tests**

Append inside the `GanttMetricsTests` class in `tests/MppViewer.Tests/Controls/GanttMetricsTests.cs` (before the closing `}`):

```csharp
    [Fact]
    public void ClampZoom_BelowFitFloor_ReturnsFit()
        => Assert.Equal(5f, GanttMetrics.ClampZoom(1f, 5f));

    [Fact]
    public void ClampZoom_AboveMax_ReturnsMax()
        => Assert.Equal(GanttMetrics.MaxPixelsPerDay, GanttMetrics.ClampZoom(1000f, 5f));

    [Fact]
    public void ClampZoom_InRange_Unchanged()
        => Assert.Equal(15f, GanttMetrics.ClampZoom(15f, 5f));

    [Fact]
    public void ClampZoom_FitEqualsMax_AlwaysReturnsMax()
        => Assert.Equal(GanttMetrics.MaxPixelsPerDay, GanttMetrics.ClampZoom(15f, GanttMetrics.MaxPixelsPerDay));
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `"$DOTNET" test MppViewer.sln -c Release --filter "FullyQualifiedName~GanttMetricsTests"`
Expected: compile error / FAIL — `ClampZoom` does not exist.

- [ ] **Step 3: Implement ClampZoom**

In `src/MppViewer/Controls/GanttMetrics.cs`, add this method immediately after the existing `ClampPixelsPerDay` method:

```csharp
    /// <summary>
    /// Clamp a candidate scale into the zoom range whose lower bound is the fit-to-width
    /// scale: [fitPixelsPerDay, MaxPixelsPerDay]. Zooming out therefore never goes below the
    /// point at which the whole project fills the viewport. FitPixelsPerDay returns a value
    /// in [Min, Max], so fitPixelsPerDay &lt;= MaxPixelsPerDay and the clamp bounds are valid.
    /// </summary>
    public static float ClampZoom(float candidate, float fitPixelsPerDay)
        => Math.Clamp(candidate, fitPixelsPerDay, MaxPixelsPerDay);
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `"$DOTNET" test MppViewer.sln -c Release --filter "FullyQualifiedName~GanttMetricsTests"`
Expected: PASS (existing 11 + 4 new = 15).

- [ ] **Step 5: Commit**

```bash
git add src/MppViewer/Controls/GanttMetrics.cs tests/MppViewer.Tests/Controls/GanttMetricsTests.cs
git commit -m "feat: add ClampZoom (fit-to-width zoom floor) to GanttMetrics

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: Refactor zoom engine in GanttControl (fit floor + ZoomIn/ZoomOut)

**Files:**
- Modify: `src/MppViewer/Controls/GanttControl.cs`

Context: today the private `ZoomAt(int cursorX, int wheelDelta)` does the wheel-driven zoom, clamping with `ClampPixelsPerDay` (fixed `[Min,Max]`). We refactor it into a generic `ApplyZoom(int anchorX, int notches)` so the wheel AND new toolbar buttons feed the same engine, change the floor to the fit scale via `ClampZoom`, and clamp the start scale in `Load`. No unit tests (WinForms isn't unit-tested here) — verify by build + full test run.

The current relevant code (for reference) is: a field `private float _pixelsPerDay = 15f;` (line ~18) with `private const int WheelNotchDelta = 120;` just below; the `ZoomAt` method (lines ~104–128); `OnMouseWheel` Ctrl branch calling `ZoomAt(e.X, e.Delta)` (lines ~187–192); and `Load` computing `_totalWidth` from `_pixelsPerDay` (lines ~159–172).

- [ ] **Step 1: Introduce a DefaultPixelsPerDay constant**

Change the field declaration (currently `private float _pixelsPerDay = 15f;`) to:

```csharp
    private const float DefaultPixelsPerDay = 15f;
    private float _pixelsPerDay = DefaultPixelsPerDay;
```

- [ ] **Step 2: Replace ZoomAt with ApplyZoom + ZoomIn + ZoomOut**

Replace the entire `ZoomAt` method (the one with the XML doc starting "Zoom osi czasu względem punktu pod kursorem" and ending at its closing `}`) with:

```csharp
    /// <summary>
    /// Zoom osi czasu wokół punktu zaczepienia (anchorX w pikselach ekranu): punkt osi pod
    /// anchorX zostaje w miejscu, reszta rozsuwa/zsuwa się wokół niego. notches = liczba
    /// "ząbków" (dodatnie = przybliżenie, ujemne = oddalenie). Dolny limit = skala "fit to
    /// width", górny = MaxPixelsPerDay. Zmienia tylko skalę poziomą — geometria wierszy
    /// (sterowana przez tabelę) pozostaje nietknięta.
    /// </summary>
    private void ApplyZoom(int anchorX, int notches)
    {
        if (_tasks.Count == 0) return;

        float oldPpd = _pixelsPerDay;
        float factor = (float)Math.Pow(GanttMetrics.ZoomStep, notches);
        float fitPpd = GanttMetrics.FitPixelsPerDay(Width, _projectStart, _projectEnd);
        float newPpd = GanttMetrics.ClampZoom(oldPpd * factor, fitPpd);
        if (newPpd == oldPpd) return;                               // już na granicy zakresu

        _pixelsPerDay = newPpd;
        _totalWidth = GanttMetrics.TotalWidth(_projectStart, _projectEnd, _pixelsPerDay);
        _scrollOffsetX = GanttMetrics.ZoomedScrollOffset(anchorX, _scrollOffsetX, oldPpd, newPpd);

        RecalcHScroll();
        ClampScrollOffset();
        Invalidate();
    }

    /// <summary>Przybliża oś czasu o jeden ząbek, zaczepiając zoom na środku widoku.</summary>
    public void ZoomIn() => ApplyZoom(Width / 2, +1);

    /// <summary>Oddala oś czasu o jeden ząbek (nie schodzi poniżej "fit to width").</summary>
    public void ZoomOut() => ApplyZoom(Width / 2, -1);
```

- [ ] **Step 3: Update OnMouseWheel to call ApplyZoom**

In `OnMouseWheel`, replace the Ctrl branch:
```csharp
        // Ctrl + kółko = zoom osi czasu (zaczepiony pod kursorem). Bez Ctrl = scroll pionowy.
        if ((ModifierKeys & Keys.Control) == Keys.Control)
        {
            ZoomAt(e.X, e.Delta);
            return;
        }
```
with:
```csharp
        // Ctrl + kółko = zoom osi czasu (zaczepiony pod kursorem). Bez Ctrl = scroll pionowy.
        if ((ModifierKeys & Keys.Control) == Keys.Control)
        {
            // Myszy/touchpady wysyłające Delta < jednego ząbka dają notches == 0 → bez zoomu,
            // dopóki skumulowane przewinięcie nie przekroczy ząbka. To zachowanie jest zamierzone.
            ApplyZoom(e.X, e.Delta / WheelNotchDelta);
            return;
        }
```

- [ ] **Step 4: Clamp the start scale in Load**

In `Load`, insert a line so the initial scale respects the fit floor. Change:
```csharp
        _taskRowIndex = tasks.Select((t, i) => (t.Id, i))
                             .ToDictionary(x => x.Id, x => x.i);

        _totalWidth = GanttMetrics.TotalWidth(_projectStart, _projectEnd, _pixelsPerDay);
```
to:
```csharp
        _taskRowIndex = tasks.Select((t, i) => (t.Id, i))
                             .ToDictionary(x => x.Id, x => x.i);

        // Skala startowa = domyślna, ale nie poniżej "fit to width" (krótki projekt, gdzie
        // fit wypada powyżej 15px/dzień, ładuje się od razu dopasowany — bez skoku przy
        // pierwszym oddaleniu). Gdy Width jeszcze nieznane, FitPixelsPerDay schodzi do progu
        // czytelności i start zostaje DefaultPixelsPerDay.
        _pixelsPerDay = GanttMetrics.ClampZoom(
            DefaultPixelsPerDay, GanttMetrics.FitPixelsPerDay(Width, _projectStart, _projectEnd));
        _totalWidth = GanttMetrics.TotalWidth(_projectStart, _projectEnd, _pixelsPerDay);
```

- [ ] **Step 5: Build and run the full test suite**

- `"$DOTNET" build MppViewer.sln -c Release` → expect 0 errors, 0 warnings.
- `"$DOTNET" test MppViewer.sln -c Release` → expect all pass (15). Confirm there is no remaining reference to the old method name `ZoomAt` (`grep -n "ZoomAt" src/MppViewer/Controls/GanttControl.cs` → no hits).

- [ ] **Step 6: Commit**

```bash
git add src/MppViewer/Controls/GanttControl.cs
git commit -m "feat: zoom-out floor = fit scale; ApplyZoom engine with ZoomIn/ZoomOut

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: Single-toolbar layout in MainForm (drop menu, add About + Ctrl+O)

**Files:**
- Modify: `src/MppViewer/MainForm.cs`

Context: `MainForm` currently builds a `MenuStrip` in `BuildMenu()` (Open…, Exit, right-aligned GitHub) plus a `ToolStrip` in `BuildToolbar()` (Fit to width + resource filter). We remove the menu, put every action on the toolbar, preserve `Ctrl+O` at the form level (it used to come from the menu item), and add an About `MessageBox`. `GanttControl.ZoomIn()`/`ZoomOut()`/`ZoomToFit()` (public) exist from Task 2. `OnOpenClick(object?, EventArgs)`, `OpenUrl(string)`, and `RepoUrl` already exist. No unit tests — verify by build. WinForms docking note: with the menu gone the `ToolStrip` becomes the only top-docked control; keep the `Dock.Fill` `SplitContainer` added first so it fills the center.

- [ ] **Step 1: Drop the BuildMenu call and enable KeyPreview in the constructor**

In the constructor, replace:
```csharp
        // Kolejność dodawania determinuje dokowanie: kontrolka Dock.Fill musi trafić
        // do Controls jako pierwsza (najniższy z-order), aby menu (Top) i status (Bottom)
        // najpierw zarezerwowały swoje krawędzie, zamiast zostać przykryte przez Fill.
        BuildLayout();
        BuildStatusBar();
        BuildToolbar();
        BuildMenu();   // dodawane ostatnie → dokowane najpierw → zostaje na samej górze
```
with:
```csharp
        // KeyPreview pozwala formularzowi przechwycić Ctrl+O zanim trafi do kontrolki
        // (skrót przeszedł tu z dawnej pozycji menu po usunięciu paska menu).
        KeyPreview = true;

        // Kolejność dodawania determinuje dokowanie: kontrolka Dock.Fill musi trafić
        // do Controls jako pierwsza (najniższy z-order), aby pasek narzędzi (Top) i status
        // (Bottom) zarezerwowały swoje krawędzie, zamiast zostać przykryte przez Fill.
        BuildLayout();
        BuildStatusBar();
        BuildToolbar();   // jedyna kontrolka dokowana na górze (brak paska menu)
```

- [ ] **Step 2: Delete the BuildMenu method**

Remove the entire `BuildMenu()` method (the one with the comment "Pozycje najwyższego poziomu bez rodzica" that creates `menuStrip`, `openItem`, `exitItem`, `githubItem` and sets `MainMenuStrip`). Keep `OpenUrl` (it is still used by the GitHub toolbar button). Delete only `BuildMenu`.

- [ ] **Step 3: Rebuild BuildToolbar with all actions**

Replace the entire `BuildToolbar()` method with:

```csharp
    private void BuildToolbar()
    {
        _toolbar.GripStyle = ToolStripGripStyle.Hidden;

        // Lewa strona: plik | widok (zoom) | filtr.
        var openButton = new ToolStripButton("Open…") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        openButton.Click += OnOpenClick;

        var zoomOutButton = new ToolStripButton("Zoom −") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        zoomOutButton.Click += (_, __) => _gantt.ZoomOut();
        var zoomInButton = new ToolStripButton("Zoom +") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        zoomInButton.Click += (_, __) => _gantt.ZoomIn();
        var fitButton = new ToolStripButton("Fit to width") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        fitButton.Click += (_, __) => _gantt.ZoomToFit();

        _resourceCombo.DropDownStyle = ComboBoxStyle.DropDownList;  // tylko wybór z listy, bez wpisywania
        _resourceCombo.AutoSize = false;
        _resourceCombo.Width = 220;
        _resourceCombo.SelectedIndexChanged += OnResourceFilterChanged;

        // Prawa strona (Alignment.Right): pierwszy dodany ląduje najbardziej z prawej,
        // więc dodajemy Exit, GitHub, About → wizualnie od lewej: About | GitHub | Exit.
        var exitButton = new ToolStripButton("Exit")
        { DisplayStyle = ToolStripItemDisplayStyle.Text, Alignment = ToolStripItemAlignment.Right };
        exitButton.Click += (_, __) => Close();
        var githubButton = new ToolStripButton("GitHub")
        { DisplayStyle = ToolStripItemDisplayStyle.Text, Alignment = ToolStripItemAlignment.Right };
        githubButton.Click += (_, __) => OpenUrl(RepoUrl);
        var aboutButton = new ToolStripButton("About")
        { DisplayStyle = ToolStripItemDisplayStyle.Text, Alignment = ToolStripItemAlignment.Right };
        aboutButton.Click += (_, __) => ShowAbout();

        _toolbar.Items.Add(openButton);
        _toolbar.Items.Add(new ToolStripSeparator());
        _toolbar.Items.Add(zoomOutButton);
        _toolbar.Items.Add(zoomInButton);
        _toolbar.Items.Add(fitButton);
        _toolbar.Items.Add(new ToolStripSeparator());
        _toolbar.Items.Add(new ToolStripLabel("Show assigned to:"));
        _toolbar.Items.Add(_resourceCombo);
        _toolbar.Items.Add(exitButton);
        _toolbar.Items.Add(githubButton);
        _toolbar.Items.Add(aboutButton);
        Controls.Add(_toolbar);
    }

    private void ShowAbout()
    {
        var v = GetType().Assembly.GetName().Version;
        string version = v is null ? "" : $" v{v.Major}.{v.Minor}.{v.Build}";
        MessageBox.Show(
            $"MPP Viewer{version}\n\n" +
            "A portable, read-only viewer for Microsoft Project (.mpp) files.\n\n" +
            "MIT License\n" +
            RepoUrl,
            "About MPP Viewer", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
```

- [ ] **Step 4: Add the form-level Ctrl+O handler**

Add this method to `MainForm` (e.g. immediately after `OnShown`):

```csharp
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        // Ctrl+O = otwórz plik (skrót przeniesiony z dawnego menu).
        if (e.Control && e.KeyCode == Keys.O)
        {
            OnOpenClick(this, EventArgs.Empty);
            e.Handled = true;
        }
    }
```

- [ ] **Step 5: Build and run the full test suite**

- `"$DOTNET" build MppViewer.sln -c Release` → expect 0 errors, 0 warnings.
- `"$DOTNET" test MppViewer.sln -c Release` → expect all pass (15).
- Confirm no leftover menu references: `grep -n "BuildMenu\|MenuStrip\|MainMenuStrip" src/MppViewer/MainForm.cs` → no hits.

- [ ] **Step 6: Commit**

```bash
git add src/MppViewer/MainForm.cs
git commit -m "feat: single-toolbar layout — drop menu bar, add Zoom -/+, About, form-level Ctrl+O

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## After all tasks

- Full suite once more: `"$DOTNET" test MppViewer.sln -c Release` — expect all green.
- Smoke test on Windows (owner): toolbar shows `Open… | Zoom − Zoom + Fit to width | Show assigned to:[▼] … About GitHub Exit`; `Ctrl+O` opens a file; Zoom −/+ buttons zoom about the center; Ctrl+wheel zooms about the cursor; zooming out (wheel or button) **stops at the fit scale** and matches the "Fit to width" button; About shows a dialog with the version; Exit closes; drag-pan / double-click / resource filter still work; rows stay aligned with bars at every zoom.
- **Version bump + release stay deferred** (owner decides): once the owner is happy with all `dev` changes, do README + bump (`MppViewer.csproj`) in one shot, then `git merge dev --ff-only` into `main`, push, and `gh release create vX.Y.Z --target main <exe>` from the CI artifact. This release will cover both the time-axis zoom and this redesign.
