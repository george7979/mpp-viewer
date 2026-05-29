using System.Diagnostics;
using MppViewer.Controls;
using MppViewer.Models;
using MppViewer.Services;

namespace MppViewer;

public class MainForm : Form
{
    private const string AllResources = "(everyone)";
    private const string RepoUrl = "https://github.com/george7979/mpp-viewer";

    private readonly TaskGridView _grid = new();
    private readonly GanttControl _gantt = new();
    private readonly SplitContainer _split = new();
    private readonly ToolStrip _toolbar = new();
    private readonly ToolStripComboBox _resourceCombo = new();
    private readonly StatusStrip _status = new();
    private readonly ToolStripStatusLabel _statusFile = new() { Text = "No file" };
    private readonly ToolStripStatusLabel _statusCount = new();
    private readonly ToolStripStatusLabel _statusRange = new();
    private readonly ToolStripStatusLabel _statusVersion = new();

    public MainForm()
    {
        Text = "MPP Viewer";
        Size = new System.Drawing.Size(1280, 720);
        MinimumSize = new System.Drawing.Size(800, 500);

        // Ikona okna/paska zadań z osadzonego zasobu (exe-owa ikona idzie z ApplicationIcon).
        using (var iconStream = GetType().Assembly.GetManifestResourceStream("app.ico"))
            if (iconStream != null) Icon = new System.Drawing.Icon(iconStream);

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
    /// Ustawia startową pozycję splittera tak, by kolumna Fill ("Resources") wystartowała
    /// na szerokości swojej treści. Kolumna jest Fill, więc przylega do wykresu zawsze —
    /// ta metoda dobiera tylko ładną szerokość początkową (zapas na pionowy pasek; jeśli
    /// go nie ma, Fill po prostu zajmie te kilka px — bez przerwy).
    /// </summary>
    private void FitSplitterToColumns()
    {
        int fixedWidth = 0;
        foreach (DataGridViewColumn column in _grid.Columns)
            if (column.AutoSizeMode != DataGridViewAutoSizeColumnMode.Fill)
                fixedWidth += column.Width;

        int preferred = _grid.Columns["colResources"]!
            .GetPreferredWidth(DataGridViewAutoSizeColumnMode.AllCells, fixedHeight: true);

        int needed = fixedWidth + preferred + SystemInformation.VerticalScrollBarWidth + 2;
        int max = _split.Width - _split.Panel2MinSize - _split.SplitterWidth;
        _split.SplitterDistance = Math.Clamp(needed, _split.Panel1MinSize, Math.Max(_split.Panel1MinSize, max));
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
        // realnego rozmiaru, a SplitterDistance poza zakresem rzuca wyjątek. 780 to
        // rozsądny podział dla pustego okna; po wczytaniu pliku FitSplitterToColumns
        // dosuwa wykres do faktycznej szerokości kolumn.
        int max = _split.Width - _split.Panel2MinSize - _split.SplitterWidth;
        _split.SplitterDistance = Math.Clamp(780, _split.Panel1MinSize, Math.Max(_split.Panel1MinSize, max));
    }

    private void BuildMenu()
    {
        // Pozycje najwyższego poziomu bez rodzica "File": klik = akcja od razu
        // (ToolStripMenuItem bez DropDownItems odpala Click zamiast rozwijać menu).
        var menuStrip = new MenuStrip();
        var openItem = new ToolStripMenuItem("Open...", null, OnOpenClick) { ShortcutKeys = Keys.Control | Keys.O };
        var exitItem = new ToolStripMenuItem("Exit", null, (_, __) => Close());
        // Link do repozytorium — wyrównany do prawej, by nie mieszał się z akcjami.
        var githubItem = new ToolStripMenuItem("GitHub", null, (_, __) => OpenUrl(RepoUrl))
        {
            Alignment = ToolStripItemAlignment.Right
        };

        menuStrip.Items.Add(openItem);
        menuStrip.Items.Add(exitItem);
        menuStrip.Items.Add(githubItem);
        Controls.Add(menuStrip);
        MainMenuStrip = menuStrip;
    }

    private static void OpenUrl(string url)
    {
        // UseShellExecute=true → otwiera w domyślnej przeglądarce systemowej.
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* brak skojarzonej przeglądarki — pomiń po cichu */ }
    }

    private void BuildToolbar()
    {
        _toolbar.GripStyle = ToolStripGripStyle.Hidden;
        _resourceCombo.DropDownStyle = ComboBoxStyle.DropDownList;  // tylko wybór z listy, bez wpisywania
        _resourceCombo.AutoSize = false;
        _resourceCombo.Width = 220;
        _resourceCombo.SelectedIndexChanged += OnResourceFilterChanged;

        _toolbar.Items.Add(new ToolStripLabel("Show assigned to:"));
        _toolbar.Items.Add(_resourceCombo);
        Controls.Add(_toolbar);
    }

    private void OnResourceFilterChanged(object? sender, EventArgs e)
    {
        // Indeks 0 = "(everyone)" → brak filtra (null).
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

        // Wersja czytana z assembly (zawsze zgodna z buildem), w stopce po prawej.
        var v = GetType().Assembly.GetName().Version;
        _statusVersion.Text = v is null ? "" : $"v{v.Major}.{v.Minor}.{v.Build}";
        _statusVersion.Alignment = ToolStripItemAlignment.Right;

        _status.Items.AddRange(new ToolStripItem[] { _statusFile, _statusCount, _statusRange, _statusVersion });
        Controls.Add(_status);
    }

    private void BuildLayout()
    {
        _split.Dock = DockStyle.Fill;
        _split.Orientation = Orientation.Vertical;
        // Przy zmianie rozmiaru okna tabela trzyma szerokość, a nadmiar pochłania wykres
        // (inaczej Fill-owa ostatnia kolumna rosłaby wraz z oknem).
        _split.FixedPanel = FixedPanel.Panel1;

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
            Title = "Open MS Project file",
            Filter = "MS Project (*.mpp)|*.mpp|All files (*.*)|*.*",
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
            _statusCount.Text = $"{data.Tasks.Count} tasks";
            _statusRange.Text = $"{data.ProjectStart:dd.MM.yyyy} – {data.ProjectFinish:dd.MM.yyyy}";
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            _statusFile.Text = "Error";
            _statusCount.Text = "";
            _statusRange.Text = "";
            MessageBox.Show($"Cannot open the file:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception)
        {
            _statusFile.Text = "Error";
            _statusCount.Text = "";
            _statusRange.Text = "";
            MessageBox.Show("Cannot read the file. Make sure it is a valid MS Project (.mpp) file.",
                "Read error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }
}
