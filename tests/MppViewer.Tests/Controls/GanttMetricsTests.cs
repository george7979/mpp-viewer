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
}
