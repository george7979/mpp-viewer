using System.Drawing.Drawing2D;
using System.Globalization;
using MppViewer.Models;

namespace MppViewer.Controls;

/// <summary>
/// Wykres Gantta sprzężony z tabelą zadań. Kluczowa zasada: wykres NIE liczy
/// pozycji wierszy samodzielnie — odczytuje je wprost z powiązanego
/// <see cref="TaskGridView"/> przez GetRowDisplayRectangle. Dzięki temu paski są
/// zawsze piksel w piksel wyrównane z wierszami tabeli, a przewijanie pionowe
/// tabeli automatycznie przewija wykres.
/// </summary>
public class GanttControl : Panel
{
    /// <summary>Wspólna wysokość nagłówka — tabela ustawia ColumnHeadersHeight na tę samą wartość.</summary>
    public const int HeaderHeight = 44;
    private const float DefaultPixelsPerDay = 15f;
    private float _pixelsPerDay = DefaultPixelsPerDay;
    private const int WheelNotchDelta = 120;   // standardowy ząbek kółka myszy = 120 jednostek Delta
    private const float ScrollLeftMargin = 12f;  // odstęp paska od lewej krawędzi po przewinięciu

    // Akcent kolorystyczny — pomarańcz (~#F58220). Brushe/peny jako statyczne
    // pola: żyją przez czas życia procesu, więc bez alokacji na każdym OnPaint.
    private static readonly Color AccentColor = Color.FromArgb(245, 130, 32);
    private static readonly Color AccentLight = Color.FromArgb(250, 213, 178);
    private static readonly Brush BarFillBrush = new SolidBrush(AccentLight);
    private static readonly Brush BarProgressBrush = new SolidBrush(AccentColor);
    private static readonly Pen BarBorderPen = new Pen(AccentColor);

    private IReadOnlyList<TaskItem> _tasks = Array.Empty<TaskItem>();
    private DateTime _projectStart;
    private DateTime _projectEnd;
    private int _totalWidth;
    private readonly HScrollBar _hScroll = new();
    private int _scrollOffsetX;
    private Dictionary<int, int> _taskRowIndex = new();
    private TaskGridView? _grid;
    private string? _highlightResource;

    // Stan chwytania (drag-pan) — przesuwanie wykresu myszą/palcem.
    private bool _panning;
    private Point _panStart;
    private int _panStartScrollX;
    private int _panStartFirstRow;
    private int _panRowHeight = 1;

    public GanttControl()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.Selectable, true);

        _hScroll.Dock = DockStyle.Bottom;
        _hScroll.SmallChange = 30;
        _hScroll.Scroll += (_, __) =>
        {
            _scrollOffsetX = _hScroll.Value;
            Invalidate();
        };
        Controls.Add(_hScroll);
    }

    /// <summary>
    /// Sprzęga wykres z tabelą zadań. Wykres odczytuje geometrię wierszy z tabeli
    /// i przerysowuje się przy każdym jej przewinięciu lub zmianie rozmiaru.
    /// </summary>
    public void AttachGrid(TaskGridView grid)
    {
        _grid = grid;
        _grid.Scroll += (_, __) => Invalidate();
        _grid.RowsAdded += (_, __) => Invalidate();
        _grid.RowsRemoved += (_, __) => Invalidate();
        _grid.Resize += (_, __) => Invalidate();
    }

    /// <summary>
    /// Podświetla zadania wybranej osoby, wyszarzając pozostałe. null = pokaż wszystkie
    /// w pełnym kolorze. To stan widoku — nie zmienia zbioru zadań ani geometrii.
    /// </summary>
    public void SetResourceFilter(string? resource)
    {
        _highlightResource = resource;
        Invalidate();
    }

    private bool IsDimmed(TaskItem task)
        => _highlightResource != null && !task.ResourceNames.Contains(_highlightResource);

    /// <summary>
    /// Przewija oś czasu poziomo tak, by podana data znalazła się przy lewej
    /// krawędzi wykresu (z małym marginesem). Zmienia tylko kamerę — nie model.
    /// </summary>
    public void ScrollToDate(DateTime date)
    {
        float absX = GanttMetrics.DateToX(date, _projectStart, _pixelsPerDay);
        int target = (int)Math.Round(absX - ScrollLeftMargin);
        int maxValue = Math.Max(_hScroll.Minimum, _hScroll.Maximum - _hScroll.LargeChange + 1);
        target = Math.Clamp(target, _hScroll.Minimum, maxValue);

        _hScroll.Value = target;     // programowe ustawienie NIE odpala zdarzenia Scroll,
        _scrollOffsetX = target;     // więc offset trzeba zsynchronizować ręcznie
        Invalidate();
    }

    /// <summary>
    /// Zoom osi czasu wokół punktu zaczepienia (anchorX w pikselach ekranu): punkt osi pod
    /// anchorX zostaje w miejscu, reszta rozsuwa/zsuwa się wokół niego. notches = liczba
    /// "ząbków" (dodatnie = przybliżenie, ujemne = oddalenie). Dolny limit = skala "fit to
    /// width", górny = MaxPixelsPerDay. Zmienia tylko skalę poziomą — geometria wierszy
    /// (sterowana przez tabelę) pozostaje nietknięta.
    /// </summary>
    private void ApplyZoom(int anchorX, int notches)
    {
        if (_tasks.Count == 0) return;

        float oldPpd = _pixelsPerDay;
        float factor = (float)Math.Pow(GanttMetrics.ZoomStep, notches);
        float fitPpd = GanttMetrics.FitPixelsPerDay(Width, _projectStart, _projectEnd);
        float newPpd = GanttMetrics.ClampZoom(oldPpd * factor, fitPpd);
        if (newPpd == oldPpd) return;                               // już na granicy zakresu

        _pixelsPerDay = newPpd;
        _totalWidth = GanttMetrics.TotalWidth(_projectStart, _projectEnd, _pixelsPerDay);
        _scrollOffsetX = GanttMetrics.ZoomedScrollOffset(anchorX, _scrollOffsetX, oldPpd, newPpd);

        RecalcHScroll();
        ClampScrollOffset();
        Invalidate();
    }

    /// <summary>Przybliża oś czasu o jeden ząbek, zaczepiając zoom na środku widoku.</summary>
    public void ZoomIn() => ApplyZoom(Width / 2, +1);

    /// <summary>Oddala oś czasu o jeden ząbek (nie schodzi poniżej "fit to width").</summary>
    public void ZoomOut() => ApplyZoom(Width / 2, -1);

    /// <summary>
    /// Dobiera skalę osi czasu tak, by cały projekt zmieścił się na szerokość widoku,
    /// i resetuje przewinięcie poziome. Pion (lista zadań) nadal się przewija.
    /// </summary>
    public void ZoomToFit()
    {
        if (_tasks.Count == 0) return;

        _pixelsPerDay = GanttMetrics.FitPixelsPerDay(Width, _projectStart, _projectEnd);
        _totalWidth = GanttMetrics.TotalWidth(_projectStart, _projectEnd, _pixelsPerDay);
        _scrollOffsetX = 0;

        RecalcHScroll();
        ClampScrollOffset();   // ten sam wzorzec synchronizacji co w ApplyZoom (offset 0 zawsze prawidłowy)
        Invalidate();
    }

    // Dosuwa _scrollOffsetX do prawidłowego zakresu paska i synchronizuje jego Value
    // (ZoomedScrollOffset może zwrócić wartość ujemną lub powyżej maksimum).
    // UWAGA: ten obustronny clamp jest konieczny — RecalcHScroll przycina offset tylko
    // OD GÓRY i tylko warunkowo, nie obsługuje wartości ujemnej (zoom-out przy kursorze
    // u lewej krawędzi). Nie upraszczać go przez poleganie na RecalcHScroll.
    private void ClampScrollOffset()
    {
        int maxValue = Math.Max(_hScroll.Minimum, _hScroll.Maximum - _hScroll.LargeChange + 1);
        _scrollOffsetX = Math.Clamp(_scrollOffsetX, _hScroll.Minimum, maxValue);
        _hScroll.Value = _scrollOffsetX;
    }

    public void Load(IReadOnlyList<TaskItem> tasks, DateTime start, DateTime end)
    {
        _tasks = tasks;
        _projectStart = start;
        _projectEnd = end > start ? end : start.AddDays(1);
        _taskRowIndex = tasks.Select((t, i) => (t.Id, i))
                             .ToDictionary(x => x.Id, x => x.i);

        // Skala startowa = domyślna, ale nie poniżej "fit to width" (krótki projekt, gdzie
        // fit wypada powyżej 15px/dzień, ładuje się od razu dopasowany — bez skoku przy
        // pierwszym oddaleniu). Gdy Width jeszcze nieznane, FitPixelsPerDay schodzi do progu
        // czytelności i start zostaje DefaultPixelsPerDay.
        _pixelsPerDay = GanttMetrics.ClampZoom(
            DefaultPixelsPerDay, GanttMetrics.FitPixelsPerDay(Width, _projectStart, _projectEnd));
        _totalWidth = GanttMetrics.TotalWidth(_projectStart, _projectEnd, _pixelsPerDay);
        _scrollOffsetX = 0;
        _hScroll.Value = 0;
        RecalcHScroll();
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        RecalcHScroll();
        Invalidate();
    }

    // Przewijanie kółkiem myszy nad wykresem przewija powiązaną tabelę,
    // co (przez zdarzenie Scroll) przerysuje wykres — pionowy ruch obu naraz.
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);

        // Ctrl + kółko = zoom osi czasu (zaczepiony pod kursorem). Bez Ctrl = scroll pionowy.
        if ((ModifierKeys & Keys.Control) == Keys.Control)
        {
            // Myszy/touchpady wysyłające Delta < jednego ząbka dają notches == 0 → bez zoomu,
            // dopóki skumulowane przewinięcie nie przekroczy ząbka. To zachowanie jest zamierzone.
            ApplyZoom(e.X, e.Delta / WheelNotchDelta);
            return;
        }

        if (_grid == null || _grid.RowCount == 0) return;

        int delta = -Math.Sign(e.Delta) * 3;
        int first = Math.Max(0, _grid.FirstDisplayedScrollingRowIndex) + delta;
        first = Math.Max(0, Math.Min(first, _grid.RowCount - 1));
        try { _grid.FirstDisplayedScrollingRowIndex = first; }
        catch (InvalidOperationException) { /* wiersz chwilowo niedostępny */ }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        if (CanFocus) Focus();   // bez focusu kontrolka nie dostaje zdarzeń kółka
    }

    // Drag-pan: lewy przycisk (lub palec — Windows promuje dotyk na zdarzenia myszy)
    // chwyta wykres i przesuwa go w obu osiach. Poziom płynnie, pion schodkowo (przez tabelę).
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || e.Y < HeaderHeight) return;  // nie łap nagłówka

        _panning = true;
        _panStart = e.Location;
        _panStartScrollX = _scrollOffsetX;
        _panStartFirstRow = _grid != null ? Math.Max(0, _grid.FirstDisplayedScrollingRowIndex) : 0;
        _panRowHeight = PanRowHeight();
        Cursor = Cursors.SizeAll;
        Capture = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_panning) return;

        // Poziom: treść podąża za kursorem (ciągnięcie w prawo cofa oś czasu) — płynnie.
        int maxX = Math.Max(_hScroll.Minimum, _hScroll.Maximum - _hScroll.LargeChange + 1);
        int targetX = Math.Clamp(_panStartScrollX - (e.X - _panStart.X), _hScroll.Minimum, maxX);
        if (targetX != _scrollOffsetX)
        {
            _scrollOffsetX = targetX;
            _hScroll.Value = targetX;
            Invalidate();
        }

        // Pion: schodkowo przez tabelę (DataGridView przewija się co wiersz).
        if (_grid != null && _grid.RowCount > 0)
        {
            int rowDelta = (int)Math.Round((double)(e.Y - _panStart.Y) / _panRowHeight);
            int targetFirst = Math.Clamp(_panStartFirstRow - rowDelta, 0, _grid.RowCount - 1);
            if (targetFirst != _grid.FirstDisplayedScrollingRowIndex)
                try { _grid.FirstDisplayedScrollingRowIndex = targetFirst; }
                catch (InvalidOperationException) { /* wiersz chwilowo niedostępny */ }
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!_panning) return;
        _panning = false;
        Capture = false;
        Cursor = Cursors.Default;
    }

    // Wysokość bieżącego wiersza tabeli — do przeliczenia pionowego przeciągnięcia na wiersze.
    private int PanRowHeight()
    {
        if (_grid == null || _grid.RowCount == 0) return 1;
        int idx = Math.Max(0, _grid.FirstDisplayedScrollingRowIndex);
        int h = _grid.Rows[idx].Height;
        return h > 0 ? h : 1;
    }

    private void RecalcHScroll()
    {
        int viewport = Math.Max(1, Width);
        // Zakres efektywny scrolla = Maximum - LargeChange + 1. Aby maks. offset
        // wynosił (_totalWidth - viewport), ustawiamy LargeChange = viewport oraz
        // Maximum = _totalWidth - 1 (z dolnym ograniczeniem, gdy treść mieści się w oknie).
        _hScroll.LargeChange = viewport;
        _hScroll.Maximum = Math.Max(viewport - 1, _totalWidth - 1);
        _hScroll.Enabled = _totalWidth > viewport;
        if (_scrollOffsetX > _hScroll.Maximum - _hScroll.LargeChange + 1)
        {
            _scrollOffsetX = Math.Max(0, _hScroll.Maximum - _hScroll.LargeChange + 1);
            _hScroll.Value = _scrollOffsetX;
        }
    }

    private float DateToX(DateTime d)
        => GanttMetrics.DateToX(d, _projectStart, _pixelsPerDay) - _scrollOffsetX;

    /// <summary>
    /// Pobiera dokładny prostokąt wiersza z tabeli. Zwraca false, gdy wiersz jest
    /// poza widocznym obszarem (tabela zwraca wtedy prostokąt o wysokości 0).
    /// </summary>
    private bool TryRowBounds(int rowIndex, out int top, out int height)
    {
        top = 0;
        height = 0;
        if (_grid == null || rowIndex < 0 || rowIndex >= _grid.RowCount) return false;
        var r = _grid.GetRowDisplayRectangle(rowIndex, false);
        if (r.Height == 0) return false;
        top = r.Top;
        height = r.Height;
        return true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;

        DrawTimelineHeader(g);

        int dataBottom = Math.Max(HeaderHeight, Height - _hScroll.Height);
        g.SetClip(new Rectangle(0, HeaderHeight, Width, dataBottom - HeaderHeight));
        DrawRowGuides(g);
        DrawMonthGridlines(g);
        DrawTaskBars(g);
        DrawDependencies(g);
        g.ResetClip();
    }

    // Tło wierszy odwzorowane 1:1 z tabeli: naprzemienne pasy + poziome separatory
    // rysowane z tych samych prostokątów wierszy co paski. Dzięki temu wiersz
    // tabeli i jego pasek dzielą dokładnie ten sam pas — oko przejeżdża wprost.
    private void DrawRowGuides(Graphics g)
    {
        if (_grid == null) return;
        using var altBrush = new SolidBrush(Color.FromArgb(245, 245, 248));
        using var linePen = new Pen(Color.FromArgb(225, 225, 230));
        for (int i = 0; i < _tasks.Count; i++)
        {
            if (!TryRowBounds(i, out int top, out int height)) continue;
            // Malujemy OBA stany jawnie, tą samą regułą i kolorami co tabela
            // (parzyste = SystemColors.Window/białe, nieparzyste = szary), żeby pasy
            // były w tej samej fazie — nie polegamy na szarym tle panelu dla "białych".
            Brush rowBrush = i % 2 == 1 ? altBrush : SystemBrushes.Window;
            g.FillRectangle(rowBrush, 0, top, Width, height);
            g.DrawLine(linePen, 0, top + height - 1, Width, top + height - 1);
        }
    }

    private void DrawTimelineHeader(Graphics g)
    {
        g.FillRectangle(SystemBrushes.Control, 0, 0, Width, HeaderHeight);
        g.DrawLine(Pens.Gray, 0, HeaderHeight - 1, Width, HeaderHeight - 1);

        if (_tasks.Count == 0) return;

        using var monthFont = new Font(Font.FontFamily, 8f, FontStyle.Bold);
        var current = new DateTime(_projectStart.Year, _projectStart.Month, 1);
        while (current <= _projectEnd)
        {
            float x = DateToX(current);
            if (x > -120 && x < Width + 20)
            {
                g.DrawLine(Pens.Gray, x, HeaderHeight - 8, x, HeaderHeight);
                g.DrawString(current.ToString("MMM yyyy", CultureInfo.InvariantCulture), monthFont, Brushes.Black, x + 3, 4);
            }
            current = current.AddMonths(1);
        }
    }

    private void DrawMonthGridlines(Graphics g)
    {
        if (_tasks.Count == 0) return;
        using var pen = new Pen(Color.FromArgb(238, 238, 238));
        var current = new DateTime(_projectStart.Year, _projectStart.Month, 1);
        while (current <= _projectEnd)
        {
            float x = DateToX(current);
            if (x >= 0 && x <= Width) g.DrawLine(pen, x, HeaderHeight, x, Height);
            current = current.AddMonths(1);
        }
    }

    private void DrawTaskBars(Graphics g)
    {
        for (int i = 0; i < _tasks.Count; i++)
        {
            if (!TryRowBounds(i, out int top, out int height)) continue;
            DrawTaskBar(g, _tasks[i], top, height, _tasks[i].IsSummary, IsDimmed(_tasks[i]));
        }
    }

    private void DrawTaskBar(Graphics g, TaskItem task, int top, int height, bool summary, bool dim)
    {
        if (task.Start == null || task.Finish == null) return;

        float x1 = DateToX(task.Start.Value);
        float x2 = DateToX(task.Finish.Value);
        float barWidth = Math.Max(3f, x2 - x1);

        if (x1 > Width || x1 + barWidth < 0) return;  // całość poza ekranem

        if (summary)
        {
            // Zadanie sumaryczne (rodzic): cienka belka z trójkątnymi końcówkami.
            var fill = dim ? Brushes.Silver : Brushes.Black;
            float h = 5f;
            float y = top + (height - h) / 2f;
            g.FillRectangle(fill, x1, y, barWidth, h);
            g.FillPolygon(fill, new[]
            {
                new PointF(x1, y), new PointF(x1, y + 9), new PointF(x1 + 6, y)
            });
            g.FillPolygon(fill, new[]
            {
                new PointF(x1 + barWidth, y), new PointF(x1 + barWidth, y + 9), new PointF(x1 + barWidth - 6, y)
            });
        }
        else
        {
            float pad = 4f;
            float y = top + pad;
            float h = Math.Max(6f, height - pad * 2);

            g.FillRectangle(dim ? Brushes.Gainsboro : BarFillBrush, x1, y, barWidth, h);
            if (!dim)
            {
                float progressWidth = barWidth * Math.Clamp(task.PercentComplete, 0, 100) / 100f;
                if (progressWidth > 0)
                    g.FillRectangle(BarProgressBrush, x1, y, progressWidth, h);
            }
            g.DrawRectangle(dim ? Pens.Silver : BarBorderPen, x1, y, barWidth, h);

            // Etykietę z przypisanymi rysujemy tylko dla wyróżnionych pasków — przy
            // wyszarzonych pomijamy, by nie zaśmiecać widoku po włączeniu filtra.
            if (!dim && task.ResourceNames.Count > 0)
            {
                string label = string.Join("; ", task.ResourceNames);
                float labelY = top + (height - Font.Height) / 2f;
                g.DrawString(label, Font, Brushes.DimGray, x1 + barWidth + 4, labelY);
            }
        }
    }

    private void DrawDependencies(Graphics g)
    {
        var oldMode = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using var pen = new Pen(Color.DimGray, 1.2f) { EndCap = LineCap.ArrowAnchor };

        for (int i = 0; i < _tasks.Count; i++)
        {
            var task = _tasks[i];
            if (task.Start == null) continue;
            if (!TryRowBounds(i, out int toTop, out int toH)) continue;

            foreach (var predId in task.PredecessorIds)
            {
                if (!_taskRowIndex.TryGetValue(predId, out int p)) continue;
                var pred = _tasks[p];
                if (pred.Finish == null) continue;
                if (!TryRowBounds(p, out int frTop, out int frH)) continue;  // pomiń, gdy koniec poza ekranem

                float fromX = DateToX(pred.Finish.Value);
                float fromY = frTop + frH / 2f;
                float toX = DateToX(task.Start.Value);
                float toY = toTop + toH / 2f;

                // Łamana pod kątem prostym (Finish-to-Start): wyjście w prawo,
                // pion do wiersza następnika, wejście w jego start.
                const float stub = 10f;
                var pts = new[]
                {
                    new PointF(fromX, fromY),
                    new PointF(fromX + stub, fromY),
                    new PointF(fromX + stub, toY),
                    new PointF(toX, toY)
                };
                g.DrawLines(pen, pts);
            }
        }

        g.SmoothingMode = oldMode;
    }
}
