using System.Diagnostics;

namespace KoklukBedrockModloader;

internal sealed record BedrockSkinDiscovery(bool GdkData, bool UwpData, IReadOnlyList<SkinEntry> Candidates);

internal static class BedrockSkinSource
{
    // This discovery is read-only. A PNG in the cache proves only that it was present locally.
    internal static BedrockSkinDiscovery Discover(string localName)
    {
        string gdk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Minecraft Bedrock", "Users");
        string uwp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", "Microsoft.MinecraftUWP_8wekyb3d8bbwe", "LocalState", "games", "com.mojang");
        var roots = new List<string>();
        if (Directory.Exists(uwp)) roots.Add(uwp);
        if (Directory.Exists(gdk))
        {
            foreach (string user in Directories(gdk, 20).OrderBy(path => Path.GetFileName(path).Equals("Shared", StringComparison.OrdinalIgnoreCase) ? 0 : 1))
            {
                try
                {
                    string root = SafeChild(user, "games", "com.mojang");
                    if (Directory.Exists(root)) roots.Add(root);
                }
                catch (InvalidDataException) { } catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        var entries = new List<SkinEntry>();
        var seen = new HashSet<string>();
        foreach (string root in roots)
        {
            try
            {
                string folder = SafeChild(root, "custom_skins");
                foreach (string file in Files(folder, "*.png", 20))
                {
                    if (entries.Count >= SkinLibrary.Capacity) break;
                    try
                    {
                        byte[] png = SkinLibrary.Normalize(SkinLibrary.ReadLimited(SafeChild(folder, Path.GetFileName(file)), SkinLibrary.MaxPng));
                        if (seen.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(png))))
                            entries.Add(new SkinEntry(Guid.NewGuid(), $"{localName} {entries.Count + 1}", false, false, null, png, DateTime.MinValue));
                    }
                    catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException) { }
                }
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException) { }
            foreach (string category in new[] { "skin_packs", "development_skin_packs" })
            {
                string folder;
                try { folder = SafeChild(root, category); }
                catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException) { continue; }
                foreach (string candidate in Directories(folder, 32).Concat(PackFiles(folder, 32)))
                {
                    int remaining = SkinLibrary.Capacity - entries.Count;
                    if (remaining <= 0) break;
                    try
                    {
                        string path = SafeChild(folder, Path.GetFileName(candidate));
                        entries.AddRange(Directory.Exists(path) ? SkinLibrary.ReadPackFolder(path, remaining) : SkinLibrary.ReadPack(path, remaining));
                    }
                    catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or KeyNotFoundException or System.Text.Json.JsonException) { }
                }
            }
        }
        return new BedrockSkinDiscovery(roots.Any(path => path.StartsWith(gdk + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)), roots.Contains(uwp), entries);
    }

    internal static List<SkinEntry> ScanPacks(string folder)
    {
        var entries = new List<SkinEntry>();
        foreach (string file in PackFiles(folder, 128))
        {
            int remaining = SkinLibrary.Capacity - entries.Count;
            if (remaining <= 0) break;
            try { entries.AddRange(SkinLibrary.ReadPack(SafeChild(folder, Path.GetFileName(file)), remaining)); }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or KeyNotFoundException or System.Text.Json.JsonException) { }
        }
        return entries;
    }

    static IEnumerable<string> PackFiles(string folder, int limit) =>
        Files(folder, "*", limit * 4)
            .Where(path => Path.GetExtension(path) is "" || Path.GetExtension(path).Equals(".mcpack", StringComparison.OrdinalIgnoreCase))
            .Take(limit);

    static string[] Directories(string root, int limit)
    {
        try { return Directory.Exists(root) ? Directory.EnumerateDirectories(root).Take(limit).ToArray() : []; }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException) { return []; }
    }

    static string[] Files(string root, string filter, int limit)
    {
        try { return Directory.Exists(root) ? Directory.EnumerateFiles(root, filter).Take(limit).ToArray() : []; }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException) { return []; }
    }

    internal static string SafeChild(string root, params string[] parts)
    {
        string path = root;
        Check(path);
        foreach (string part in parts)
        {
            if (part is "." or ".." || string.IsNullOrEmpty(part) || part.IndexOfAny(['/', (char)92, ':']) >= 0)
                throw new InvalidDataException("Invalid local skin path.");
            path = Path.Combine(path, part);
            Check(path);
        }
        return path;

        static void Check(string item)
        {
            try
            {
                if ((File.GetAttributes(item) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Linked game files are excluded.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }

    internal static bool IsGameClosed()
    {
        try
        {
            foreach (string name in new[] { "Minecraft.Windows", "Minecraft", "Minecraft.WindowsPreview" })
            {
                var processes = Process.GetProcessesByName(name);
                foreach (var process in processes) process.Dispose();
                if (processes.Length != 0) return false;
            }
            return true;
        }
        catch { return false; }
    }
}
