using MppViewer.Models;

namespace MppViewer.Controls;

public class TaskGridView : DataGridView
{
    private readonly Font _summaryFont;

    public TaskGridView()
    {
        // Pogrubiona odmiana czcionki tabeli dla zadań sumarycznych (rodziców).
        _summaryFont = new Font(Font, FontStyle.Bold);

        ReadOnly = true;
        AllowUserToAddRows = false;
        AllowUserToDeleteRows = false;
        AllowUserToResizeRows = false;
        RowHeadersVisible = false;
        SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        MultiSelect = false;
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        // Wysokość nagłówka zrównana z wykresem, by wiersze tabeli i paski Gantta
        // startowały na tym samym Y (GanttControl odczytuje pozycje wierszy stąd).
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        ColumnHeadersHeight = GanttControl.HeaderHeight;
        // Naprzemienne tło wierszy zgodne z pasami w wykresie — ten sam parzysty/nieparzysty
        // podział pozwala wzrokowo dopasować wiersz tabeli do paska Gantta.
        AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 245, 248);
        // Zaznaczenie w akcencie pomarańczowym (zamiast systemowego niebieskiego).
        DefaultCellStyle.SelectionBackColor = Color.FromArgb(245, 130, 32);
        DefaultCellStyle.SelectionForeColor = Color.White;
        typeof(DataGridView)
            .GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(this, true);

        Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colId", HeaderText = "ID", Width = 45, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
        });
        Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colName", HeaderText = "Task name", Width = 260
        });
        Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colDuration", HeaderText = "Duration", Width = 90, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
        });
        Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colStart", HeaderText = "Start", Width = 90
        });
        Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colFinish", HeaderText = "Finish", Width = 90
        });
        Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colPct", HeaderText = "% complete", Width = 80, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
        });
        Columns.Add(new DataGridViewTextBoxColumn
        {
            // Fill: ostatnia kolumna zawsze rozciąga się do prawej krawędzi siatki, więc
            // wykres przylega bez przerwy niezależnie od timingu/paska. MainForm ustawia
            // startowy splitter tak, by Fill wystartował na szerokości treści.
            Name = "colResources", HeaderText = "Resources",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 100
        });

        // Sortowanie po nagłówkach wyłączone: tabela to hierarchiczne drzewo WBS,
        // a sortowanie przestawiłoby wiersze, rozbijając wcięcia/pogrubienia i
        // rozjeżdżając je z wykresem (Gantt mapuje zadania po kolejności wierszy).
        foreach (DataGridViewColumn column in Columns)
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
    }

    public void LoadTasks(IReadOnlyList<TaskItem> tasks)
    {
        Rows.Clear();
        foreach (var task in tasks)
        {
            string indent = new string(' ', task.OutlineLevel * 3);
            string durationText = FormatDuration(task.Duration);

            int rowIndex = Rows.Add(
                task.Id,
                indent + task.Name,
                durationText,
                task.Start?.ToString("yyyy-MM-dd") ?? "",
                task.Finish?.ToString("yyyy-MM-dd") ?? "",
                $"{task.PercentComplete}%",
                string.Join("; ", task.ResourceNames)
            );

            // Wyróżnij poziomy struktury (PROJEKT/ETAP/...) pogrubieniem.
            if (task.IsSummary)
                Rows[rowIndex].DefaultCellStyle.Font = _summaryFont;

            // Zachowaj zadanie przy wierszu, by filtr mógł sprawdzić dopasowanie
            // bez przeładowywania tabeli.
            Rows[rowIndex].Tag = task;
        }
    }

    /// <summary>
    /// Wyszarza wiersze zadań nieprzypisanych do wybranej osoby; null = wszystkie
    /// w normalnym kolorze. Nie usuwa wierszy — to tylko styl widoku.
    /// </summary>
    public void SetResourceFilter(string? resource)
    {
        foreach (DataGridViewRow row in Rows)
        {
            if (row.Tag is not TaskItem task) continue;
            bool dim = resource != null && !task.ResourceNames.Contains(resource);
            row.DefaultCellStyle.ForeColor = dim ? Color.Silver : Color.Empty;
        }
        Invalidate();
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
            return "—";
        if (duration.TotalDays >= 1)
            return $"{duration.TotalDays:F1} d";
        if (duration.TotalHours >= 1)
            return $"{duration.TotalHours:F1} h";
        return $"{duration.TotalMinutes:F0} min";
    }
}
