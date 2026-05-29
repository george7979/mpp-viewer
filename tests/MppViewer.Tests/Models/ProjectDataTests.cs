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
        Assert.Equal(new DateTime(2025, 1, 6), task.Start);
        Assert.Equal(new DateTime(2025, 1, 10), task.Finish);
        Assert.Equal(50, task.PercentComplete);
        Assert.Equal(TimeSpan.FromDays(5), task.Duration);
        Assert.Empty(task.PredecessorIds);
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
