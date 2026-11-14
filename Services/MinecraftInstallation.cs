using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    Button? minecraftLaunchButton;

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        if (settings.FollowWindowsTheme && lightMode != WindowsUsesLightTheme())
        {
            lightMode = !lightMode;
            BuildShellRefresh();
        }
        if (currentPage != "Home") return;
        if (cinnabarMode) RefreshCinnabarButton();
        else if (minecraftLaunchButton is { IsDisposed: false } button) RefreshMinecraftButton(button);
    }

    void RefreshMinecraftButton(Button button)
    {
        button.Text = MinecraftInstallation.IsInstalled(settings.ContentPath)
            ? T("Launch Minecraft", "Minecraft'ı başlat") : T("Install Minecraft", "Minecraft'ı yükle");
    }
}

internal static class MinecraftInstallation
{
    // Query registrations for this Windows user, not leftover AppData pack folders.
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern int GetPackagesByPackageFamily(string family, ref uint count, IntPtr names, ref uint length, IntPtr buffer);

    static readonly string[] Families = ["Microsoft.MinecraftUWP_8wekyb3d8bbwe", "Microsoft.MinecraftWindows_8wekyb3d8bbwe"];

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern int GetPackagePathByFullName(string packageFullName, ref uint length, System.Text.StringBuilder? path);

    internal static IReadOnlyList<string> FindGameDirectories(string contentPath)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (HasGameExecutable(contentPath)) result.Add(Path.GetFullPath(contentPath.Trim().Trim('"')));
        foreach (string drive in Environment.GetLogicalDrives())
        {
            string path = Path.Combine(drive, "XboxGames", "Minecraft for Windows", "Content");
            if (HasGameExecutable(path)) result.Add(path);
        }
        foreach (string family in Families)
        {
            uint count = 0, length = 0;
            if (GetPackagesByPackageFamily(family, ref count, IntPtr.Zero, ref length, IntPtr.Zero) != 122 ||
                count is 0 or > 256 || length is 0 or > 65536) continue;
            IntPtr names = Marshal.AllocHGlobal(checked((int)count * IntPtr.Size));
            IntPtr buffer = IntPtr.Zero;
            try
            {
                buffer = Marshal.AllocHGlobal(checked((int)length * 2));
                if (GetPackagesByPackageFamily(family, ref count, names, ref length, buffer) != 0) continue;
                for (int i = 0; i < count; i++)
                {
                    string? fullName = Marshal.PtrToStringUni(Marshal.ReadIntPtr(names, i * IntPtr.Size));
                    if (string.IsNullOrEmpty(fullName)) continue;
                    uint capacity = 0;
                    if (GetPackagePathByFullName(fullName, ref capacity, null) != 122 || capacity is 0 or > 32768) continue;
                    var path = new System.Text.StringBuilder((int)capacity);
                    if (GetPackagePathByFullName(fullName, ref capacity, path) == 0) result.Add(path.ToString());
                }
            }
            finally { Marshal.FreeHGlobal(names); if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer); }
        }
        return result.ToArray();
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    static extern int GetPackageFamilyName(IntPtr process, ref uint length, StringBuilder? name);

    internal sealed record RunningGame(DateTime? StartedUtc, string? Version);

    internal static RunningGame? FindRunningGame(string contentPath)
    {
        IReadOnlyList<string>? installs = null;
        var candidates = new List<RunningGame>();
        foreach (var process in Process.GetProcessesByName("Minecraft.Windows"))
        {
            using (process)
            {
                try
                {
                    string? exe = null;
                    try { exe = process.MainModule?.FileName; }
                    catch (Win32Exception) { }
                    if (!IsBedrockPackage(process))
                    {
                        if (exe is null) continue;
                        installs ??= FindGameDirectories(contentPath);
                        if (!installs.Contains(Path.GetDirectoryName(exe), StringComparer.OrdinalIgnoreCase)) continue;
                    }
                    var started = process.StartTime.ToUniversalTime();
                    string? version = null;
                    if (exe is not null)
                    {
                        try
                        {
                            var info = FileVersionInfo.GetVersionInfo(exe);
                            if (info.FileMajorPart > 0)
                                version = new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart).ToString();
                        }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
                    }
                    if (!process.HasExited) candidates.Add(new RunningGame(started, version));
                }
                catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or UnauthorizedAccessException) { }
            }
        }
        return candidates.OrderByDescending(game => game.StartedUtc).FirstOrDefault();
    }

    static bool IsBedrockPackage(Process process)
    {
        try
        {
            uint length = 0;
            IntPtr handle = process.Handle;
            if (GetPackageFamilyName(handle, ref length, null) != 122 || length is 0 or > 256) return false;
            var family = new StringBuilder((int)length);
            return GetPackageFamilyName(handle, ref length, family) == 0 &&
                Families.Contains(family.ToString(), StringComparer.OrdinalIgnoreCase);
        }
        catch (Win32Exception) { return false; }
    }

    [ComImport, Guid("2E941141-7F97-4756-BA1D-9DECDE894A3D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IApplicationActivationManager
    {
        [PreserveSig]
        int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string appId,
            [MarshalAs(UnmanagedType.LPWStr)] string? arguments, uint options, out uint processId);
    }

    internal static bool TryLaunch(string contentPath)
    {
        var type = Type.GetTypeFromCLSID(new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C"));
        object? instance = null;
        try
        {
            if (type is not null && (instance = Activator.CreateInstance(type)) is IApplicationActivationManager manager)
                foreach (string family in Families)
                    if (manager.ActivateApplication(family + "!App", null, 0, out uint processId) >= 0 && processId != 0)
                        return true;
        }
        catch (COMException) { }
        finally { if (instance is not null) Marshal.ReleaseComObject(instance); }

        foreach (string directory in FindGameDirectories(contentPath))
        {
            string executable = Path.Combine(directory, "Minecraft.Windows.exe");
            if (!File.Exists(executable)) continue;
            try
            {
                using var process = Process.Start(new ProcessStartInfo(executable) { WorkingDirectory = directory });
                if (process is not null) return true;
            }
            catch (Win32Exception) { }
        }
        return false;
    }

    internal static bool IsInstalled(string contentPath)
    {
        if (HasGameExecutable(contentPath)) return true;
        foreach (string drive in Environment.GetLogicalDrives())
            if (HasGameExecutable(Path.Combine(drive, "XboxGames", "Minecraft for Windows", "Content"))) return true;
        foreach (string family in Families)
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
