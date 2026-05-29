namespace MppViewer.Controls;

public static class GanttMetrics
{
    public static float DateToX(DateTime date, DateTime projectStart, float pixelsPerDay)
        => (float)((date - projectStart).TotalDays * pixelsPerDay);

    public static int TotalWidth(DateTime projectStart, DateTime projectEnd, float pixelsPerDay)
        => (int)((projectEnd - projectStart).TotalDays * pixelsPerDay);
}
