namespace PdfReaderLight;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var form = new MainForm(args.Length > 0 ? args[0] : null);
        Application.Run(form);
    }
}
