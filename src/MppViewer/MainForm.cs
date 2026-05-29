using MppViewer.Controls;
using MppViewer.Models;
using MppViewer.Services;

namespace MppViewer;

public class MainForm : Form
{
    private const string AllResources = "(wszyscy)";

    private readonly TaskGridView _grid = new();
    private readonly GanttControl _gantt = new();
    private readonly SplitContainer _split = new();
    private readonly ToolStrip _toolbar = new();
    private readonly ToolStripComboBox _resourceCombo = new();
    private readonly StatusStrip _status = new();
    private readonly ToolStripStatusLabel _statusFile = new() { Text = "Brak pliku" };
    private readonly ToolStripStatusLabel _statusCount = new();
    private readonly ToolStripStatusLabel _statusRange = new();

    public MainForm()
    {
        Text = "MPP Viewer";
        Size = new System.Drawing.Size(1280, 720);
        MinimumSize = new System.Drawing.Size(800, 500);

        // Kolejność dodawania determinuje dokowanie: kontrolka Dock.Fill musi trafić
        // do Controls jako pierwsza (najniższy z-order), aby menu (Top) i status (Bottom)
        // najpierw zarezerwowały swoje krawędzie, zamiast zostać przykryte przez Fill.
        BuildLayout();
        BuildStatusBar();
        BuildToolbar();
        BuildMenu();   // dodawane ostatnie → dokowane najpierw → zostaje na samej górze

        // Wykres odczytuje geometrię wierszy z tabeli i sam nasłuchuje jej przewijania.
        _gantt.AttachGrid(_grid);

        // Dwuklik w wiersz przewija wykres do daty startu klikniętego zadania.
        _grid.CellDoubleClick += OnRowDoubleClick;
    }

    /// <summary>
    /// Dosuwa wykres do tabeli: ustawia splitter tuż za kolumnami. Dodaje szerokość
    /// pionowego paska przewijania TYLKO gdy jest widoczny (inaczej powstaje szara
    /// przerwa). Liczone po zakończeniu layoutu, gdy auto-rozmiar kolumn i pasek
    /// są już ustalone.
    /// </summary>
    private void FitSplitterToColumns()
    {
        BeginInvoke(() =>
        {
            int columns = _grid.Columns.GetColumnsWidth(DataGridViewElementStates.Visible);
            var vbar = _grid.Controls.OfType<VScrollBar>().FirstOrDefault();
            int vscroll = vbar is { Visible: true } ? vbar.Width : 0;
            int needed = columns + vscroll + 2;  // +2: obramowanie siatki (FixedSingle)

            int max = _split.Width - _split.Panel2MinSize - _split.SplitterWidth;
            _split.SplitterDistance = Math.Clamp(needed, _split.Panel1MinSize, Math.Max(_split.Panel1MinSize, max));
        });
    }

    private void OnRowDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;  // nagłówek
        if (_grid.Rows[e.RowIndex].Tag is not TaskItem task) return;

        var date = task.Start ?? task.Finish;
        if (date.HasValue)
            _gantt.ScrollToDate(date.Value);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        // Ustawiane dopiero teraz — w konstruktorze SplitContainer nie ma jeszcze
        // realnego rozmiaru, a SplitterDistance poza zakresem rzuca wyjątek.
        // 780 mieści wszystkie stałe kolumny + kolumnę Fill, więc tabela startuje
        // bez poziomego scrolla, a "Przypisani" od razu dochodzi do wykresu.
        int max = _split.Width - _split.Panel2MinSize - _split.SplitterWidth;
        _split.SplitterDistance = Math.Clamp(780, _split.Panel1MinSize, Math.Max(_split.Panel1MinSize, max));
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

    private void BuildToolbar()
    {
        _toolbar.GripStyle = ToolStripGripStyle.Hidden;
        _resourceCombo.DropDownStyle = ComboBoxStyle.DropDownList;  // tylko wybór z listy, bez wpisywania
        _resourceCombo.AutoSize = false;
        _resourceCombo.Width = 220;
        _resourceCombo.SelectedIndexChanged += OnResourceFilterChanged;

        _toolbar.Items.Add(new ToolStripLabel("Pokaż przypisane do:"));
        _toolbar.Items.Add(_resourceCombo);
        Controls.Add(_toolbar);
    }

    private void OnResourceFilterChanged(object? sender, EventArgs e)
    {
        // Indeks 0 = "(wszyscy)" → brak filtra (null).
        string? resource = _resourceCombo.SelectedIndex <= 0
            ? null
            : _resourceCombo.SelectedItem as string;
        _grid.SetResourceFilter(resource);
        _gantt.SetResourceFilter(resource);
    }

    private void PopulateResourceFilter(IReadOnlyList<TaskItem> tasks)
    {
        var names = tasks
            .SelectMany(t => t.ResourceNames)
            .Distinct()
            .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        _resourceCombo.Items.Clear();
        _resourceCombo.Items.Add(AllResources);
        foreach (var name in names)
            _resourceCombo.Items.Add(name);
        _resourceCombo.SelectedIndex = 0;  // reset filtra przy nowym pliku
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
            PopulateResourceFilter(data.Tasks);
            FitSplitterToColumns();

            _statusFile.Text = System.IO.Path.GetFileName(path);
            _statusCount.Text = $"{data.Tasks.Count} zadań";
            _statusRange.Text = $"{data.ProjectStart:dd.MM.yyyy} – {data.ProjectFinish:dd.MM.yyyy}";
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            _statusFile.Text = "Błąd";
            _statusCount.Text = "";
            _statusRange.Text = "";
            MessageBox.Show($"Nie można otworzyć pliku:\n{ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception)
        {
            _statusFile.Text = "Błąd";
            _statusCount.Text = "";
            _statusRange.Text = "";
            MessageBox.Show("Nie można odczytać pliku. Upewnij się, że jest to prawidłowy plik MS Project (.mpp).",
                "Błąd odczytu", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }
}
