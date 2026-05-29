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
            IsSummary: true,
            Start: new DateTime(2025, 1, 6),
            Finish: new DateTime(2025, 1, 10),
            Duration: TimeSpan.FromDays(5),
            PercentComplete: 50,
            PredecessorIds: Array.Empty<int>(),
            ResourceNames: new[] { "Jan Kowalski", "Anna Nowak" }
        );

        Assert.Equal(1, task.Id);
        Assert.Equal("Design", task.Name);
        Assert.Equal(1, task.OutlineLevel);
        Assert.Equal(new DateTime(2025, 1, 6), task.Start);
        Assert.Equal(new DateTime(2025, 1, 10), task.Finish);
        Assert.Equal(50, task.PercentComplete);
        Assert.Equal(TimeSpan.FromDays(5), task.Duration);
        Assert.Empty(task.PredecessorIds);
        Assert.Equal(new[] { "Jan Kowalski", "Anna Nowak" }, task.ResourceNames);
        Assert.True(task.IsSummary);
    }

    [Fact]
    public void ProjectData_StoresTasks()
    {
        var tasks = new[]
        {
            new TaskItem(1, "Task A", 1, false, null, null, TimeSpan.Zero, 0, Array.Empty<int>(), Array.Empty<string>()),
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
