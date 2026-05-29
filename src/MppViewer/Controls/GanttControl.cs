using System.Drawing.Drawing2D;
using MppViewer.Models;

namespace MppViewer.Controls;

public class GanttControl : Panel
{
    private const int HeaderHeight = 48;
    private const int RowHeight = 22;
    private const int BarPadding = 3;
    private const float PixelsPerDay = 15f;

    private IReadOnlyList<TaskItem> _tasks = Array.Empty<TaskItem>();
    private DateTime _projectStart;
    private DateTime _projectEnd;
    private readonly HScrollBar _hScroll = new();
    private int _scrollOffsetX;
    private Dictionary<int, int> _taskRowIndex = new();

    public int FirstVisibleRow { get; set; }

    public GanttControl()
    {
        DoubleBuffered = true;
        _hScroll.Dock = DockStyle.Bottom;
        _hScroll.SmallChange = 20;
        _hScroll.LargeChange = 200;
        _hScroll.Scroll += (_, __) =>
        {
            _scrollOffsetX = _hScroll.Value;
            Invalidate();
        };
        Controls.Add(_hScroll);
    }

    public void Load(IReadOnlyList<TaskItem> tasks, DateTime start, DateTime end)
    {
        _tasks = tasks;
        _projectStart = start;
        _projectEnd = end;
        _taskRowIndex = tasks.Select((t, i) => (t.Id, i))
                             .ToDictionary(x => x.Id, x => x.i);

        int totalWidth = GanttMetrics.TotalWidth(start, end, PixelsPerDay);
        _hScroll.Maximum = Math.Max(0, totalWidth);
        _hScroll.Value = 0;
        _scrollOffsetX = 0;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;

        DrawTimelineHeader(g);
        DrawTaskBars(g);
        DrawDependencies(g);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Invalidate();
    }

    private void DrawTimelineHeader(Graphics g)
    {
        g.FillRectangle(SystemBrushes.Control, 0, 0, Width, HeaderHeight);
        g.DrawLine(Pens.Gray, 0, HeaderHeight - 1, Width, HeaderHeight - 1);

        if (_tasks.Count == 0) return;

        using var monthFont = new Font(Font.FontFamily, 8f, FontStyle.Bold);
        using var dayFont = new Font(Font.FontFamily, 7f);

        var current = new DateTime(_projectStart.Year, _projectStart.Month, 1);
        while (current <= _projectEnd)
        {
            float x = GanttMetrics.DateToX(current, _projectStart, PixelsPerDay) - _scrollOffsetX;
            if (x > -100 && x < Width + 100)
            {
                g.DrawString(current.ToString("MMM yyyy"), monthFont, Brushes.Black, x + 2, 2);
                g.DrawLine(Pens.LightGray, x, 0, x, HeaderHeight);
            }
            current = current.AddMonths(1);
        }
    }

    private void DrawTaskBars(Graphics g)
    {
        for (int i = 0; i < _tasks.Count; i++)
        {
            int rowY = GanttMetrics.RowY(i, FirstVisibleRow, RowHeight, HeaderHeight);
            if (rowY + RowHeight < HeaderHeight || rowY > Height) continue;

            DrawTaskBar(g, _tasks[i], rowY);
        }
    }

    private void DrawTaskBar(Graphics g, TaskItem task, int rowY)
    {
        if (task.Start == null || task.Finish == null) return;

        float x1 = GanttMetrics.DateToX(task.Start.Value, _projectStart, PixelsPerDay) - _scrollOffsetX;
        float x2 = GanttMetrics.DateToX(task.Finish.Value, _projectStart, PixelsPerDay) - _scrollOffsetX;
        float barWidth = Math.Max(2f, x2 - x1);
        float barY = rowY + BarPadding;
        float barHeight = RowHeight - BarPadding * 2;

        // Background
        g.FillRectangle(Brushes.LightSteelBlue, x1, barY, barWidth, barHeight);

        // Progress
        float progressWidth = barWidth * task.PercentComplete / 100f;
        if (progressWidth > 0)
            g.FillRectangle(Brushes.SteelBlue, x1, barY, progressWidth, barHeight);

        // Border
        g.DrawRectangle(Pens.DarkSlateGray, x1, barY, barWidth, barHeight);
    }

    private void DrawDependencies(Graphics g)
    {
        using var pen = new Pen(Color.DarkRed, 1f);
        pen.EndCap = LineCap.ArrowAnchor;

        foreach (var task in _tasks)
        {
            if (task.Start == null) continue;
            foreach (var predId in task.PredecessorIds)
            {
                if (!_taskRowIndex.TryGetValue(predId, out int predIndex)) continue;
                var pred = _tasks[predIndex];
                if (pred.Finish == null) continue;

                int taskIndex = _taskRowIndex[task.Id];
                float fromX = GanttMetrics.DateToX(pred.Finish.Value, _projectStart, PixelsPerDay) - _scrollOffsetX;
                float fromY = GanttMetrics.RowY(predIndex, FirstVisibleRow, RowHeight, HeaderHeight) + RowHeight / 2f;
                float toX = GanttMetrics.DateToX(task.Start.Value, _projectStart, PixelsPerDay) - _scrollOffsetX;
                float toY = GanttMetrics.RowY(taskIndex, FirstVisibleRow, RowHeight, HeaderHeight) + RowHeight / 2f;

                g.DrawLine(pen, fromX, fromY, toX, toY);
            }
        }
    }
}
