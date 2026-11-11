using System.IO.Compression;
using System.Text.Json;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    const string BackupSettingsEntry = "bedrockloader-settings.json";
    static readonly string[] MinecraftBackupFolders =
        ["minecraftWorlds", "resource_packs", "behavior_packs", "skin_packs", "custom_skins",
         "development_resource_packs", "development_behavior_packs", "development_skin_packs"];
    static readonly (string Group, string Directory)[] CinnabarBackupFolders =
        [("cinnabar-worlds", "worlds"), ("cinnabar-shaders", "shaderpacks"), ("cinnabar-packs", "resource-packs/global"), ("cinnabar-mods", "mods")];
    bool backupBusy;
    [Flags]
    enum BackupParts { Settings = 1, Servers = 2, Worlds = 4, Packs = 8, Mods = 16, Skins = 32, Cinnabar = 64 }
    const BackupParts SmallBackup = BackupParts.Settings | BackupParts.Servers;

    sealed record PortableSettings(int Format, string Language, string Theme, string MaterialColor, bool Light,
        bool WindowsTheme, bool Tray, bool Glow, bool Animations, bool Advanced, DiscordOptions Discord, List<SavedServer> Servers,
        BackupParts? Included = null, bool? ClickSounds = null, bool? ShowUsage = null, string? LocalName = null);

    PortableSettings SnapshotSettings(BackupParts parts)
    {
        var source = parts.HasFlag(BackupParts.Settings) ? settings : new AppSettings();
        return new(1, source.Language, source.Theme, source.MaterialYouColor, source.LightMode, source.FollowWindowsTheme,
            source.CloseToTray, source.ShinyHomeIcons, source.AnimationsEnabled, source.AdvancedAnimationsEnabled, source.Discord,
            parts.HasFlag(BackupParts.Servers) ? [.. settings.Servers] : [], parts, source.ClickSounds, source.ShowUsage,
            NormalizeLocalName(source.LocalName));
    }

    static PortableSettings ReadPortableSettings(Stream stream)
    {
        using var data = new MemoryStream();
        byte[] buffer = new byte[8192];
        for (int size; (size = stream.Read(buffer)) != 0;)
        {
            if (data.Length + size > 1024 * 1024) throw new InvalidDataException("Settings backup exceeds 1 MB.");
            data.Write(buffer, 0, size);
        }
        data.Position = 0;
        var parsed = JsonSerializer.Deserialize<PortableSettings>(data);
        if (parsed?.Theme == "Blue") parsed = parsed with { Theme = "Midnight" };
        if (parsed is not { Format: 1 } || parsed.Language is not ("TR" or "EN") ||
            !ThemeChoices.Contains(parsed.Theme) || !MaterialColors.Any(color => color.Id == parsed.MaterialColor) ||
            parsed.Discord == null || parsed.Servers == null || parsed.Servers.Count > 5000 ||
            parsed.Discord.ApplicationId is not { Length: <= 32 } || parsed.Discord.LargeImage is not { Length: <= 128 } ||
            parsed.Discord.SmallImage is not { Length: <= 128 } ||
            parsed.Servers.Any(server => server == null || server.Port is < 1 or > 65535 ||
                string.IsNullOrWhiteSpace(server.Name) || server.Name.Length > 80 ||
                server.Host is not { Length: <= 253 } || BedrockServers.NormalizeHost(server.Host) == null))
            throw new InvalidDataException("Invalid BedrockLoader settings backup.");
        return parsed;
    }

    void ApplyPortableSettings(PortableSettings saved, BackupParts parts)
    {
        parts &= saved.Included ?? SmallBackup;
        if ((parts & SmallBackup) == 0) return;
        if (parts.HasFlag(BackupParts.Settings))
        {
            settings.Language = saved.Language;
            english = saved.Language == "EN";
            settings.Theme = saved.Theme;
            settings.MaterialYouColor = saved.MaterialColor;
            settings.LightMode = saved.Light;
            settings.FollowWindowsTheme = saved.WindowsTheme;
            lightMode = saved.WindowsTheme ? WindowsUsesLightTheme() : saved.Light;
            settings.CloseToTray = saved.Tray;
            settings.ShinyHomeIcons = saved.Glow;
            AnimatedRoundButton.AnimationsEnabled = settings.AnimationsEnabled = saved.Animations;
            AnimatedRoundButton.AdvancedAnimationsEnabled = settings.AdvancedAnimationsEnabled = saved.Advanced;
            settings.Discord = saved.Discord;
            settings.ClickSounds = saved.ClickSounds ?? settings.ClickSounds;
            settings.ShowUsage = saved.ShowUsage ?? settings.ShowUsage;
            if (saved.LocalName != null) settings.LocalName = NormalizeLocalName(saved.LocalName);
        }
        foreach (var server in parts.HasFlag(BackupParts.Servers) ? saved.Servers : [])
        {
            string host = BedrockServers.NormalizeHost(server.Host)!;
            if (settings.Servers.Any(s => s.Port == server.Port && string.Equals(BedrockServers.NormalizeHost(s.Host), host, StringComparison.OrdinalIgnoreCase))) continue;
            settings.Servers.Add(server with { Id = Guid.NewGuid(), Host = host });
        }
        WriteSettings();
        RefreshDiscordOptions();
        BuildShellRefresh();
    }

    async Task ExportBackupAsync(bool full)
    {
        if (backupBusy) return;
        if (ChooseBackupParts(full, importing: false) is not { } parts) return;
        if ((parts & ~SmallBackup) != 0 && !BackupGamesClosed())
        {
            ShowThemedPopup(T("Export full backup", "Tam yedeği dışa aktar"),
                T("Close Minecraft and Cinnabar before backing up game data.", "Oyun verilerini yedeklemeden önce Minecraft ve Cinnabar’ı kapat."));
            return;
        }
        using var picker = new SaveFileDialog { Filter = full ? "ZIP (*.zip)|*.zip" : "JSON (*.json)|*.json",
            FileName = full ? "BedrockLoader-backup.zip" : "BedrockLoader-settings.json" };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        string targetFile = picker.FileName;
        var snapshot = SnapshotSettings(parts);
        await RunBackupAsync(() => Task.Run(() =>
        {
            string temporary = targetFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var output = File.Open(temporary, FileMode.CreateNew))
                {
                    if (!full) JsonSerializer.Serialize(output, snapshot, new JsonSerializerOptions { WriteIndented = true });
                    else
                    {
                        using var archive = new ZipArchive(output, ZipArchiveMode.Create, true);
                        using (var settingsStream = archive.CreateEntry(BackupSettingsEntry).Open())
                            JsonSerializer.Serialize(settingsStream, snapshot);
                        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var (group, root) in BackupSources())
                        {
                            if (!parts.HasFlag(PartForGroup(group))) continue;
                            if (!Directory.Exists(root) || IsReparsePoint(root)) continue;
                            foreach (string child in Directory.EnumerateFileSystemEntries(root))
                            {
                                if (IsReparsePoint(child)) continue;
                                string name = Path.GetFileName(child);
                                string key = group + "/" + name;
                                for (int n = 2; !seen.Add(key); n++)
                                    key = group + "/" + Path.GetFileNameWithoutExtension(name) + "_" + n + Path.GetExtension(name);
                                if (!Directory.Exists(child))
                                {
                                    if (!Path.GetFullPath(child).Equals(Path.GetFullPath(temporary), StringComparison.OrdinalIgnoreCase) &&
                                        !Path.GetFullPath(child).Equals(Path.GetFullPath(targetFile), StringComparison.OrdinalIgnoreCase))
                                        archive.CreateEntryFromFile(child, key, CompressionLevel.Fastest);
                                    continue;
                                }
                                var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };
                                foreach (string file in Directory.EnumerateFiles(child, "*", options))
                                    if (!file.Equals(temporary, StringComparison.OrdinalIgnoreCase) && !file.Equals(targetFile, StringComparison.OrdinalIgnoreCase))
                                        archive.CreateEntryFromFile(file, key + "/" + Path.GetRelativePath(child, file).Replace('\\', '/'), CompressionLevel.Fastest);
                            }
                        }
                    }
                }
                File.Move(temporary, targetFile, true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }), T("Backup exported.", "Yedek dışa aktarıldı."));
    }

    IEnumerable<(string Group, string Path)> BackupSources()
    {
        yield return ("mods", modsFolder);
        yield return ("launcher-skins", Path.Combine(appDataRoot, "Skins", "Library"));
        foreach (var (group, directory) in CinnabarBackupFolders)
            yield return (group, Path.Combine(CinnabarDataRoot(), directory));
        foreach (string root in MinecraftBackupRoots())
            foreach (string name in MinecraftBackupFolders)
                yield return (name, Path.Combine(root, name));
    }

    static IEnumerable<string> MinecraftBackupRoots() =>
        WorldRoots().Select(Path.GetDirectoryName).OfType<string>().Prepend(ResolveComMojangRoot())
            .Distinct(StringComparer.OrdinalIgnoreCase).Where(Directory.Exists);

    async Task ImportBackupAsync(bool full)
    {
        if (backupBusy) return;
        if (ChooseBackupParts(full, importing: true) is not { } parts) return;
        using var picker = new OpenFileDialog { Filter = full ? "ZIP (*.zip)|*.zip" : "JSON (*.json)|*.json" };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        string sourceFile = picker.FileName;
        int restored = 0, skipped = 0;
        await RunBackupAsync(async () =>
        {
            var snapshot = await Task.Run(() =>
            {
                if (full) return RestoreFullBackup(sourceFile, parts, out restored, out skipped);
                using var input = File.OpenRead(sourceFile);
                return ReadPortableSettings(input);
            });
            ApplyPortableSettings(snapshot, parts);
        }, () => full ? T($"Restored: {restored}. Existing items skipped: {skipped}.", $"Geri yüklenen: {restored}. Atlanan mevcut öğeler: {skipped}.")
            : T("Settings imported.", "Ayarlar içe aktarıldı."));
    }

    PortableSettings RestoreFullBackup(string file, BackupParts parts, out int restored, out int skipped)
    {
        restored = skipped = 0;
        string? game = MinecraftBackupRoots().FirstOrDefault();
        bool gameDataSelected = (parts & (BackupParts.Worlds | BackupParts.Packs | BackupParts.Skins)) != 0;
        if (game == null && gameDataSelected)
            throw new InvalidOperationException(T("Open Minecraft once to create its data folder, then close it before restoring.",
                "Veri klasörünü oluşturmak için Minecraft’ı bir kez aç, ardından geri yüklemeden önce kapat."));
        var destinations = MinecraftBackupFolders.ToDictionary(key => key, key => game == null ? "" : Path.Combine(game, key), StringComparer.OrdinalIgnoreCase);
        destinations.Add("mods", modsFolder);
        destinations.Add("launcher-skins", Path.Combine(appDataRoot, "Skins", "Library"));
        foreach (var (group, directory) in CinnabarBackupFolders)
            destinations.Add(group, Path.Combine(CinnabarDataRoot(), directory));
        return RestoreBackupFiles(file, destinations, () => (parts & ~SmallBackup) == 0 || BackupGamesClosed(), out restored, out skipped, parts);
    }

    static bool BackupGamesClosed()
    {
        var processes = System.Diagnostics.Process.GetProcessesByName("cinnabar");
        foreach (var process in processes) process.Dispose();
        return processes.Length == 0 && BedrockSkinSource.IsGameClosed();
    }

    static PortableSettings RestoreBackupFiles(string file, Dictionary<string, string> destinations, Func<bool> canCommit,
        out int restored, out int skipped, BackupParts? selected = null)
    {
        restored = skipped = 0;
        using var archive = ZipFile.OpenRead(file);
        if (archive.Entries.Count > 250_000) throw new InvalidDataException("Too many files in backup.");
        var selectedEntries = archive.Entries.Where(entry =>
            selected == null || selected.Value.HasFlag(PartForGroup(entry.FullName.Split('/')[0]))).ToArray();
        if (selectedEntries.Any(entry => entry.Length > 32L * 1024 * 1024 * 1024) ||
            selectedEntries.Sum(entry => entry.Length) > 128L * 1024 * 1024 * 1024)
            throw new InvalidDataException("Backup exceeds extraction limit.");
        var settingsEntry = archive.GetEntry(BackupSettingsEntry) ?? throw new InvalidDataException("Missing backup settings.");
        PortableSettings portable;
        using (var input = settingsEntry.Open()) portable = ReadPortableSettings(input);
        var staging = Directory.CreateTempSubdirectory("BedrockLoader-restore-");
        var installed = new List<(string Path, bool Folder)>();
        try
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            byte[] buffer = new byte[64 * 1024];
            foreach (var entry in selectedEntries)
            {
                if (ReferenceEquals(entry, settingsEntry)) continue;
                string[] parts = entry.FullName.Split('/');
                if (parts.Length < 2 || !destinations.ContainsKey(parts[0]) ||
                    parts.Any(part => part.Length == 0 || part is "." or ".." || part.EndsWith('.') || part.EndsWith(' ') ||
                        part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || part.Contains('\\') ||
                        System.Text.RegularExpressions.Regex.IsMatch(part, @"^(CON|PRN|AUX|NUL|COM[1-9¹²³]|LPT[1-9¹²³])(?:\.|$)",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant)) ||
                    !paths.Add(entry.FullName))
                    throw new InvalidDataException("Invalid path in backup: " + entry.FullName);
                string target = Path.Combine(staging.FullName, Path.Combine(parts));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                using var input = entry.Open();
                using var output = File.Open(target, FileMode.CreateNew);
                long remaining = entry.Length;
                for (int size; (size = input.Read(buffer)) != 0;)
                {
                    remaining -= size;
                    if (remaining < 0) throw new InvalidDataException("Archive entry exceeds its declared size.");
                    output.Write(buffer, 0, size);
                }
                if (remaining != 0) throw new InvalidDataException("Archive entry is incomplete.");
            }
            if (!canCommit()) throw new InvalidOperationException("Close Minecraft and Cinnabar before restoring.");
            foreach (var (group, destination) in destinations)
            {
                string source = Path.Combine(staging.FullName, group);
                if (!Directory.Exists(source)) continue;
                for (DirectoryInfo? directory = new(destination); directory != null; directory = directory.Parent)
                    if (directory.Exists && IsReparsePoint(directory.FullName)) throw new IOException("Linked backup destination.");
                Directory.CreateDirectory(destination);
                foreach (string child in Directory.EnumerateFileSystemEntries(source))
                {
                    string target = Path.Combine(destination, Path.GetFileName(child));
                    if (Path.Exists(target)) { skipped++; continue; }
                    bool folder = Directory.Exists(child);
                    if (folder)
                    {
                        string temp = Path.Combine(destination, ".restore-" + Guid.NewGuid().ToString("N"));
                        try
                        {
                            Directory.CreateDirectory(temp);
                            foreach (var path in Directory.EnumerateFiles(child, "*", SearchOption.AllDirectories))
                            {
                                string copy = Path.Combine(temp, Path.GetRelativePath(child, path));
                                Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
                                File.Copy(path, copy);
                            }
                            Directory.Move(temp, target);
                        }
                        finally { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
                    }
                    else File.Move(child, target);
                    installed.Add((target, folder));
                    restored++;
                }
            }
            return portable;
        }
        catch
        {
            foreach (var item in installed)
                if (item.Folder) Directory.Delete(item.Path, true); else File.Delete(item.Path);
            throw;
        }
        finally { staging.Delete(true); }
    }

    static BackupParts PartForGroup(string group)
    {
        if (group.StartsWith("cinnabar-", StringComparison.OrdinalIgnoreCase)) return BackupParts.Cinnabar;
        return group.ToLowerInvariant() switch
        {
            "minecraftworlds" => BackupParts.Worlds,
            "mods" => BackupParts.Mods,
            "launcher-skins" or "skin_packs" or "custom_skins" or "development_skin_packs" => BackupParts.Skins,
            _ => BackupParts.Packs
        };
    }

    static bool IsReparsePoint(string path) => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    Task RunBackupAsync(Func<Task> action, string success) => RunBackupAsync(action, () => success);
    async Task RunBackupAsync(Func<Task> action, Func<string> success)
    {
        backupBusy = true;
        try { UpdateStatus(T("Backup operation in progress…", "Yedek işlemi sürüyor…")); await action(); UpdateStatus(success()); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or InvalidOperationException)
        { if (!IsDisposed) ShowError(ex, T("Backup failed", "Yedek işlemi başarısız")); }
        finally { backupBusy = false; }
    }
}
