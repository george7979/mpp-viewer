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

        // Kolejność dodawania determinuje dokowanie: kontrolka Dock.Fill musi trafić
        // do Controls jako pierwsza (najniższy z-order), aby menu (Top) i status (Bottom)
        // najpierw zarezerwowały swoje krawędzie, zamiast zostać przykryte przez Fill.
        BuildLayout();
        BuildStatusBar();
        BuildMenu();

        // Wykres odczytuje geometrię wierszy z tabeli i sam nasłuchuje jej przewijania.
        _gantt.AttachGrid(_grid);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        // Ustawiane dopiero teraz — w konstruktorze SplitContainer nie ma jeszcze
        // realnego rozmiaru, a SplitterDistance poza zakresem rzuca wyjątek.
        int max = _split.Width - _split.Panel2MinSize - _split.SplitterWidth;
        _split.SplitterDistance = Math.Clamp(480, _split.Panel1MinSize, Math.Max(_split.Panel1MinSize, max));
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
