# Zoom Floor + Toolbar Redesign — Design Spec

**Date:** 2026-05-29
**Status:** Approved

---

## Goal

Two related UX improvements to the Gantt time-axis zoom (which currently lives on `dev`, unreleased):

1. **Zoom-out floor = "fit to width".** The lowest zoom level becomes the scale at which the whole project fills the viewport, instead of a fixed `MinPixelsPerDay = 2`. Zooming out and clicking "Fit to width" converge on the same state.
2. **Single-toolbar layout (no menu bar)**, following best practices for a small desktop document viewer, with explicit **Zoom −/+** buttons for discoverability.

---

## Background

The time-axis zoom feature (Ctrl+wheel + "Fit to width") is implemented on `dev` but not yet released (version bump deferred). This spec refines it before release. The current UI has a flat `MenuStrip` (Open…, Exit, right-aligned GitHub) plus a `ToolStrip` (Fit to width + "Show assigned to:" filter). The user chose to drop the menu bar entirely in favor of one toolbar.

---

## A. Zoom-out floor = fit scale

Today zoom clamps `PixelsPerDay` to `[MinPixelsPerDay=2, MaxPixelsPerDay=60]`. Change the **lower** bound to the dynamic fit scale `FitPixelsPerDay(Width, start, end)`. The upper bound (`MaxPixelsPerDay`) is unchanged.

- New pure function `GanttMetrics.ClampZoom(float candidate, float fitPpd)` = `Math.Clamp(candidate, fitPpd, MaxPixelsPerDay)`. Valid because `FitPixelsPerDay` already returns a value in `[MinPixelsPerDay, MaxPixelsPerDay]`, so `fitPpd <= MaxPixelsPerDay` (lo ≤ hi always holds).
- The zoom engine computes `fitPpd = GanttMetrics.FitPixelsPerDay(Width, _projectStart, _projectEnd)` on each zoom and clamps the candidate scale through `ClampZoom`.
- The **initial** scale is also clamped: `_pixelsPerDay` starts at `ClampZoom(15f, fitPpd)` once a file is loaded. For a normal/long project `fitPpd < 15`, so the start stays 15 (current behavior). For a very short project where `fitPpd > 15`, the chart loads already fitted — avoiding a one-time "jump" on the first zoom-out. If `Width` is not yet known (0), `FitPixelsPerDay` floors at `MinPixelsPerDay`, so the start stays 15 (safe fallback).

Consequence: "Zoom −" held to the limit and the "Fit to width" button produce the same scale.

---

## B. Single-toolbar layout (remove menu bar)

Remove the `MenuStrip` and the `BuildMenu()` method. All actions move onto the existing single `ToolStrip`. Text buttons (`DisplayStyle = ToolStripItemDisplayStyle.Text`) — no icon design.

Visual order, left to right:

```
[Open…] | [Zoom −] [Zoom +] [Fit to width] | Show assigned to: [▼]   …   [About] [GitHub] [Exit]
```

- Left group: `Open…`
- Separator, then view group: `Zoom −`, `Zoom +`, `Fit to width`
- Separator, then filter: `Show assigned to:` label + the existing resource combo
- Right-aligned group (via `Alignment = ToolStripItemAlignment.Right`): `About`, `GitHub`, `Exit`

Docking: with the `MenuStrip` gone, the `ToolStrip` becomes the topmost docked control. Keep the existing add-order so the `Dock.Fill` `SplitContainer` reserves the center, the `StatusStrip` the bottom, and the `ToolStrip` the top.

---

## C. Zoom −/+ buttons and the zoom engine refactor

Extract the core of today's private `ZoomAt(int cursorX, int wheelDelta)` into `private void ApplyZoom(int anchorX, int notches)`:

- no-op if `_tasks.Count == 0`;
- `oldPpd = _pixelsPerDay`; `factor = (float)Math.Pow(GanttMetrics.ZoomStep, notches)`;
- `fitPpd = GanttMetrics.FitPixelsPerDay(Width, _projectStart, _projectEnd)`;
- `newPpd = GanttMetrics.ClampZoom(oldPpd * factor, fitPpd)`; return if `newPpd == oldPpd`;
- `_pixelsPerDay = newPpd`; recompute `_totalWidth`; `_scrollOffsetX = GanttMetrics.ZoomedScrollOffset(anchorX, _scrollOffsetX, oldPpd, newPpd)`; `RecalcHScroll(); ClampScrollOffset(); Invalidate();`

Callers:
- `OnMouseWheel` (Ctrl branch): `ApplyZoom(e.X, e.Delta / WheelNotchDelta)` — cursor-anchored. Sub-notch deltas give `notches == 0` → no-op (unchanged behavior).
- New `public void ZoomIn()` → `ApplyZoom(Width / 2, +1)` — viewport-center anchor.
- New `public void ZoomOut()` → `ApplyZoom(Width / 2, -1)` — viewport-center anchor.

The toolbar's `Zoom +`/`Zoom −` buttons call `_gantt.ZoomIn()`/`_gantt.ZoomOut()`.

---

## D. Preserve Ctrl+O

The `Ctrl+O` shortcut previously came from the menu item. With the menu gone, move it to the form: set `KeyPreview = true` and handle it in `OnKeyDown` — `if (e.Control && e.KeyCode == Keys.O) { OnOpenClick(this, EventArgs.Empty); e.Handled = true; }`. The `Open…` toolbar button calls the same `OnOpenClick`.

---

## E. About dialog

A simple `MessageBox` (no custom form). On the `About` button click, show:
- Title: `About MPP Viewer`
- Body: product name, version read from the assembly (same source as the status-bar footer), `MIT License`, and the repository URL (`RepoUrl`).

---

## F. Testing

Pure unit tests (in `GanttMetricsTests`):
- `ClampZoom`: candidate below `fitPpd` → `fitPpd`; candidate above `MaxPixelsPerDay` → `MaxPixelsPerDay`; candidate in range → unchanged; `fitPpd == MaxPixelsPerDay` (short project) → always returns `MaxPixelsPerDay`.

WinForms glue (toolbar items, About `MessageBox`, `Ctrl+O` handler, `ZoomIn`/`ZoomOut`, the Load-time clamp) is verified by a clean build and the owner's manual smoke test — no UI unit tests (repo convention).

---

## G. Out of scope (YAGNI)

- Keyboard shortcuts for zoom (`Ctrl +/−/0`) — buttons + Ctrl+wheel only for now.
- Custom About window with a clickable link — `MessageBox` is enough for an early version.
- Re-fitting automatically on window resize — the floor is recomputed per zoom from the current `Width`; no auto-refit.
- Toolbar icons — text buttons only.

---

## H. Version & release

All work lands on `dev`. The version bump stays deferred: it happens once, at release time, covering both this change and the already-implemented time-axis zoom. (The earlier plan's "Task 4 — README + bump to 1.2.0" is folded into that single eventual release.) The footer keeps showing `v1.1.4` until then.
