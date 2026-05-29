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
    public void RowY_FirstRow_IsHeaderHeight()
    {
        int y = GanttMetrics.RowY(rowIndex: 0, firstVisibleRow: 0, rowHeight: 22, headerHeight: 48);
        Assert.Equal(48, y);
    }

    [Fact]
    public void RowY_ScrolledByTwo_OffsetsByTwoRows()
    {
        int y = GanttMetrics.RowY(rowIndex: 3, firstVisibleRow: 2, rowHeight: 22, headerHeight: 48);
        Assert.Equal(48 + 22, y);
    }
}
