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
    IReadOnlyList<int> PredecessorIds,
    IReadOnlyList<string> ResourceNames
);
