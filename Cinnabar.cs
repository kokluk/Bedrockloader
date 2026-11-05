using System.Diagnostics;
using System.Text.Json;
using Microsoft.Win32;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    const string CinnabarReleases = "https://github.com/cinnabar-mc/cinnabar/releases";
    bool cinnabarMode, cinnabarBusy;
    ProgressBar? cinnabarProgress;
    RowStyle? cinnabarProgressRow;
    int cinnabarPercent;

    void ShowCinnabarProgress(int percent)
    {
        cinnabarPercent = Math.Max(0, percent);
        if (cinnabarProgress is not { IsDisposed: false } bar || cinnabarProgressRow == null) return;
        bar.Visible = cinnabarMode && percent >= 0;
        bar.Value = cinnabarPercent;
        cinnabarProgressRow.Height = bar.Visible ? 20 : 0;
    }

    string CinnabarLaunchText() => FindCinnabar() != null
        ? T("Launch Cinnabar", "Cinnabar’ı başlat") : T("Install Cinnabar", "Cinnabar’ı yükle");

    void CompareGames() => ShowThemedPopup(T("Minecraft and Cinnabar", "Minecraft ve Cinnabar"),
        T("Minecraft Bedrock is the official Mojang game and supports Marketplace, Realms and native Bedrock packs.\n\nCinnabar is an independent Rust client in beta. It has separate local data and supports compatible Java shader packs. Feature and server compatibility can differ.",
          "Minecraft Bedrock, Marketplace, Realms ve Bedrock paketlerini destekleyen resmî Mojang oyunudur.\n\nCinnabar, Rust ile geliştirilen bağımsız bir beta istemcisidir. Yerel verileri ayrı saklar ve uyumlu Java shader paketlerini destekler. Özellikler ve sunucu uyumluluğu değişebilir."));

    void ExplainCinnabar() => ShowThemedPopup(T("What is Cinnabar?", "Cinnabar nedir?"),
        T("Cinnabar is an independent, unofficial client compatible with Minecraft Bedrock. It has its own launcher and shader settings. It uses Rust and targets better performance. It is in beta and supports compatible Java shader packs through its Enhanced renderer.",
          "Cinnabar, Minecraft Bedrock ile uyumlu, bağımsız ve resmî olmayan bir istemcidir. Kendi başlatıcısı ve shader ayarları vardır. Rust ile geliştiriliyor ve performansı artırmayı hedefliyor. Beta aşamasında. Enhanced görüntüleyici, uyumlu Java shader paketlerini destekliyor."));

    string? FindCinnabar()
    {
        if (File.Exists(settings.CinnabarExecutable)) return settings.CinnabarExecutable;
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
        return roots.SelectMany(root => new[] { Path.Combine(root, "Cinnabar.exe"), Path.Combine(root, "CinnabarLauncher.exe") })
            .FirstOrDefault(File.Exists);
    }

    async Task CheckCinnabarUpdateAsync()
    {
        if (cinnabarBusy) return;
        cinnabarBusy = true;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(updatesLifetime.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            using var json = await ReadCinnabarReleaseAsync(timeout.Token);
            string tag = json.RootElement.GetProperty("tag_name").GetString() ?? "";
            if (!TryCinnabarVersion(tag, out var latest)) throw new InvalidDataException("Invalid Cinnabar release version.");
            if (IsDisposed || !cinnabarMode || currentPage != "Home") return;

            string? executable = FindCinnabar();
            string installedTag = executable != null && string.Equals(executable, settings.CinnabarExecutable, StringComparison.OrdinalIgnoreCase)
                ? settings.CinnabarInstalledTag : "";
            if (executable != null && string.IsNullOrEmpty(installedTag))
                installedTag = FileVersionInfo.GetVersionInfo(executable).ProductVersion ?? "";
            string message;
            if (executable == null)
                message = T($"Cinnabar is not installed. Latest release: {tag}.", $"Cinnabar kurulu değil. Son sürüm: {tag}.");
            else if (!TryCinnabarVersion(installedTag, out var installed))
                message = T($"Latest release: {tag}. The installed version could not be verified.", $"Son sürüm: {tag}. Kurulu sürüm doğrulanamadı.");
            else if (installed < latest)
                message = T($"Update available: {installedTag} → {tag}.", $"Güncelleme var: {installedTag} → {tag}.");
            else
            {
                ShowThemedPopup("Cinnabar", T($"Cinnabar is up to date ({installedTag}).", $"Cinnabar güncel ({installedTag})."));
                return;
            }
            if (ShowThemedPopup("Cinnabar", message, true, T("Open releases", "Sürümleri aç")) == DialogResult.Yes)
                OpenUrl(CinnabarReleases);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (!IsDisposed && !updatesLifetime.IsCancellationRequested && cinnabarMode && currentPage == "Home")
                ShowThemedPopup("Cinnabar", T("Could not check for updates. Try again later.", "Güncellemeler denetlenemedi. Daha sonra tekrar dene."));
        }
        finally { cinnabarBusy = false; }
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

    async Task LaunchCinnabarAsync()
    {
        if (cinnabarBusy) return;
        cinnabarBusy = true;
        string? installer = null;
        try
        {
            if (FindCinnabar() is { } executable)
            {
                using var game = Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(executable)! });
                return;
            }
            if (ShowThemedPopup("Cinnabar", T("Download and open the official Windows installer?",
                "Resmî Windows kurulum dosyası indirilip açılsın mı?"), true, T("Download", "İndir")) != DialogResult.Yes) return;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(updatesLifetime.Token);
            timeout.CancelAfter(TimeSpan.FromMinutes(5));
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
            IProgress<long> progress = new Progress<long>(bytes => { if (cinnabarBusy && !IsDisposed) ShowCinnabarProgress((int)(bytes * 100 / size)); });
            await ReleaseUpdates.DownloadVerifiedAsync(Http, release, installer, progress.Report, timeout.Token, null);
            using (var stream = File.OpenRead(installer))
            {
                var header = new byte[8];
                stream.ReadExactly(header);
                if (!header.AsSpan().SequenceEqual(new byte[] { 0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1 }))
                    throw new InvalidDataException("Invalid MSI header.");
            }
            string target = Path.Combine(appDataRoot, "CinnabarRuntime", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(target);
            var command = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "msiexec.exe")) { UseShellExecute = false, CreateNoWindow = true };
            foreach (string arg in new[] { "/a", installer, "/qn", "/norestart", "TARGETDIR=" + target }) command.ArgumentList.Add(arg);
            using var setup = Process.Start(command) ?? throw new IOException("Cinnabar setup could not start.");
            await setup.WaitForExitAsync();
            if (setup.ExitCode != 0) throw new IOException("Cinnabar extraction failed (" + setup.ExitCode + ").");
            settings.CinnabarExecutable = Directory.EnumerateFiles(target, "cinnabar.exe", SearchOption.AllDirectories).SingleOrDefault()
                ?? throw new InvalidDataException("Cinnabar executable missing from the installer.");
            settings.CinnabarInstalledTag = tag;
            SaveSettings();
            UpdateStatus(T("Cinnabar is ready to launch.", "Cinnabar başlatmaya hazır."));
            if (!IsDisposed && currentPage == "Home") ShowPage("Home", true);
        }
        catch (Exception ex)
        {
            if (!IsDisposed) ShowError(ex, T("Cinnabar could not be opened", "Cinnabar açılamadı"));
        }
        finally
        {
            if (installer != null) try { File.Delete(installer); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            cinnabarBusy = false;
            ShowCinnabarProgress(-1);
        }
    }
}
