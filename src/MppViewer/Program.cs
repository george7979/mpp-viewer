namespace MppViewer;

internal static class Program
{
    /// <summary>
    /// Punkt wejścia aplikacji. Atrybut [STAThread] jest wymagany przez WinForms
    /// i shellowe okna dialogowe (OpenFileDialog korzysta z COM-owego IFileDialog,
    /// który działa wyłącznie w apartamencie STA).
    /// </summary>
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}
