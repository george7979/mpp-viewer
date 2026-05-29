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
        typeof(DataGridView)
            .GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(this, true);

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
