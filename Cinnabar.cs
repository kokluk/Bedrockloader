using System.Diagnostics;
using System.Text.Json;
using Microsoft.Win32;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    const string CinnabarReleases = "https://github.com/cinnabar-mc/cinnabar/releases";
    bool cinnabarMode, cinnabarBusy;
    Button? cinnabarLaunchButton, cinnabarPlayButton;
    int cinnabarPercent = -1;
    bool cinnabarUpdating, cinnabarChecking;
    Version? cinnabarLatest;
    Version? cinnabarProbedVersion;
    string? cinnabarProbedPath;
    DateTime cinnabarProbedWriteUtc;
    DateTime cinnabarCheckedAt;
    string CinnabarRuntimeRoot => Path.Combine(appDataRoot, "CinnabarRuntime");

    void ShowCinnabarProgress(int percent)
    {
        cinnabarPercent = percent;
        RefreshCinnabarButton();
        RefreshDownloadTaskbar();
    }

    void RefreshCinnabarButton()
    {
        if (!cinnabarMode || currentPage != "Home" || cinnabarLaunchButton is not { IsDisposed: false } button) return;
        button.Text = CinnabarLaunchText();
        button.Enabled = !cinnabarBusy;
        if (cinnabarPlayButton is { IsDisposed: false } play) play.Visible = !cinnabarBusy && CinnabarUpdateAvailable(FindCinnabar());
    }

    string CinnabarLaunchText()
    {
        if (cinnabarPercent >= 0)
            return cinnabarUpdating ? T($"Updating Cinnabar {cinnabarPercent}%", $"Cinnabar güncelleniyor %{cinnabarPercent}")
                : T($"Downloading Cinnabar {cinnabarPercent}%", $"Cinnabar indiriliyor %{cinnabarPercent}");
        string? executable = FindCinnabar();
        return CinnabarUpdateAvailable(executable) ? T("Update Cinnabar", "Cinnabar’ı güncelle") :
            executable != null ? T("Launch Cinnabar", "Cinnabar’ı başlat") : T("Install Cinnabar", "Cinnabar’ı yükle");
    }

    bool CinnabarUpdateAvailable(string? executable) => cinnabarLatest != null &&
        CinnabarInstalledVersion(executable) is { } installed && installed < cinnabarLatest;

    void CompareGames() => ShowThemedPopup(T("Minecraft and Cinnabar", "Minecraft ve Cinnabar"),
        T("Minecraft Bedrock is the official Mojang game and supports Marketplace, Realms and native Bedrock packs.\n\nCinnabar is an independent Rust client in beta. It has separate local data and supports compatible Java shader packs. Feature and server compatibility can differ.",
          "Minecraft Bedrock, Marketplace, Realms ve Bedrock paketlerini destekleyen resmî Mojang oyunudur.\n\nCinnabar, Rust ile geliştirilen bağımsız bir beta istemcisidir. Yerel verileri ayrı saklar ve uyumlu Java shader paketlerini destekler. Özellikler ve sunucu uyumluluğu değişebilir."));

    void ExplainCinnabar() => ShowThemedPopup(T("What is Cinnabar?", "Cinnabar nedir?"),
        T("Cinnabar is an independent, unofficial client compatible with Minecraft Bedrock. It has its own launcher and shader settings. It uses Rust and targets better performance. It is in beta and supports compatible Java shader packs through its Enhanced renderer.",
          "Cinnabar, Minecraft Bedrock ile uyumlu, bağımsız ve resmî olmayan bir istemcidir. Kendi başlatıcısı ve shader ayarları vardır. Rust ile geliştiriliyor ve performansı artırmayı hedefliyor. Beta aşamasında. Enhanced görüntüleyici, uyumlu Java shader paketlerini destekliyor."));

    string? FindCinnabar()
    {
        if (!string.IsNullOrWhiteSpace(settings.CinnabarExecutable) &&
            Path.IsPathFullyQualified(settings.CinnabarExecutable) && File.Exists(settings.CinnabarExecutable)) return settings.CinnabarExecutable;
        if (Directory.Exists(CinnabarRuntimeRoot))
        {
            var options = new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 5,
                IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
            string? managed = Directory.EnumerateFiles(CinnabarRuntimeRoot, "cinnabar.exe", options)
                .Where(path => !Path.GetRelativePath(CinnabarRuntimeRoot, path).StartsWith(".staging-", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (managed != null) return managed;
        }
        var roots = new List<string>();
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            using var uninstall = hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (uninstall == null) continue;
            foreach (string key in uninstall.GetSubKeyNames())
            {
                using var entry = uninstall.OpenSubKey(key);
                if (entry?.GetValue("DisplayName") is not string name || !name.Equals("Cinnabar", StringComparison.OrdinalIgnoreCase)) continue;
                if (entry.GetValue("InstallLocation") is string path && Path.IsPathFullyQualified(path)) roots.Add(path);
            }
        }
        roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Cinnabar"));
        roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Cinnabar"));
        roots.Add(AppContext.BaseDirectory);
        roots.Add(Path.Combine(AppContext.BaseDirectory, "Cinnabar"));
        return roots.SelectMany(root => new[] { Path.Combine(root, "Cinnabar.exe"), Path.Combine(root, "CinnabarLauncher.exe") })
            .FirstOrDefault(File.Exists);
    }

    Version? CinnabarInstalledVersion(string? executable)
    {
        if (executable == null) return null;
        if (string.Equals(executable, cinnabarProbedPath, StringComparison.OrdinalIgnoreCase) &&
            cinnabarProbedWriteUtc == File.GetLastWriteTimeUtc(executable) && cinnabarProbedVersion != null)
            return cinnabarProbedVersion;
        var info = FileVersionInfo.GetVersionInfo(executable);
        if (TryCinnabarVersion(info.ProductVersion ?? "", out var version) ||
            TryCinnabarVersion(info.FileVersion ?? "", out version)) return version;
        if (string.Equals(executable, settings.CinnabarExecutable, StringComparison.OrdinalIgnoreCase) &&
            (settings.CinnabarExecutableWriteUtc == default || settings.CinnabarExecutableWriteUtc == File.GetLastWriteTimeUtc(executable)) &&
            TryCinnabarVersion(settings.CinnabarInstalledTag, out version)) return version;
        return null;
    }

    async Task ProbeCinnabarVersionAsync(string executable, CancellationToken token)
    {
        DateTime writeTime = File.GetLastWriteTimeUtc(executable);
        if (string.Equals(executable, cinnabarProbedPath, StringComparison.OrdinalIgnoreCase) &&
            writeTime == cinnabarProbedWriteUtc && cinnabarProbedVersion != null) return;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            var command = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, WorkingDirectory = Path.GetDirectoryName(executable)! };
            command.ArgumentList.Add("--update-build-info");
            using var process = Process.Start(command);
            if (process == null) return;
            string? output = await process.StandardOutput.ReadLineAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            if (process.ExitCode != 0 || string.IsNullOrEmpty(output) || output.Length > 4096) return;
            using var json = JsonDocument.Parse(output);
            if (json.RootElement.TryGetProperty("version", out var value) &&
                TryCinnabarVersion(value.GetString() ?? "", out var version))
            {
                cinnabarProbedPath = executable;
                cinnabarProbedWriteUtc = writeTime;
                cinnabarProbedVersion = version;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException or
            OperationCanceledException or System.ComponentModel.Win32Exception) { }
    }

    async Task CheckCinnabarUpdateAsync(bool interactive = true)
    {
        if (cinnabarBusy || cinnabarChecking || (!interactive && DateTime.UtcNow - cinnabarCheckedAt < TimeSpan.FromMinutes(15))) return;
        cinnabarChecking = true;
        cinnabarCheckedAt = DateTime.UtcNow;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(updatesLifetime.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            using var json = await ReadCinnabarReleaseAsync(timeout.Token);
            string tag = json.RootElement.GetProperty("tag_name").GetString() ?? "";
            if (!TryCinnabarVersion(tag, out cinnabarLatest)) throw new InvalidDataException("Invalid Cinnabar release version.");
            string? executable = FindCinnabar();
            if (executable != null) await ProbeCinnabarVersionAsync(executable, timeout.Token);
            RefreshCinnabarButton();
            if (!interactive || IsDisposed || !cinnabarMode || currentPage != "Home") return;

            var installed = CinnabarInstalledVersion(executable);
            string message;
            if (executable == null)
                message = T($"Install Cinnabar {tag}?", $"Cinnabar {tag} kurulsun mu?");
            else if (installed == null)
                message = T($"The installed version could not be verified. Install official {tag}?", $"Kurulu sürüm doğrulanamadı. Resmî {tag} sürümü kurulsun mu?");
            else if (installed < cinnabarLatest)
                message = T($"Update available: {installed} → {tag}.", $"Güncelleme var: {installed} → {tag}.");
            else
            {
                ShowThemedPopup("Cinnabar", T($"Cinnabar is up to date ({tag}).", $"Cinnabar güncel ({tag})."));
                return;
            }
            if (ShowThemedPopup("Cinnabar", message, true, executable == null ? T("Install", "Yükle") : T("Update", "Güncelle")) == DialogResult.Yes)
                await LaunchCinnabarAsync(forceInstall: true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (interactive && !IsDisposed && !updatesLifetime.IsCancellationRequested && cinnabarMode && currentPage == "Home")
                ShowThemedPopup("Cinnabar", T("Could not check for updates. Try again later.", "Güncellemeler denetlenemedi. Daha sonra tekrar dene."));
        }
        finally { cinnabarChecking = false; }
    }

    static bool TryCinnabarVersion(string tag, out Version? version)
    {
        if (Version.TryParse(tag.Trim().TrimStart('v', 'V'), out var parsed))
        {
            version = new Version(parsed.Major, parsed.Minor, Math.Max(0, parsed.Build), Math.Max(0, parsed.Revision));
            return version > new Version(0, 0, 0, 0);
        }
        version = null;
        return false;
    }

    async Task<JsonDocument> ReadCinnabarReleaseAsync(CancellationToken cancel) =>
        JsonDocument.Parse(await Http.GetStringAsync("https://api.github.com/repos/cinnabar-mc/cinnabar/releases/latest", cancel));

    async Task LaunchCinnabarAsync(bool playOnly = false, bool forceInstall = false)
    {
        if (cinnabarBusy || backupBusy) return;
        cinnabarBusy = true;
        string? installer = null;
        string? staging = null;
        string? extraction = null;
        try
        {
            string? executable = FindCinnabar();
            if (executable != null && !forceInstall && (playOnly || !CinnabarUpdateAvailable(executable)))
            {
                using var game = Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(executable)! });
                return;
            }
            cinnabarUpdating = executable != null;
            if (!forceInstall && ShowThemedPopup("Cinnabar", cinnabarUpdating
                ? T("Download and apply the official Cinnabar update?", "Resmî Cinnabar güncellemesi uygulansın mı?")
                : T("Download and install the official Windows build?", "Resmî Windows sürümü indirilip kurulsun mu?"),
                true, cinnabarUpdating ? T("Update", "Güncelle") : T("Install", "Yükle")) != DialogResult.Yes) return;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(updatesLifetime.Token);
            timeout.CancelAfter(TimeSpan.FromMinutes(35));
            ShowCinnabarProgress(0);
            UpdateStatus(T("Downloading Cinnabar…", "Cinnabar indiriliyor…"));
            using var json = await ReadCinnabarReleaseAsync(timeout.Token);
            var root = json.RootElement;
            string tag = root.GetProperty("tag_name").GetString() ?? "";
            const string name = "Cinnabar-x64.msi";
            var asset = root.GetProperty("assets").EnumerateArray().FirstOrDefault(item => item.GetProperty("name").GetString() == name);
            if (asset.ValueKind == JsonValueKind.Undefined) throw new InvalidDataException("Cinnabar x64 MSI unavailable.");
            string? digest = asset.GetProperty("digest").GetString();
            long size = asset.GetProperty("size").GetInt64();
            if (tag.Length is 0 or > 100 || digest is not { Length: 71 } || !digest.StartsWith("sha256:", StringComparison.Ordinal) ||
                !digest[7..].All(Uri.IsHexDigit) || size is <= 0 or > ReleaseUpdates.MaxDownload)
                throw new InvalidDataException("Invalid Cinnabar release metadata.");
            var release = new UpdateRelease(new Version(0, 0), tag, "", new Uri(CinnabarReleases),
                new Uri(CinnabarReleases + "/download/" + Uri.EscapeDataString(tag) + "/" + name), digest[7..], size);
            installer = Path.Combine(Path.GetTempPath(), "Cinnabar-" + Guid.NewGuid().ToString("N") + ".msi");
            int reported = -1;
            IProgress<long> progress = new Progress<long>(bytes => { if (cinnabarBusy && !IsDisposed) ShowCinnabarProgress((int)(bytes * 100 / size)); });
            await ReleaseUpdates.DownloadVerifiedAsync(Http, release, installer, bytes =>
            {
                int percent = (int)(bytes * 100 / size);
                if (percent == reported) return;
                reported = percent;
                progress.Report(bytes);
            }, timeout.Token, null);
            using (var stream = File.OpenRead(installer))
            {
                var header = new byte[8];
                stream.ReadExactly(header);
                if (!header.AsSpan().SequenceEqual(new byte[] { 0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1 }))
                    throw new InvalidDataException("Invalid MSI header.");
            }
            string id = Guid.NewGuid().ToString("N");
            string tempRoot = Path.GetTempPath();
            extraction = Path.Combine(tempRoot, "cb-" + id[..8]);
            staging = Path.Combine(CinnabarRuntimeRoot, ".staging-" + id);
            string target = Path.Combine(CinnabarRuntimeRoot, id);
            Directory.CreateDirectory(extraction);
            var command = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "msiexec.exe")) { UseShellExecute = false, CreateNoWindow = true };
            foreach (string arg in new[] { "/a", installer, "/qn", "/norestart", "TARGETDIR=" + extraction }) command.ArgumentList.Add(arg);
            using var setup = Process.Start(command) ?? throw new IOException("Cinnabar setup could not start.");
            await setup.WaitForExitAsync();
            if (setup.ExitCode != 0) throw new IOException("Cinnabar extraction failed (" + setup.ExitCode + ").");
            string extracted = Directory.EnumerateFiles(extraction, "cinnabar.exe", SearchOption.AllDirectories).SingleOrDefault()
                ?? throw new InvalidDataException("Cinnabar executable missing from the installer.");
            string source = Path.GetDirectoryName(extracted)!;
            Directory.CreateDirectory(CinnabarRuntimeRoot);
            await Task.Run(() => StageCinnabarRuntime(source, staging), timeout.Token);
            Directory.Move(staging, target);
            var previous = (settings.CinnabarExecutable, settings.CinnabarInstalledTag, settings.CinnabarExecutableWriteUtc);
            settings.CinnabarExecutable = Path.Combine(target, Path.GetFileName(extracted));
            settings.CinnabarInstalledTag = tag;
            settings.CinnabarExecutableWriteUtc = File.GetLastWriteTimeUtc(settings.CinnabarExecutable);
            try { WriteSettings(); }
            catch
            {
                (settings.CinnabarExecutable, settings.CinnabarInstalledTag, settings.CinnabarExecutableWriteUtc) = previous;
                throw;
            }
            if (TryCinnabarVersion(tag, out var latest)) cinnabarLatest = latest;
            UpdateStatus(T("Cinnabar is ready to launch.", "Cinnabar başlatmaya hazır."));
        }
        catch (Exception ex)
        {
            if (!IsDisposed) ShowError(ex, T("Cinnabar could not be opened", "Cinnabar açılamadı"));
        }
        finally
        {
            if (installer != null) try { File.Delete(installer); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            DeleteCinnabarStaging(staging, CinnabarRuntimeRoot, ".staging-");
            DeleteCinnabarStaging(extraction, Path.GetTempPath(), "cb-");
            cinnabarBusy = false;
            ShowCinnabarProgress(-1);
        }
    }

    static void StageCinnabarRuntime(string source, string staging)
    {
        if (string.Equals(Path.GetPathRoot(source), Path.GetPathRoot(staging), StringComparison.OrdinalIgnoreCase))
        {
            Directory.Move(source, staging);
            return;
        }
        Directory.CreateDirectory(staging);
        foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(staging, Path.GetRelativePath(source, directory)));
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(staging, Path.GetRelativePath(source, file)));
    }

    static void DeleteCinnabarStaging(string? path, string root, string prefix)
    {
        if (path == null || !Directory.Exists(path) || !Path.GetFileName(path).StartsWith(prefix, StringComparison.Ordinal) ||
            !string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)),
                StringComparison.OrdinalIgnoreCase)) return;
        try { Directory.Delete(path, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
