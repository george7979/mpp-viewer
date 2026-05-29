# Gantt Time-Axis Zoom Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `Ctrl`+mouse-wheel zoom of the Gantt time axis (anchored on the date under the cursor) plus a "Fit to width" toolbar button, in the existing two-pane view.

**Architecture:** Pure, WinForms-free zoom math lives in `GanttMetrics` (unit-tested). `GanttControl` turns its fixed `PixelsPerDay` constant into a mutable field and gains `ZoomAt`/`ZoomToFit`; `MainForm` adds one toolbar button that calls `ZoomToFit`. Zoom touches only the X (time) axis, so the "Gantt is slaved to the grid's row geometry" invariant is untouched.

**Tech Stack:** .NET 8, WinForms, xUnit. `dotnet` is **not** on PATH in this WSL repo — every command below uses the Windows SDK explicitly.

**Conventions for every command in this plan:**
```bash
DOTNET="/mnt/c/Program Files/dotnet/dotnet.exe"
```
- Build: `"$DOTNET" build MppViewer.sln -c Release`
- Test (all): `"$DOTNET" test MppViewer.sln -c Release`
- Test (one): `"$DOTNET" test MppViewer.sln -c Release --filter "FullyQualifiedName~<name>"`
- Run from repo root: `/home/jerzy/cursor/mpp-viewer`

---

## File Structure

| File | Responsibility | Change |
|---|---|---|
| `src/MppViewer/Controls/GanttMetrics.cs` | Pure timeline geometry | Add 3 constants + 3 pure functions |
| `tests/MppViewer.Tests/Controls/GanttMetricsTests.cs` | Unit tests for the above | Add tests |
| `src/MppViewer/Controls/GanttControl.cs` | Chart rendering + interaction | `const PixelsPerDay` → field `_pixelsPerDay`; `OnMouseWheel` Ctrl branch; `ZoomAt`, `ZoomToFit`, `ClampScrollOffset` |
| `src/MppViewer/MainForm.cs` | Window + toolbar wiring | Add "Fit to width" `ToolStripButton` |
| `README.md` | User docs | Features bullet + Usage step + version note |
| `src/MppViewer/MppViewer.csproj` | Version | Bump `1.1.4` → `1.2.0` |

---

## Task 1: Zoom math in GanttMetrics (pure, unit-tested)

**Files:**
- Modify: `src/MppViewer/Controls/GanttMetrics.cs`
- Test: `tests/MppViewer.Tests/Controls/GanttMetricsTests.cs`

Context: `GanttMetrics` is a static class of pure functions over the timeline. `DateToX(date, start, ppd) = (date-start).TotalDays * ppd` is **linear in `ppd`**, which is what makes cursor-anchored zoom a single multiply. This task adds the math only; no WinForms.

- [ ] **Step 1: Write the failing tests**

Append to `tests/MppViewer.Tests/Controls/GanttMetricsTests.cs`, inside the existing `GanttMetricsTests` class (before the closing `}`):

```csharp
    [Fact]
    public void ClampPixelsPerDay_BelowMin_ReturnsMin()
        => Assert.Equal(GanttMetrics.MinPixelsPerDay, GanttMetrics.ClampPixelsPerDay(0.5f));

    [Fact]
    public void ClampPixelsPerDay_AboveMax_ReturnsMax()
        => Assert.Equal(GanttMetrics.MaxPixelsPerDay, GanttMetrics.ClampPixelsPerDay(1000f));

    [Fact]
    public void ClampPixelsPerDay_InRange_Unchanged()
        => Assert.Equal(15f, GanttMetrics.ClampPixelsPerDay(15f));

    [Fact]
    public void ZoomedScrollOffset_ZoomIn_KeepsCursorDateFixed()
    {
        int cursorX = 300, oldOffset = 120;
        float oldPpd = 15f, newPpd = 15f * 1.2f;

        int newOffset = GanttMetrics.ZoomedScrollOffset(cursorX, oldOffset, oldPpd, newPpd);

        // The content point that was under the cursor must land back at cursorX.
        float newContentX = (cursorX + oldOffset) * (newPpd / oldPpd);
        float screenX = newContentX - newOffset;
        Assert.Equal(cursorX, screenX, precision: 0);
    }

    [Fact]
    public void ZoomedScrollOffset_ZoomOut_KeepsCursorDateFixed()
    {
        int cursorX = 300, oldOffset = 120;
        float oldPpd = 15f, newPpd = 15f / 1.2f;

        int newOffset = GanttMetrics.ZoomedScrollOffset(cursorX, oldOffset, oldPpd, newPpd);

        float newContentX = (cursorX + oldOffset) * (newPpd / oldPpd);
        float screenX = newContentX - newOffset;
        Assert.Equal(cursorX, screenX, precision: 0);
    }

    [Fact]
    public void FitPixelsPerDay_NormalProject_FitsViewport()
    {
        // 364 days, viewport 2000px, 16px margin → (2000-16)/364 ≈ 5.45, within range.
        float ppd = GanttMetrics.FitPixelsPerDay(2000, Start, End);
        Assert.Equal(5.45f, ppd, precision: 2);
    }

    [Fact]
    public void FitPixelsPerDay_VeryLongProject_ClampsToMin()
    {
        var farEnd = Start.AddYears(30);
        float ppd = GanttMetrics.FitPixelsPerDay(1000, Start, farEnd);
        Assert.Equal(GanttMetrics.MinPixelsPerDay, ppd);
    }

    [Fact]
    public void FitPixelsPerDay_ZeroSpan_NoDivideByZero()
    {
        float ppd = GanttMetrics.FitPixelsPerDay(1000, Start, Start);
        Assert.True(ppd >= GanttMetrics.MinPixelsPerDay && ppd <= GanttMetrics.MaxPixelsPerDay);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `"$DOTNET" test MppViewer.sln -c Release --filter "FullyQualifiedName~GanttMetricsTests"`
Expected: compile error / FAIL — `MinPixelsPerDay`, `ClampPixelsPerDay`, `ZoomedScrollOffset`, `FitPixelsPerDay` do not exist yet.

- [ ] **Step 3: Implement the pure functions**

Replace the entire contents of `src/MppViewer/Controls/GanttMetrics.cs` with:

```csharp
namespace MppViewer.Controls;

public static class GanttMetrics
{
    /// <summary>Lower bound for the time-axis scale (≈ multi-year overview).</summary>
    public const float MinPixelsPerDay = 2f;
    /// <summary>Upper bound for the time-axis scale (≈ day-level detail).</summary>
    public const float MaxPixelsPerDay = 60f;
    /// <summary>Multiplicative zoom factor per mouse-wheel notch.</summary>
    public const float ZoomStep = 1.2f;
    /// <summary>Padding kept on the right when fitting the whole project to the viewport.</summary>
    private const float FitMargin = 16f;

    public static float DateToX(DateTime date, DateTime projectStart, float pixelsPerDay)
        => (float)((date - projectStart).TotalDays * pixelsPerDay);

    public static int TotalWidth(DateTime projectStart, DateTime projectEnd, float pixelsPerDay)
        => (int)((projectEnd - projectStart).TotalDays * pixelsPerDay);

    /// <summary>Clamp a candidate scale into the allowed range.</summary>
    public static float ClampPixelsPerDay(float pixelsPerDay)
        => Math.Clamp(pixelsPerDay, MinPixelsPerDay, MaxPixelsPerDay);

    /// <summary>
    /// New (unclamped) horizontal scroll offset that keeps the content point under the
    /// cursor fixed on screen while the scale changes from oldPpd to newPpd. DateToX is
    /// linear in pixelsPerDay, so the content X of a fixed date scales by newPpd/oldPpd.
    /// </summary>
    public static int ZoomedScrollOffset(int cursorX, int oldScrollOffsetX, float oldPpd, float newPpd)
    {
        float absX = cursorX + oldScrollOffsetX;        // screen → content
        float k = newPpd / oldPpd;
        return (int)Math.Round(absX * k - cursorX);
    }

    /// <summary>
    /// Scale that fits [start, end] into viewportWidth (minus a small margin), clamped to
    /// [Min, Max]. Returns MaxPixelsPerDay for a non-positive day span (no divide by zero).
    /// </summary>
    public static float FitPixelsPerDay(int viewportWidth, DateTime start, DateTime end)
    {
        double days = (end - start).TotalDays;
        if (days <= 0) return MaxPixelsPerDay;
        float ppd = (float)((viewportWidth - FitMargin) / days);
        return ClampPixelsPerDay(ppd);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `"$DOTNET" test MppViewer.sln -c Release --filter "FullyQualifiedName~GanttMetricsTests"`
Expected: PASS (the 3 original + 8 new tests).

- [ ] **Step 5: Commit**

```bash
git add src/MppViewer/Controls/GanttMetrics.cs tests/MppViewer.Tests/Controls/GanttMetricsTests.cs
git commit -m "feat: add time-axis zoom math to GanttMetrics

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: Wire zoom into GanttControl

**Files:**
- Modify: `src/MppViewer/Controls/GanttControl.cs`

Context: `GanttControl` draws the chart and owns the horizontal scrollbar `_hScroll` and offset `_scrollOffsetX`. `RecalcHScroll()` recomputes the scrollbar range from `_totalWidth` and `Width`. Today the scale is a `const`; this task makes it a field and adds zoom. No unit tests (WinForms is not unit-tested in this repo — verify by build). `DateToX`/`TotalWidth` are called with the scale; all three call sites must switch to the new field.

- [ ] **Step 1: Turn the scale constant into a field**

In `src/MppViewer/Controls/GanttControl.cs`, change the declaration (currently around line 18):

```csharp
    private const float PixelsPerDay = 15f;
```
to:
```csharp
    private float _pixelsPerDay = 15f;
```

- [ ] **Step 2: Update the three call sites**

In `ScrollToDate` change:
```csharp
        float absX = GanttMetrics.DateToX(date, _projectStart, PixelsPerDay);
```
to:
```csharp
        float absX = GanttMetrics.DateToX(date, _projectStart, _pixelsPerDay);
```

In `Load` change:
```csharp
        _totalWidth = GanttMetrics.TotalWidth(_projectStart, _projectEnd, PixelsPerDay);
```
to:
```csharp
        _totalWidth = GanttMetrics.TotalWidth(_projectStart, _projectEnd, _pixelsPerDay);
```

In the private `DateToX` helper change:
```csharp
    private float DateToX(DateTime d)
        => GanttMetrics.DateToX(d, _projectStart, PixelsPerDay) - _scrollOffsetX;
```
to:
```csharp
    private float DateToX(DateTime d)
        => GanttMetrics.DateToX(d, _projectStart, _pixelsPerDay) - _scrollOffsetX;
```

- [ ] **Step 3: Add the Ctrl branch to OnMouseWheel**

Replace the existing `OnMouseWheel` method:

```csharp
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (_grid == null || _grid.RowCount == 0) return;

        int delta = -Math.Sign(e.Delta) * 3;
        int first = Math.Max(0, _grid.FirstDisplayedScrollingRowIndex) + delta;
        first = Math.Max(0, Math.Min(first, _grid.RowCount - 1));
        try { _grid.FirstDisplayedScrollingRowIndex = first; }
        catch (InvalidOperationException) { /* wiersz chwilowo niedostępny */ }
    }
```
with:
```csharp
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);

        // Ctrl + kółko = zoom osi czasu (zaczepiony pod kursorem). Bez Ctrl = scroll pionowy.
        if ((ModifierKeys & Keys.Control) == Keys.Control)
        {
            ZoomAt(e.X, e.Delta);
            return;
        }

        if (_grid == null || _grid.RowCount == 0) return;

        int delta = -Math.Sign(e.Delta) * 3;
        int first = Math.Max(0, _grid.FirstDisplayedScrollingRowIndex) + delta;
        first = Math.Max(0, Math.Min(first, _grid.RowCount - 1));
        try { _grid.FirstDisplayedScrollingRowIndex = first; }
        catch (InvalidOperationException) { /* wiersz chwilowo niedostępny */ }
    }
```

- [ ] **Step 4: Add ZoomAt, ZoomToFit and ClampScrollOffset**

Insert these three methods immediately after the `ScrollToDate` method (they are "camera" operations like `ScrollToDate`):

```csharp
    /// <summary>
    /// Zoom osi czasu względem punktu pod kursorem: data pod kursorem zostaje w tym samym
    /// miejscu na ekranie, a reszta osi rozsuwa/zsuwa się wokół niej. Zmienia tylko skalę
    /// poziomą — geometria wierszy (sterowana przez tabelę) pozostaje nietknięta.
    /// </summary>
    private void ZoomAt(int cursorX, int wheelDelta)
    {
        if (_tasks.Count == 0) return;

        float oldPpd = _pixelsPerDay;
        int notches = wheelDelta / 120;                              // jeden ząbek kółka = 120
        float factor = (float)Math.Pow(GanttMetrics.ZoomStep, notches);
        float newPpd = GanttMetrics.ClampPixelsPerDay(oldPpd * factor);
        if (newPpd == oldPpd) return;                               // już na granicy zakresu

        _pixelsPerDay = newPpd;
        _totalWidth = GanttMetrics.TotalWidth(_projectStart, _projectEnd, _pixelsPerDay);
        _scrollOffsetX = GanttMetrics.ZoomedScrollOffset(cursorX, _scrollOffsetX, oldPpd, newPpd);

        RecalcHScroll();
        ClampScrollOffset();
        Invalidate();
    }

    /// <summary>
    /// Dobiera skalę osi czasu tak, by cały projekt zmieścił się na szerokość widoku,
    /// i resetuje przewinięcie poziome. Pion (lista zadań) nadal się przewija.
    /// </summary>
    public void ZoomToFit()
    {
        if (_tasks.Count == 0) return;

        _pixelsPerDay = GanttMetrics.FitPixelsPerDay(Width, _projectStart, _projectEnd);
        _totalWidth = GanttMetrics.TotalWidth(_projectStart, _projectEnd, _pixelsPerDay);
        _scrollOffsetX = 0;

        RecalcHScroll();
        _hScroll.Value = 0;
        Invalidate();
    }

    // Dosuwa _scrollOffsetX do prawidłowego zakresu paska i synchronizuje jego Value
    // (ZoomedScrollOffset może zwrócić wartość ujemną lub powyżej maksimum).
    private void ClampScrollOffset()
    {
        int maxValue = Math.Max(_hScroll.Minimum, _hScroll.Maximum - _hScroll.LargeChange + 1);
        _scrollOffsetX = Math.Clamp(_scrollOffsetX, _hScroll.Minimum, maxValue);
        _hScroll.Value = _scrollOffsetX;
    }
```

- [ ] **Step 5: Build to verify it compiles**

Run: `"$DOTNET" build MppViewer.sln -c Release`
Expected: Build succeeded, 0 errors. (Then `"$DOTNET" test MppViewer.sln -c Release` — all tests still pass, nothing regressed.)

- [ ] **Step 6: Commit**

```bash
git add src/MppViewer/Controls/GanttControl.cs
git commit -m "feat: Ctrl+wheel time-axis zoom and ZoomToFit in GanttControl

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: "Fit to width" toolbar button in MainForm

**Files:**
- Modify: `src/MppViewer/MainForm.cs`

Context: `BuildToolbar()` builds the existing `_toolbar` (currently just the "Show assigned to:" label + `_resourceCombo`). `_gantt` is the `GanttControl` field. A `ToolStripButton` with no image needs `DisplayStyle = Text` to show its label.

- [ ] **Step 1: Add the button**

Replace the body of `BuildToolbar()`:

```csharp
    private void BuildToolbar()
    {
        _toolbar.GripStyle = ToolStripGripStyle.Hidden;
        _resourceCombo.DropDownStyle = ComboBoxStyle.DropDownList;  // tylko wybór z listy, bez wpisywania
        _resourceCombo.AutoSize = false;
        _resourceCombo.Width = 220;
        _resourceCombo.SelectedIndexChanged += OnResourceFilterChanged;

        _toolbar.Items.Add(new ToolStripLabel("Show assigned to:"));
        _toolbar.Items.Add(_resourceCombo);
        Controls.Add(_toolbar);
    }
```
with:
```csharp
    private void BuildToolbar()
    {
        _toolbar.GripStyle = ToolStripGripStyle.Hidden;

        // Dopasuj oś czasu wykresu do szerokości okna (odpowiednik "Entire Project").
        var fitButton = new ToolStripButton("Fit to width")
        {
            DisplayStyle = ToolStripItemDisplayStyle.Text
        };
        fitButton.Click += (_, __) => _gantt.ZoomToFit();

        _resourceCombo.DropDownStyle = ComboBoxStyle.DropDownList;  // tylko wybór z listy, bez wpisywania
        _resourceCombo.AutoSize = false;
        _resourceCombo.Width = 220;
        _resourceCombo.SelectedIndexChanged += OnResourceFilterChanged;

        _toolbar.Items.Add(fitButton);
        _toolbar.Items.Add(new ToolStripSeparator());
        _toolbar.Items.Add(new ToolStripLabel("Show assigned to:"));
        _toolbar.Items.Add(_resourceCombo);
        Controls.Add(_toolbar);
    }
```

- [ ] **Step 2: Build to verify it compiles**

Run: `"$DOTNET" build MppViewer.sln -c Release`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/MppViewer/MainForm.cs
git commit -m "feat: add Fit to width button to the toolbar

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 4: Docs and version bump

**Files:**
- Modify: `README.md`
- Modify: `src/MppViewer/MppViewer.csproj`

Context: README has a `## Features` bulleted list and a `## Usage` numbered list. The early-version note near the top reads `> **Early version (1.1.4)**`. The csproj holds `<Version>`/`<AssemblyVersion>`/`<FileVersion>`. This is a feature addition → minor bump to **1.2.0**.

- [ ] **Step 1: Add a Features bullet**

In `README.md`, in the `## Features` list, after the line that begins `- **Synchronized view**` insert this new bullet:

```markdown
- **Zoom the timeline** — hold `Ctrl` and scroll the mouse wheel over the chart to stretch or compress the time axis; the date under the cursor stays in place. The **Fit to width** button scales the whole project to the window.
```

- [ ] **Step 2: Add a Usage step**

In `README.md`, in the `## Usage` numbered list, replace the existing step:

```markdown
3. **Drag the chart to pan it** — grab the Gantt area with the mouse (or swipe with a finger on a touchscreen) and drag in any direction: left/right scrolls the timeline, up/down scrolls the rows.
4. **Double-click a row** to scroll the timeline to that task.
5. Use **Show assigned to:** at the top to highlight one person's tasks (others are greyed out); pick *(everyone)* to show everyone again.
6. Drag the splitter between the panes to resize them.
```
with (renumbered, with the new zoom step inserted as 4):
```markdown
3. **Drag the chart to pan it** — grab the Gantt area with the mouse (or swipe with a finger on a touchscreen) and drag in any direction: left/right scrolls the timeline, up/down scrolls the rows.
4. **Zoom the time axis** — hold `Ctrl` and scroll the mouse wheel over the chart; the date under the cursor stays put. Click **Fit to width** to scale the whole project to the window.
5. **Double-click a row** to scroll the timeline to that task.
6. Use **Show assigned to:** at the top to highlight one person's tasks (others are greyed out); pick *(everyone)* to show everyone again.
7. Drag the splitter between the panes to resize them.
```

- [ ] **Step 3: Update the early-version note**

In `README.md` change:
```markdown
> **Early version (1.1.4)** — a young project under active development. Core viewing works, but the UI and behaviour may change between releases, and some `.mpp` features are not yet rendered. Feedback is welcome.
```
to:
```markdown
> **Early version (1.2.0)** — a young project under active development. Core viewing works, but the UI and behaviour may change between releases, and some `.mpp` features are not yet rendered. Feedback is welcome.
```

- [ ] **Step 4: Bump the version in the csproj**

In `src/MppViewer/MppViewer.csproj` change:
```xml
    <Version>1.1.4</Version>
    <AssemblyVersion>1.1.4.0</AssemblyVersion>
    <FileVersion>1.1.4.0</FileVersion>
```
to:
```xml
    <Version>1.2.0</Version>
    <AssemblyVersion>1.2.0.0</AssemblyVersion>
    <FileVersion>1.2.0.0</FileVersion>
```

- [ ] **Step 5: Build to verify the version compiles and the footer picks it up**

Run: `"$DOTNET" build MppViewer.sln -c Release`
Expected: Build succeeded. (The status-bar footer reads the version from the assembly, so it will now show `v1.2.0`.)

- [ ] **Step 6: Commit**

```bash
git add README.md src/MppViewer/MppViewer.csproj
git commit -m "docs: document time-axis zoom; bump version to 1.2.0

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## After all tasks

- Run the full suite once more: `"$DOTNET" test MppViewer.sln -c Release` — expect all green.
- Smoke test on Windows (manual, owner): open a real `.mpp`; `Ctrl`+wheel zooms in/out with the date under the cursor staying put; plain wheel still scrolls; **Fit to width** frames the whole project; drag-pan and the resource filter still work.
- **Release flow (needs owner consent for push/release):** push `dev` → wait for CI artifact → smoke test the CI exe → `git merge dev --ff-only` into `main` → push → `gh release create v1.2.0 --target main <exe>` with notes.
