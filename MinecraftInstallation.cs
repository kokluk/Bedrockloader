using System.Runtime.InteropServices;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    Button? minecraftLaunchButton;

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        if (currentPage != "Home" || minecraftLaunchButton is not { IsDisposed: false } button) return;
        button.Text = MinecraftInstallation.IsInstalled(settings.ContentPath)
            ? T("Launch Minecraft", "Minecraft'ı başlat") : T("Install Minecraft", "Minecraft'ı yükle");
    }
}

internal static class MinecraftInstallation
{
    // Query registrations for this Windows user, not leftover AppData pack folders.
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern int GetPackagesByPackageFamily(string family, ref uint count, IntPtr names, ref uint length, IntPtr buffer);

    internal static bool IsInstalled(string contentPath)
    {
        if (HasGameExecutable(contentPath)) return true;
        foreach (string drive in Environment.GetLogicalDrives())
            if (HasGameExecutable(Path.Combine(drive, "XboxGames", "Minecraft for Windows", "Content"))) return true;
        foreach (string family in new[] { "Microsoft.MinecraftUWP_8wekyb3d8bbwe", "Microsoft.MinecraftWindows_8wekyb3d8bbwe" })
        {
            uint count = 0, length = 0;
            int result = GetPackagesByPackageFamily(family, ref count, IntPtr.Zero, ref length, IntPtr.Zero);
            if ((result == 122 || result == 0) && count > 0) return true;
        }
        return false;
    }

    internal static bool HasGameExecutable(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            path = path.Trim().Trim('"');
            return File.Exists(Path.Combine(path, "Minecraft.Windows.exe"));
        }
        catch (ArgumentException) { return false; }
        catch (NotSupportedException) { return false; }
    }
}
