# MPP Viewer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a portable, single-file Windows .exe that opens .mpp files and shows a split view of task table + Gantt chart, built automatically via GitHub Actions.

**Architecture:** .NET 8 WinForms app, three layers: `MppReader` (MPXJ.NET) → `ProjectData`/`TaskItem` (records) → `TaskGridView` + `GanttControl` (UI). Self-contained `PublishSingleFile` build, no JVM or installation required.

**Tech Stack:** .NET 8, WinForms (`net8.0-windows`), MPXJ NuGet package (IKVM-based .NET port of MPXJ), xUnit 2.9, GitHub Actions `windows-latest`

---

## File Map

| File | Responsibility |
|------|----------------|
| `src/MppViewer/MppViewer.csproj` | Project config, NuGet references |
| `src/MppViewer/Program.cs` | Entry point |
| `src/MppViewer/Models/ProjectData.cs` | Immutable records: `ProjectData`, `TaskItem` |
| `src/MppViewer/Services/MppReader.cs` | MPXJ.NET parsing → `ProjectData` (only file touching MPXJ) |
| `src/MppViewer/Controls/GanttMetrics.cs` | Pure math: date↔pixel, total width (no WinForms types) |
| `src/MppViewer/Controls/TaskGridView.cs` | `DataGridView` subclass with WBS indentation |
| `src/MppViewer/Controls/GanttControl.cs` | Custom `Panel`: timeline header + task bars + dependency arrows |
| `src/MppViewer/MainForm.cs` | Menu, `SplitContainer`, status bar, async file open, scroll sync |
| `tests/MppViewer.Tests/MppViewer.Tests.csproj` | xUnit test project |
| `tests/MppViewer.Tests/Models/ProjectDataTests.cs` | Model construction tests |
| `tests/MppViewer.Tests/Controls/GanttMetricsTests.cs` | Date↔pixel math tests |
| `.github/workflows/build.yml` | CI: build + upload `.exe` artifact |

---

## Task 1: Solution scaffold

**Files:**
- Create: `MppViewer.sln`
- Create: `src/MppViewer/MppViewer.csproj`
- Create: `src/MppViewer/Program.cs`
- Create: `tests/MppViewer.Tests/MppViewer.Tests.csproj`

- [ ] **Step 1: Create solution and projects**

```bash
cd mpp-viewer   # repo root
dotnet new sln -n MppViewer
dotnet new winforms -n MppViewer -o src/MppViewer --framework net8.0-windows
dotnet new xunit -n MppViewer.Tests -o tests/MppViewer.Tests --framework net8.0-windows
dotnet sln add src/MppViewer/MppViewer.csproj
dotnet sln add tests/MppViewer.Tests/MppViewer.Tests.csproj
dotnet add tests/MppViewer.Tests/MppViewer.Tests.csproj reference src/MppViewer/MppViewer.csproj
```

Expected: `MppViewer.sln` created, two projects added.

- [ ] **Step 2: Verify MPXJ package name on NuGet**

```bash
dotnet add src/MppViewer/MppViewer.csproj package MPXJ
```

If `MPXJ` is not found, try:
```bash
dotnet add src/MppViewer/MppViewer.csproj package net.sf.mpxj
```

Use whichever succeeds. The correct NuGet package is published by Jon Iles (mpxj.org). Confirm the package is installed:

```bash
grep -i mpxj src/MppViewer/MppViewer.csproj
```

Expected output contains: `<PackageReference Include="MPXJ"`

- [ ] **Step 3: Update `MppViewer.csproj` to required settings**

Replace content of `src/MppViewer/MppViewer.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AssemblyName>MppViewer</AssemblyName>
    <RootNamespace>MppViewer</RootNamespace>
    <ApplicationHighDpiMode>SystemAware</ApplicationHighDpiMode>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MPXJ" Version="13.*" />
  </ItemGroup>
</Project>
```

(Replace `MPXJ` with actual package name found in Step 2.)

- [ ] **Step 4: Write `Program.cs`**

```csharp
using MppViewer;

Application.EnableVisualStyles();
Application.SetCompatibleTextRenderingDefault(false);
Application.Run(new MainForm());
```

- [ ] **Step 5: Create stub `MainForm.cs` (empty, just to allow build)**

```csharp
namespace MppViewer;

public class MainForm : Form
{
    public MainForm()
    {
        Text = "MPP Viewer";
        Size = new System.Drawing.Size(1200, 700);
    }
}
```

- [ ] **Step 6: Verify solution builds**

```bash
dotnet build MppViewer.sln
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 7: Commit**

```bash
git add MppViewer.sln src/ tests/
git commit -m "feat: scaffold solution with WinForms project and test project"
```

---

## Task 2: Data models + tests

**Files:**
- Create: `src/MppViewer/Models/ProjectData.cs`
- Create: `tests/MppViewer.Tests/Models/ProjectDataTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/MppViewer.Tests/Models/ProjectDataTests.cs`:

```csharp
using MppViewer.Models;

namespace MppViewer.Tests.Models;

public class ProjectDataTests
{
    [Fact]
    public void TaskItem_StoresAllProperties()
    {
        var task = new TaskItem(
            Id: 1,
            Name: "Design",
            OutlineLevel: 1,
            Start: new DateTime(2025, 1, 6),
            Finish: new DateTime(2025, 1, 10),
            Duration: TimeSpan.FromDays(5),
            PercentComplete: 50,
            PredecessorIds: Array.Empty<int>()
        );

        Assert.Equal(1, task.Id);
        Assert.Equal("Design", task.Name);
        Assert.Equal(1, task.OutlineLevel);
        Assert.Equal(50, task.PercentComplete);
        Assert.Equal(TimeSpan.FromDays(5), task.Duration);
    }

    [Fact]
    public void ProjectData_StoresTasks()
    {
        var tasks = new[]
        {
            new TaskItem(1, "Task A", 1, null, null, TimeSpan.Zero, 0, Array.Empty<int>()),
        };
        var project = new ProjectData(
            FilePath: "test.mpp",
            ProjectStart: new DateTime(2025, 1, 1),
            ProjectFinish: new DateTime(2025, 12, 31),
            Tasks: tasks
        );

        Assert.Equal("test.mpp", project.FilePath);
        Assert.Single(project.Tasks);
        Assert.Equal("Task A", project.Tasks[0].Name);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/MppViewer.Tests/MppViewer.Tests.csproj --filter "FullyQualifiedName~ProjectDataTests"
```

Expected: FAIL — `MppViewer.Models` namespace not found.

- [ ] **Step 3: Create `src/MppViewer/Models/ProjectData.cs`**

```csharp
namespace MppViewer.Models;

public record ProjectData(
    string FilePath,
    DateTime ProjectStart,
    DateTime ProjectFinish,
    IReadOnlyList<TaskItem> Tasks
);

public record TaskItem(
    int Id,
    string Name,
    int OutlineLevel,
    DateTime? Start,
    DateTime? Finish,
    TimeSpan Duration,
    int PercentComplete,
    IReadOnlyList<int> PredecessorIds
);
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/MppViewer.Tests/MppViewer.Tests.csproj --filter "FullyQualifiedName~ProjectDataTests"
```

Expected: `Passed: 2, Failed: 0`

- [ ] **Step 5: Commit**

```bash
git add src/MppViewer/Models/ tests/MppViewer.Tests/Models/
git commit -m "feat: add ProjectData and TaskItem records with tests"
```

---

## Task 3: GanttMetrics + tests

**Files:**
- Create: `src/MppViewer/Controls/GanttMetrics.cs`
- Create: `tests/MppViewer.Tests/Controls/GanttMetricsTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/MppViewer.Tests/Controls/GanttMetricsTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/MppViewer.Tests/MppViewer.Tests.csproj --filter "FullyQualifiedName~GanttMetricsTests"
```

Expected: FAIL — `MppViewer.Controls.GanttMetrics` not found.

- [ ] **Step 3: Create `src/MppViewer/Controls/GanttMetrics.cs`**

```csharp
namespace MppViewer.Controls;

public static class GanttMetrics
{
    public static float DateToX(DateTime date, DateTime projectStart, float pixelsPerDay)
        => (float)(date - projectStart).TotalDays * pixelsPerDay;

    public static int TotalWidth(DateTime projectStart, DateTime projectEnd, float pixelsPerDay)
        => (int)((projectEnd - projectStart).TotalDays * pixelsPerDay);

    public static int RowY(int rowIndex, int firstVisibleRow, int rowHeight, int headerHeight)
        => headerHeight + (rowIndex - firstVisibleRow) * rowHeight;
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/MppViewer.Tests/MppViewer.Tests.csproj --filter "FullyQualifiedName~GanttMetricsTests"
```

Expected: `Passed: 5, Failed: 0`

- [ ] **Step 5: Commit**

```bash
git add src/MppViewer/Controls/GanttMetrics.cs tests/MppViewer.Tests/Controls/
git commit -m "feat: add GanttMetrics pure math utilities with tests"
```

---

## Task 4: MppReader service

**Files:**
- Create: `src/MppViewer/Services/MppReader.cs`

Note: MPXJ.NET uses IKVM under the hood. Java types (dates, numbers) are mapped to their IKVM .NET equivalents. The `java.util.Date.getTime()` method returns milliseconds since Unix epoch, used for DateTime conversion.

- [ ] **Step 1: Create `src/MppViewer/Services/MppReader.cs`**

```csharp
using MppViewer.Models;
using net.sf.mpxj;
using net.sf.mpxj.reader;

namespace MppViewer.Services;

public static class MppReader
{
    public static ProjectData Read(string filePath)
    {
        var projectFile = new UniversalProjectReader().read(filePath);
        var rawTasks = projectFile.Tasks;

        var tasks = new List<TaskItem>();
        foreach (Task task in rawTasks)
        {
            if (string.IsNullOrEmpty(task.Name)) continue;

            tasks.Add(new TaskItem(
                Id: task.ID?.intValue() ?? 0,
                Name: task.Name,
                OutlineLevel: task.OutlineLevel?.intValue() ?? 0,
                Start: ToDateTime(task.Start),
                Finish: ToDateTime(task.Finish),
                Duration: ToDuration(task.Duration),
                PercentComplete: ToInt(task.PercentageComplete),
                PredecessorIds: GetPredecessorIds(task)
            ));
        }

        var projectStart = tasks
            .Where(t => t.Start.HasValue)
            .Select(t => t.Start!.Value)
            .DefaultIfEmpty(DateTime.Today)
            .Min();

        var projectFinish = tasks
            .Where(t => t.Finish.HasValue)
            .Select(t => t.Finish!.Value)
            .DefaultIfEmpty(DateTime.Today.AddMonths(1))
            .Max();

        return new ProjectData(filePath, projectStart, projectFinish, tasks);
    }

    private static DateTime? ToDateTime(java.util.Date? d)
    {
        if (d == null) return null;
        return DateTimeOffset.FromUnixTimeMilliseconds(d.getTime()).LocalDateTime;
    }

    private static TimeSpan ToDuration(Duration? dur)
    {
        if (dur == null) return TimeSpan.Zero;
        if (dur.getUnits() == TimeUnit.HOURS)
            return TimeSpan.FromHours(dur.getDuration());
        if (dur.getUnits() == TimeUnit.MINUTES)
            return TimeSpan.FromMinutes(dur.getDuration());
        // Default: treat numeric value as days
        return TimeSpan.FromDays(dur.getDuration());
    }

    private static int ToInt(java.lang.Number? n) =>
        n == null ? 0 : (int)Math.Round(n.doubleValue());

    private static IReadOnlyList<int> GetPredecessorIds(Task task)
    {
        var list = task.Predecessors;
        if (list == null) return Array.Empty<int>();

        var ids = new List<int>();
        foreach (Relation rel in list)
        {
            var id = rel.TargetTask?.ID?.intValue();
            if (id.HasValue) ids.Add(id.Value);
        }
        return ids;
    }
}
```

- [ ] **Step 2: Verify it compiles**

```bash
dotnet build src/MppViewer/MppViewer.csproj
```

Expected: `Build succeeded. 0 Error(s)`

If there are namespace errors for `java.util.Date`, `java.lang.Number`, `Duration`, `TimeUnit`, `Task`, `Relation` — check the MPXJ package's actual namespaces by browsing the installed package:

```bash
find ~/.nuget/packages -name "*.dll" -path "*/mpxj/*" | head -5
```

Then inspect with:
```bash
dotnet-dump analyze   # or check IntelliSense in any IDE
```

The MPXJ API might expose task properties as C#-style properties (PascalCase) in newer IKVM versions. Adjust method calls to match what compiles.

- [ ] **Step 3: Commit**

```bash
git add src/MppViewer/Services/
git commit -m "feat: add MppReader service using MPXJ.NET"
```

---

## Task 5: TaskGridView control

**Files:**
- Create: `src/MppViewer/Controls/TaskGridView.cs`

- [ ] **Step 1: Create `src/MppViewer/Controls/TaskGridView.cs`**

```csharp
using MppViewer.Models;

namespace MppViewer.Controls;

public class TaskGridView : DataGridView
{
    public TaskGridView()
    {
        ReadOnly = true;
        AllowUserToAddRows = false;
        AllowUserToDeleteRows = false;
        AllowUserToResizeRows = false;
        RowHeadersVisible = false;
        SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        MultiSelect = false;
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        DoubleBuffered = true;

        Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colId", HeaderText = "ID", Width = 45, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
        });
        Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colName", HeaderText = "Nazwa zadania", Width = 260
        });
        Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colDuration", HeaderText = "Czas trwania", Width = 90, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
        });
        Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colStart", HeaderText = "Start", Width = 90
        });
        Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colFinish", HeaderText = "Koniec", Width = 90
        });
        Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colPct", HeaderText = "% ukończenia", Width = 80, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
        });
    }

    public void LoadTasks(IReadOnlyList<TaskItem> tasks)
    {
        Rows.Clear();
        foreach (var task in tasks)
        {
            string indent = new string(' ', task.OutlineLevel * 3);
            string durationText = FormatDuration(task.Duration);

            Rows.Add(
                task.Id,
                indent + task.Name,
                durationText,
                task.Start?.ToString("yyyy-MM-dd") ?? "",
                task.Finish?.ToString("yyyy-MM-dd") ?? "",
                $"{task.PercentComplete}%"
            );
        }
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 1)
            return $"{duration.TotalDays:F1} d";
        if (duration.TotalHours >= 1)
            return $"{duration.TotalHours:F1} h";
        return $"{duration.TotalMinutes:F0} min";
    }
}
```

- [ ] **Step 2: Verify compilation**

```bash
dotnet build src/MppViewer/MppViewer.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add src/MppViewer/Controls/TaskGridView.cs
git commit -m "feat: add TaskGridView with WBS indentation"
```

---

## Task 6: GanttControl — timeline and task bars

**Files:**
- Create: `src/MppViewer/Controls/GanttControl.cs`

- [ ] **Step 1: Create `src/MppViewer/Controls/GanttControl.cs`**

```csharp
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
```

- [ ] **Step 2: Verify compilation**

```bash
dotnet build src/MppViewer/MppViewer.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add src/MppViewer/Controls/GanttControl.cs
git commit -m "feat: add GanttControl with timeline header, task bars and dependency arrows"
```

---

## Task 7: MainForm — wire everything together

**Files:**
- Modify: `src/MppViewer/MainForm.cs` (replace stub)

- [ ] **Step 1: Replace `src/MppViewer/MainForm.cs`**

```csharp
using MppViewer.Controls;
using MppViewer.Services;

namespace MppViewer;

public class MainForm : Form
{
    private readonly TaskGridView _grid = new();
    private readonly GanttControl _gantt = new();
    private readonly SplitContainer _split = new();
    private readonly StatusStrip _status = new();
    private readonly ToolStripStatusLabel _statusFile = new() { Text = "Brak pliku" };
    private readonly ToolStripStatusLabel _statusCount = new();
    private readonly ToolStripStatusLabel _statusRange = new();

    public MainForm()
    {
        Text = "MPP Viewer";
        Size = new System.Drawing.Size(1280, 720);
        MinimumSize = new System.Drawing.Size(800, 500);

        BuildMenu();
        BuildStatusBar();
        BuildLayout();

        _grid.Scroll += OnGridScroll;
    }

    private void BuildMenu()
    {
        var menuStrip = new MenuStrip();
        var fileMenu = new ToolStripMenuItem("Plik");
        var openItem = new ToolStripMenuItem("Otwórz...", null, OnOpenClick) { ShortcutKeys = Keys.Control | Keys.O };
        var exitItem = new ToolStripMenuItem("Wyjście", null, (_, __) => Close());

        fileMenu.DropDownItems.Add(openItem);
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add(exitItem);
        menuStrip.Items.Add(fileMenu);
        Controls.Add(menuStrip);
        MainMenuStrip = menuStrip;
    }

    private void BuildStatusBar()
    {
        _statusFile.BorderSides = ToolStripStatusLabelBorderSides.Right;
        _statusCount.BorderSides = ToolStripStatusLabelBorderSides.Right;
        _status.Items.AddRange(new ToolStripItem[] { _statusFile, _statusCount, _statusRange });
        Controls.Add(_status);
    }

    private void BuildLayout()
    {
        _split.Dock = DockStyle.Fill;
        _split.Orientation = Orientation.Vertical;
        _split.SplitterDistance = 480;

        _grid.Dock = DockStyle.Fill;
        _gantt.Dock = DockStyle.Fill;

        _split.Panel1.Controls.Add(_grid);
        _split.Panel2.Controls.Add(_gantt);
        Controls.Add(_split);
    }

    private async void OnOpenClick(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Otwórz plik MS Project",
            Filter = "MS Project (*.mpp)|*.mpp|Wszystkie pliki (*.*)|*.*",
            FilterIndex = 1
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        await LoadFileAsync(dlg.FileName);
    }

    private async Task LoadFileAsync(string path)
    {
        Cursor = Cursors.WaitCursor;
        try
        {
            var data = await Task.Run(() => MppReader.Read(path));

            _grid.LoadTasks(data.Tasks);
            _gantt.Load(data.Tasks, data.ProjectStart, data.ProjectFinish);

            _statusFile.Text = System.IO.Path.GetFileName(path);
            _statusCount.Text = $"{data.Tasks.Count} zadań";
            _statusRange.Text = $"{data.ProjectStart:dd.MM.yyyy} – {data.ProjectFinish:dd.MM.yyyy}";
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            MessageBox.Show($"Nie można otworzyć pliku:\n{ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception)
        {
            MessageBox.Show("Nie można odczytać pliku. Upewnij się, że jest to prawidłowy plik MS Project (.mpp).",
                "Błąd odczytu", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void OnGridScroll(object? sender, ScrollEventArgs e)
    {
        if (e.ScrollOrientation == ScrollOrientation.VerticalScroll)
        {
            _gantt.FirstVisibleRow = _grid.FirstDisplayedScrollingRowIndex;
            _gantt.Invalidate();
        }
    }
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build src/MppViewer/MppViewer.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Run all tests**

```bash
dotnet test MppViewer.sln
```

Expected: all tests green.

- [ ] **Step 4: Commit**

```bash
git add src/MppViewer/MainForm.cs
git commit -m "feat: add MainForm with menu, split layout, async file open and scroll sync"
```

---

## Task 8: GitHub Actions build

**Files:**
- Create: `.github/workflows/build.yml`

- [ ] **Step 1: Create directory structure**

```bash
mkdir -p .github/workflows   # from repo root
```

- [ ] **Step 2: Create `.github/workflows/build.yml`**

```yaml
name: Build

on:
  push:
    branches: [main]
  workflow_dispatch:

jobs:
  build:
    runs-on: windows-latest

    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore
        run: dotnet restore MppViewer.sln

      - name: Test
        run: dotnet test MppViewer.sln --no-restore --verbosity normal

      - name: Publish
        run: >
          dotnet publish src/MppViewer/MppViewer.csproj
          -c Release
          -r win-x64
          --self-contained true
          /p:PublishSingleFile=true
          /p:EnableCompressionInSingleFile=true
          -o publish/

      - name: Upload artifact
        uses: actions/upload-artifact@v4
        with:
          name: mpp-viewer-win-x64
          path: publish/MppViewer.exe
          retention-days: 30
```

- [ ] **Step 3: Verify YAML syntax (optional local check)**

```bash
python3 -c "import yaml; yaml.safe_load(open('.github/workflows/build.yml'))" && echo "YAML OK"
```

Expected: `YAML OK`

- [ ] **Step 4: Commit and push**

```bash
git add .github/
git commit -m "ci: add GitHub Actions build workflow producing portable exe"
```

Then push to GitHub to trigger the first build:
```bash
git push origin main
```

After push: go to GitHub → Actions tab → verify the workflow runs and produces an `mpp-viewer-win-x64` artifact.

---

## Task 9: End-to-end smoke test on Windows

This task must run on a Windows machine (or download the artifact from GitHub Actions).

- [ ] **Step 1: Download artifact**

From GitHub: Actions → latest build → Artifacts → `mpp-viewer-win-x64` → download ZIP → extract `MppViewer.exe`.

- [ ] **Step 2: Smoke test — application starts**

Double-click `MppViewer.exe`.
Expected: Window opens titled "MPP Viewer", no error dialogs, menu bar visible.

- [ ] **Step 3: Smoke test — file open**

Use Plik → Otwórz (or Ctrl+O) to open a `.mpp` file.
Expected:
- Task table fills with rows, WBS indentation visible
- Gantt chart shows bars aligned with dates
- Status bar shows filename, task count, date range
- No crash

- [ ] **Step 4: Smoke test — large file (if available)**

Open a file with 500+ tasks.
Expected: UI remains responsive during load (wait cursor appears briefly, then clears).

- [ ] **Step 5: Smoke test — invalid file**

Drag or open a non-mpp file (e.g., a `.txt` renamed to `.mpp`).
Expected: Error dialog "Nie można odczytać pliku...", window stays open.

---

## Post-implementation notes

- **MPXJ API adjustments:** If namespace or method names differ from what's in this plan, check the installed package documentation: `find ~/.nuget/packages -name "*.xml" -path "*/mpxj/*"`. The API is Java-style (camelCase methods) exposed via IKVM.
- **WBS indentation:** The space-padding approach in `TaskGridView` is quick but doesn't visually expand/collapse groups. For v2, consider a `TreeView`-based control.
- **Gantt zoom:** `PixelsPerDay = 15f` is hardcoded. A zoom slider is a natural v2 addition.
- **Scroll sync accuracy:** `FirstDisplayedScrollingRowIndex` may be off by one on partial rows. If visual misalignment occurs, adjust with `+1` offset.
