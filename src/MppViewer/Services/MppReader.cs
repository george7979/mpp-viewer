using MppViewer.Models;
using net.sf.mpxj;
using net.sf.mpxj.reader;

namespace MppViewer.Services;

public static class MppReader
{
    public static ProjectData Read(string filePath)
    {
        var projectFile = new UniversalProjectReader().read(filePath)
            ?? throw new InvalidDataException($"UniversalProjectReader returned null for '{filePath}'. The file may be an unsupported format.");
        var rawTasks = projectFile.getTasks();

        var tasks = new List<TaskItem>();
        foreach (var obj in rawTasks.toArray())
        {
            if (obj is not net.sf.mpxj.Task task) continue;
            if (string.IsNullOrEmpty(task.getName())) continue;

            tasks.Add(new TaskItem(
                Id: task.getID()?.intValue() ?? 0,
                Name: task.getName()!,
                OutlineLevel: task.getOutlineLevel()?.intValue() ?? 0,
                IsSummary: task.getSummary(),
                Start: ToDateTime(task.getStart()),
                Finish: ToDateTime(task.getFinish()),
                Duration: ToDuration(task.getDuration()),
                PercentComplete: ToInt(task.getPercentageComplete()),
                PredecessorIds: GetPredecessorIds(task),
                ResourceNames: GetResourceNames(task)
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

    private static DateTime? ToDateTime(java.time.LocalDateTime? d)
    {
        if (d == null) return null;
        return new DateTime(
            d.getYear(), d.getMonthValue(), d.getDayOfMonth(),
            d.getHour(), d.getMinute(), d.getSecond(),
            DateTimeKind.Unspecified);  // MPP stores schedule dates, not instants
    }

    private static TimeSpan ToDuration(Duration? dur)
    {
        if (dur == null) return TimeSpan.Zero;
        var units = dur.getUnits();
        var value = dur.getDuration();
        if (units == TimeUnit.HOURS)
            return TimeSpan.FromHours(value);
        if (units == TimeUnit.MINUTES)
            return TimeSpan.FromMinutes(value);
        if (units == TimeUnit.WEEKS) return TimeSpan.FromDays(value * 7);
        if (units == TimeUnit.MONTHS) return TimeSpan.FromDays(value * 30.44);
        return TimeSpan.FromDays(value);
    }

    private static int ToInt(java.lang.Number? n) =>
        n == null ? 0 : (int)Math.Round(n.doubleValue());

    private static IReadOnlyList<int> GetPredecessorIds(net.sf.mpxj.Task task)
    {
        var list = task.getPredecessors();
        if (list == null) return Array.Empty<int>();

        var ids = new List<int>();
        foreach (var obj in list.toArray())
        {
            if (obj is Relation rel)
            {
                var id = rel.getPredecessorTask()?.getID()?.intValue();
                if (id.HasValue && id.Value != task.getID()?.intValue())
                    ids.Add(id.Value);
            }
        }
        return ids;
    }

    private static IReadOnlyList<string> GetResourceNames(net.sf.mpxj.Task task)
    {
        var list = task.getResourceAssignments();
        if (list == null) return Array.Empty<string>();

        // One task may have several assignments; keep order, drop blanks and duplicates.
        var names = new List<string>();
        foreach (var obj in list.toArray())
        {
            if (obj is not ResourceAssignment assignment) continue;
            var name = assignment.getResource()?.getName();
            if (!string.IsNullOrWhiteSpace(name) && !names.Contains(name))
                names.Add(name);
        }
        return names;
    }
}
