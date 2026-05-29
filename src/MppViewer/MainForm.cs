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
    private readonly string? _startupFile;

    public MainForm(string? startupFile = null)
    {
        _startupFile = startupFile;

        Text = "MPP Viewer";
        Size = new System.Drawing.Size(1280, 720);
        MinimumSize = new System.Drawing.Size(800, 500);

        // Ikona okna/paska zadań z osadzonego zasobu (exe-owa ikona idzie z ApplicationIcon).
        using (var iconStream = GetType().Assembly.GetManifestResourceStream("app.ico"))
            if (iconStream != null) Icon = new System.Drawing.Icon(iconStream);

        // KeyPreview pozwala formularzowi przechwycić Ctrl+O zanim trafi do kontrolki
        // (skrót przeszedł tu z dawnej pozycji menu po usunięciu paska menu).
        KeyPreview = true;

        // Kolejność dodawania determinuje dokowanie: kontrolka Dock.Fill musi trafić
        // do Controls jako pierwsza (najniższy z-order), aby pasek narzędzi (Top) i status
        // (Bottom) zarezerwowały swoje krawędzie, zamiast zostać przykryte przez Fill.
        BuildLayout();
        BuildStatusBar();
        BuildToolbar();   // jedyna kontrolka dokowana na górze (brak paska menu)

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

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // Plik przekazany z "Otwórz za pomocą" / skojarzenia — załaduj po pokazaniu okna.
        if (_startupFile != null && System.IO.File.Exists(_startupFile))
            await LoadFileAsync(_startupFile);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        // Ctrl+O = otwórz plik (skrót przeniesiony z dawnego menu).
        if (e.Control && e.KeyCode == Keys.O)
        {
            OnOpenClick(this, EventArgs.Empty);
            e.Handled = true;
        }
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
        // Wyższy pasek (komfort dla myszy — standard nowoczesnego desktopu) + większa
        // czcionka, by tekst wypełniał pasek. AutoSize=false, bo inaczej ToolStrip skurczyłby
        // się do naturalnej wysokości elementów (~25px).
        _toolbar.AutoSize = false;
        _toolbar.Height = 38;
        _toolbar.Font = new System.Drawing.Font("Segoe UI", 10f);

        // Lewa strona: plik | widok (zoom) | filtr.
        var openButton = new ToolStripButton("Open…") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        openButton.Click += OnOpenClick;

        var zoomOutButton = new ToolStripButton("Zoom −") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        zoomOutButton.Click += (_, __) => _gantt.ZoomOut();
        var zoomInButton = new ToolStripButton("Zoom +") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        zoomInButton.Click += (_, __) => _gantt.ZoomIn();
        var fitButton = new ToolStripButton("Fit to width") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        fitButton.Click += (_, __) => _gantt.ZoomToFit();

        _resourceCombo.DropDownStyle = ComboBoxStyle.DropDownList;  // tylko wybór z listy, bez wpisywania
        _resourceCombo.AutoSize = false;
        _resourceCombo.Width = 220;
        _resourceCombo.SelectedIndexChanged += OnResourceFilterChanged;

        // Prawa strona (Alignment.Right): pierwszy dodany ląduje najbardziej z prawej,
        // więc dodajemy Exit, GitHub, About → wizualnie od lewej: About | GitHub | Exit.
        var exitButton = new ToolStripButton("Exit")
        { DisplayStyle = ToolStripItemDisplayStyle.Text, Alignment = ToolStripItemAlignment.Right };
        exitButton.Click += (_, __) => Close();
        var githubButton = new ToolStripButton("GitHub")
        { DisplayStyle = ToolStripItemDisplayStyle.Text, Alignment = ToolStripItemAlignment.Right };
        githubButton.Click += (_, __) => OpenUrl(RepoUrl);
        var aboutButton = new ToolStripButton("About")
        { DisplayStyle = ToolStripItemDisplayStyle.Text, Alignment = ToolStripItemAlignment.Right };
        aboutButton.Click += (_, __) => ShowAbout();

        _toolbar.Items.Add(openButton);
        _toolbar.Items.Add(new ToolStripSeparator());
        _toolbar.Items.Add(zoomOutButton);
        _toolbar.Items.Add(zoomInButton);
        _toolbar.Items.Add(fitButton);
        _toolbar.Items.Add(new ToolStripSeparator());
        _toolbar.Items.Add(new ToolStripLabel("Show assigned to:"));
        _toolbar.Items.Add(_resourceCombo);
        _toolbar.Items.Add(exitButton);
        _toolbar.Items.Add(githubButton);
        _toolbar.Items.Add(aboutButton);
        Controls.Add(_toolbar);
    }

    // Wersja aplikacji czytana z assembly — jedno źródło dla stopki i okna About.
    private static string AppVersion
    {
        get
        {
            var v = typeof(MainForm).Assembly.GetName().Version;
            return v is null ? "" : $"v{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    private void ShowAbout()
    {
        string header = AppVersion.Length == 0 ? "MPP Viewer" : $"MPP Viewer {AppVersion}";
        MessageBox.Show(
            header + "\n\n" +
            "A portable, read-only viewer for Microsoft Project (.mpp) files.\n\n" +
            "MIT License\n" +
            RepoUrl,
            "About MPP Viewer", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
        _statusVersion.Text = AppVersion;
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
