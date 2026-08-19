using MppViewer.Controls;

namespace MppViewer.Tests.Controls;

public class GanttMetricsTests
{
    private static readonly DateTime Start = new(2025, 1, 1);
    private static readonly DateTime End = new(2025, 12, 31);
    private const float PixelsPerDay = 15f;

    [Fact]
    public void DateToX_AtProjectStart_ReturnsZero()
    {
        float x = GanttMetrics.DateToX(Start, Start, PixelsPerDay);
        Assert.Equal(0f, x);
    }

    [Fact]
    public void DateToX_OneWeekIn_Returns105()
    {
        var date = Start.AddDays(7);
        float x = GanttMetrics.DateToX(date, Start, PixelsPerDay);
        Assert.Equal(105f, x, precision: 1);
    }

    [Fact]
    public void TotalWidth_364Days_Returns5460()
    {
        int width = GanttMetrics.TotalWidth(Start, End, PixelsPerDay);
        Assert.Equal((int)(364 * PixelsPerDay), width);
    }

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

        Assert.Equal(204, newOffset);   // independently computed: round(420*1.2 - 300)

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

        Assert.Equal(50, newOffset);    // independently computed: round(420/1.2 - 300)

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
        Assert.Equal(GanttMetrics.MaxPixelsPerDay, ppd);   // documented contract: non-positive span → Max
    }

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

    // --- CenteredScrollOffset: skala + przewinięcie dla "fit to task" ---

    [Fact]
    public void CenteredScrollOffset_RangeInMiddle_PutsRangeCenterAtViewportCenter()
    {
        var rangeStart = new DateTime(2025, 6, 1);   // 151 dni od startu projektu
        var rangeEnd = new DateTime(2025, 6, 11);    // 161 dni od startu projektu
        // środek zakresu = 156 dni * 15 px = 2340; minus połowa widoku (400) = 1940
        int offset = GanttMetrics.CenteredScrollOffset(rangeStart, rangeEnd, Start, PixelsPerDay, 800);
        Assert.Equal(1940, offset);
    }

    [Fact]
    public void CenteredScrollOffset_RangeAtProjectStart_ReturnsNegative()
    {
        // Zakres przy samym początku projektu nie da się wyśrodkować — połowa widoku
        // wypada przed osią. Wynik jest ujemny; dosunięcie do zera należy do wywołującego.
        int offset = GanttMetrics.CenteredScrollOffset(Start, Start.AddDays(10), Start, PixelsPerDay, 800);
        Assert.Equal(-325, offset);
    }

    [Fact]
    public void CenteredScrollOffset_ZeroSpanMilestone_CentersOnThatPoint()
    {
        var milestone = new DateTime(2025, 3, 1);    // 59 dni od startu projektu
        // punkt = 59 * 15 = 885; minus połowa widoku (400) = 485
        int offset = GanttMetrics.CenteredScrollOffset(milestone, milestone, Start, PixelsPerDay, 800);
        Assert.Equal(485, offset);
    }
}
