# Two-tier timeline header

## Problem

The header showed only month labels anchored at each month's first day. At high zoom a month is
wider than the viewport (60 px/day ≈ 1800 px), so scrolling into the middle of a month left the
header — and the gridlines below it — completely empty.

## Design

The 44 px header (`GanttControl.HeaderHeight`, shared with the grid) is split into two rows
without changing its height:

- **Upper tier** (22 px) — the coarse unit. Its label **sticks to the left edge** while the unit's
  start is scrolled off-screen, and is pushed out by the unit's end so it never overlaps the next one.
- **Lower tier** — the finest unit whose label still fits, chosen from the current zoom:

| Zoom | Upper | Lower |
|---|---|---|
| a day fits a day-number label | month (`MMM yyyy`) | day (`d`), weekends shaded |
| a week fits a `d MMM` label | month (`MMM yyyy`) | week (`d MMM`) |
| otherwise | year (`yyyy`) | month (`MMM`) |

Thresholds come from `MeasureString` of the labels, not fixed pixel values, so they follow
the font and DPI.

**Weeks start on Monday** (ISO 8601), regardless of the project calendar — decided by the
product owner; reading the start day from the `.mpp` calendar was judged not worth the cost.

Gridlines follow the scale: light lines on lower-tier boundaries, darker on upper-tier ones,
and shaded weekends in day view.

## Code

Pure geometry in `Controls/GanttMetrics.cs` (unit-tested): `ChooseTimescale`, `UnitStart`,
`NextUnit`, `StickyLabelX`. Drawing in `GanttControl.DrawTimelineHeader` / `DrawTimeGridlines`.
Only the visible range of units is iterated, so day view on a multi-year project stays cheap.
