using System.Runtime.InteropServices;

namespace KoklukBedrockModloader;

internal static class Program
{
    const string InstanceName = "Local\\Kokluk.BedrockLoader.SingleInstance.v1";
    internal static readonly int RestoreMessage = RegisterWindowMessage("Kokluk.BedrockLoader.Restore.v1");

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int RegisterWindowMessage(string message);

    [DllImport("user32.dll")]
    static extern bool PostMessage(nint window, int message, nint wParam, nint lParam);

    [STAThread]
    private static void Main()
    {
        using var instance = new Mutex(initiallyOwned: false, InstanceName, out bool firstInstance);
        if (!firstInstance)
        {
            if (RestoreMessage != 0) PostMessage(0xffff, RestoreMessage, 0, 0);
            return;
        }
        ApplicationConfiguration.Initialize();
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is not Exception error) return;
            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BedrockLoader", "crashes");
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, $"crash-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Environment.ProcessId}.txt"), error.ToString());
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        };
        Application.SetDefaultFont(LauncherFont.Create(9));
        Application.Run(new MainForm());
    }
}
