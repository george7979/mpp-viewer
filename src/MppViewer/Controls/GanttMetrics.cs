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
    /// Clamp a candidate scale into the zoom range whose lower bound is the fit-to-width
    /// scale: [fitPixelsPerDay, MaxPixelsPerDay]. Zooming out therefore never goes below the
    /// point at which the whole project fills the viewport. FitPixelsPerDay returns a value
    /// in [Min, Max], so fitPixelsPerDay &lt;= MaxPixelsPerDay and the clamp bounds are valid.
    /// </summary>
    public static float ClampZoom(float candidate, float fitPixelsPerDay)
        => Math.Clamp(candidate, fitPixelsPerDay, MaxPixelsPerDay);

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
