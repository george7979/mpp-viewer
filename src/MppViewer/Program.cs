namespace MppViewer;

internal static class Program
{
    /// <summary>
    /// Punkt wejścia aplikacji. Atrybut [STAThread] jest wymagany przez WinForms
    /// i shellowe okna dialogowe (OpenFileDialog korzysta z COM-owego IFileDialog,
    /// który działa wyłącznie w apartamencie STA).
    /// </summary>
    [STAThread]
    private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        // args[0] = ścieżka pliku przekazana przez "Otwórz za pomocą" / skojarzenie formatu.
        string? startupFile = args.Length > 0 ? args[0] : null;
        Application.Run(new MainForm(startupFile));
    }
}
