// File: src/PostgresCopy.Desktop/Program.cs

namespace PostgresCopy.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
