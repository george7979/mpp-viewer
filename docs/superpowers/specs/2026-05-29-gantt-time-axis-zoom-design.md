# Gantt Time-Axis Zoom — Design Spec

**Date:** 2026-05-29
**Status:** Approved

---

## Goal

Let the user zoom the Gantt chart's **time axis** (horizontal) in the existing two-pane view: `Ctrl` + mouse wheel stretches/compresses days, anchored on the date under the cursor, and a **"Fit to width"** toolbar button scales the whole project to the visible chart width. The vertical axis (task list) keeps its existing scroll behavior — exactly the MS Project model.

---

## Background & rationale

The original idea was a separate fullscreen chart view that would host zoom + pan. Brainstorming reduced the scope: zoom that only touches the **time axis** is orthogonal to the table (whose columns are text, not a timeline), so there is no synchronization to protect and **no separate view is needed**. Drag-pan in all directions already exists (shipped in 1.1.3). The only missing piece is zoom.

This matches how MS Project (and ProjectLibre, GanttProject) behave: the timescale zoom affects the horizontal axis only; rows always have fixed height and are scrolled vertically. There is no "fit all rows vertically" — vertical is always scroll.

---

## Constraints

- No new window / view — extend the existing `GanttControl` in the two-pane layout.
- Must not touch the "Gantt is slaved to the grid's row geometry" invariant (zoom is X-axis only).
- Pure, WinForms-free geometry stays in `GanttMetrics` and is unit-tested; WinForms glue stays in `GanttControl`/`MainForm` and is not unit-tested (repo convention).

---

## Behavior

### Ctrl + mouse wheel — zoom the time axis

- Plain mouse wheel over the chart keeps its current behavior (vertical scroll of both panes).
- `Ctrl` + wheel zooms the time axis instead. Wheel up = zoom in (days wider), wheel down = zoom out (days narrower).
- **Cursor anchor:** the date currently under the mouse cursor stays at the same screen X while the rest of the axis expands/contracts around it.
- **Zoom step:** multiplicative — `1.2` per wheel notch (so zoom-in and zoom-out feel symmetric).
- **Zoom limits:** `PixelsPerDay` is clamped to `[2f, 60f]` (≈ multi-year overview ↔ day-level detail).
- When there are no tasks loaded, the zoom is a no-op.

### "Fit to width" — scale the whole project to the viewport

- A `ToolStripButton` labeled **"Fit to width"** in the existing toolbar (next to "Show assigned to:").
- Sets `PixelsPerDay` so the full project span (`ProjectStart`→`ProjectFinish`) fits the visible chart width, then resets the horizontal scroll to 0.
- Result is clamped to the same `[2f, 60f]` range: a very long project clamps at `2f` (maximally zoomed out; the rest is reached by scrolling) rather than rendering an unreadable fog.
- When there are no tasks loaded, the button is a no-op.

---

## Architecture & changes

One-directional flow, unchanged: `MainForm` (toolbar) → public method on `GanttControl` → pure math in `GanttMetrics`.

### `Controls/GanttMetrics.cs` — new pure functions (unit-tested)

```csharp
public const float MinPixelsPerDay = 2f;
public const float MaxPixelsPerDay = 60f;
public const float ZoomStep = 1.2f;

// Clamp a candidate scale into the allowed range.
public static float ClampPixelsPerDay(float pixelsPerDay);

// New (unclamped) horizontal scroll offset that keeps the content point under
// the cursor fixed on screen while the scale changes from oldPpd to newPpd.
//   absX = cursorX + oldScrollOffsetX          (screen → content)
//   k    = newPpd / oldPpd
//   result = absX * k - cursorX
public static int ZoomedScrollOffset(int cursorX, int oldScrollOffsetX, float oldPpd, float newPpd);

// Scale that fits [start, end] into viewportWidth (minus a small margin),
// clamped to [Min, Max]. Guards against a zero/negative day span.
public static float FitPixelsPerDay(int viewportWidth, DateTime start, DateTime end);
```

`DateToX` is linear in `pixelsPerDay` (`X = days × ppd`), so for a fixed date the content X scales by `k = newPpd/oldPpd`. That linearity is what makes `ZoomedScrollOffset` a single multiply.

### `Controls/GanttControl.cs`

- Change `private const float PixelsPerDay = 15f;` to `private float _pixelsPerDay = 15f;`. Update the three call sites (`ScrollToDate`, `Load`, `DateToX`) to use the field.
- `OnMouseWheel`: if `(ModifierKeys & Keys.Control) == Keys.Control` → `ZoomAt(e.X, e.Delta)`; otherwise the existing vertical-scroll forwarding to the grid.
- New `private void ZoomAt(int cursorX, int wheelDelta)`:
  1. no-op if `_tasks.Count == 0`;
  2. `float oldPpd = _pixelsPerDay;`
  3. `int notches = wheelDelta / 120;` `float factor = (float)Math.Pow(ZoomStep, notches);`
  4. `_pixelsPerDay = GanttMetrics.ClampPixelsPerDay(oldPpd * factor);`
  5. if unchanged (already at a limit) → return;
  6. `_totalWidth = GanttMetrics.TotalWidth(_projectStart, _projectEnd, _pixelsPerDay);`
  7. `_scrollOffsetX = GanttMetrics.ZoomedScrollOffset(cursorX, _scrollOffsetX, oldPpd, _pixelsPerDay);`
  8. `RecalcHScroll();` then clamp `_scrollOffsetX`/`_hScroll.Value` into the valid scroll range;
  9. `Invalidate();`
- New `public void ZoomToFit()`:
  1. no-op if `_tasks.Count == 0`;
  2. `_pixelsPerDay = GanttMetrics.FitPixelsPerDay(Width, _projectStart, _projectEnd);`
  3. `_totalWidth = GanttMetrics.TotalWidth(_projectStart, _projectEnd, _pixelsPerDay);`
  4. `_scrollOffsetX = 0;`
  5. `RecalcHScroll();` `Invalidate();`

### `MainForm.cs`

- In `BuildToolbar`, add a `ToolStripButton("Fit to width")` whose `Click` calls `_gantt.ZoomToFit()`.

---

## Out of scope (deliberate, YAGNI)

- **Separate fullscreen chart view** — dropped; not needed once zoom is time-axis only.
- **Vertical zoom / "fit all rows"** — not a thing in MS Project; vertical stays scroll.
- **Adaptive header granularity** (quarters/years when zoomed out) — the header keeps showing months. At maximum zoom-out (2 px/day ≈ 60 px/month) the "MMM yyyy" labels may crowd but remain functional.
- **Zoom via keyboard / pinch gesture** — only Ctrl+wheel and the button for now.

---

## Testing

New unit tests in `GanttMetricsTests` (no UI tests, per repo convention):

- `ClampPixelsPerDay`: below min → Min; above max → Max; in range → unchanged.
- `ZoomedScrollOffset`: after a zoom-in and a zoom-out with a concrete cursor X, the content point that was under the cursor maps back to the same `cursorX` (round-trip property).
- `FitPixelsPerDay`: a normal project fits within the viewport; a very long project clamps to Min; a zero/one-day span does not divide by zero and returns a value within `[Min, Max]`.

---

## Docs & release

- README: add a bullet under **Features** and a step under **Usage** ("Ctrl + mouse wheel over the chart zooms the time axis; the date under the cursor stays put. Use **Fit to width** to scale the whole project to the window.").
- Bump `<Version>`/`<AssemblyVersion>`/`<FileVersion>` in `MppViewer.csproj`.
- Ship via the standard flow: dev → CI artifact → smoke test on Windows → `git merge dev --ff-only` into main → `gh release create vX.Y.Z --target main <exe>`.
