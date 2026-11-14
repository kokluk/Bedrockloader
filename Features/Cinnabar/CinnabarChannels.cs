using System.Text.Json;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    string CinnabarNightlyUserRoot => Path.Combine(appDataRoot, "CinnabarNightlyUser");

    async Task ConfirmCinnabarNightlyAsync(string executable, CinnabarNightlyCatalog.Build build, CancellationToken token)
    {
        if (!await Task.Run(() => CinnabarNightlySupportsIsolation(executable), token))
            throw new InvalidDataException("Nightly user-root support could not be confirmed.");
        var start = new System.Diagnostics.ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
            WorkingDirectory = Path.GetDirectoryName(executable)!
        };
        start.Environment["CINNABAR_USER_ROOT"] = CinnabarNightlyUserRoot;
        start.ArgumentList.Add("--update-build-info");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var process = System.Diagnostics.Process.Start(start) ?? throw new IOException("Nightly version probe failed.");
        try
        {
            string? line = await process.StandardOutput.ReadLineAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            if (process.ExitCode != 0 || line is not { Length: > 0 and <= 4096 })
                throw new InvalidDataException("Nightly build identity unavailable.");
            using var data = JsonDocument.Parse(line);
            if (data.RootElement.GetProperty("channel").GetString() != "nightly" ||
                data.RootElement.GetProperty("number").GetInt64() != build.Number)
                throw new InvalidDataException("Nightly build identity mismatch.");
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw;
        }
    }

    string CinnabarChannelDescription()
    {
        string? executable = FindCinnabar();
        if (executable == null) return T("Selected channel is not installed.", "Seçili kanal kurulu değil.");
        return settings.CinnabarNightly
            ? $"Nightly · {settings.CinnabarNightlyVersion} #{settings.CinnabarNightlyBuild} · {settings.CinnabarNightlyDate:yyyy-MM-dd}"
            : $"Stable · {CinnabarInstalledVersion(executable)?.ToString(3) ?? "—"}";
    }

    static bool CinnabarNightlySupportsIsolation(string executable) => File.Exists(executable) &&
        System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(executable)).Contains("CINNABAR_USER_ROOT", StringComparison.Ordinal);

    static UpdateRelease ReadStablePackage(JsonElement root)
    {
        string tag = root.GetProperty("tag_name").GetString() ?? "";
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean() ||
            !TryCinnabarVersion(tag, out var version)) throw new InvalidDataException("Invalid stable release.");
        const string name = "Cinnabar-x64.msi";
        var asset = root.GetProperty("assets").EnumerateArray().FirstOrDefault(item => item.GetProperty("name").GetString() == name);
        if (asset.ValueKind == JsonValueKind.Undefined || asset.GetProperty("state").GetString() != "uploaded")
            throw new InvalidDataException("Cinnabar x64 MSI unavailable.");
        string? digest = asset.GetProperty("digest").GetString();
        long size = asset.GetProperty("size").GetInt64();
        string url = CinnabarReleases + "/download/" + Uri.EscapeDataString(tag) + "/" + name;
        if (digest is not { Length: 71 } || !digest.StartsWith("sha256:", StringComparison.Ordinal) ||
            !digest[7..].All(Uri.IsHexDigit) || size is <= 0 or > ReleaseUpdates.MaxDownload ||
            asset.GetProperty("browser_download_url").GetString() != url) throw new InvalidDataException("Invalid Cinnabar MSI metadata.");
        return new(version!, tag, "", new Uri(CinnabarReleases), new Uri(url), digest[7..], size);
    }
    SoftSelector BuildCinnabarChannelSelector()
    {
        var selector = MakeThemeCombo(172);
        selector.AccessibleName = T("Cinnabar channel", "Cinnabar sürüm kanalı");
        selector.Items.AddRange([T("Stable", "Kararlı"), T("Nightly", "Gecelik")]);
        selector.ItemIcons = ["check-mark", "moon"];
        selector.SelectedIndex = settings.CinnabarNightly ? 1 : 0;
        selector.LeadingIcon = selector.ItemIcons[selector.SelectedIndex];
        bool reverting = false;
        selector.SelectedIndexChanged += (_, _) =>
        {
            if (reverting) return;
            selector.LeadingIcon = selector.ItemIcons[selector.SelectedIndex];
            bool nightly = selector.SelectedIndex == 1;
            // The menu can still be inside WinForms' Close call when selection changes.
            BeginInvoke(() =>
            {
                if (IsDisposed || selector.IsDisposed || currentPage != "Home" || !cinnabarMode ||
                    nightly != (selector.SelectedIndex == 1) || nightly == settings.CinnabarNightly) return;
                if (cinnabarBusy || cinnabarChecking || backupBusy)
                {
                    reverting = true;
                    try { selector.SelectedIndex = settings.CinnabarNightly ? 1 : 0; }
                    finally { reverting = false; selector.LeadingIcon = selector.ItemIcons[selector.SelectedIndex]; }
                    return;
                }
                settings.CinnabarNightly = nightly;
                bool showNotice = nightly && !settings.CinnabarNightlyNoticeShown;
                if (showNotice) settings.CinnabarNightlyNoticeShown = true;
                SaveSettings();
                cinnabarLatest = null;
                nightlyLatest = null;
                cinnabarCheckedAt = default;
                if (showNotice) ShowThemedPopup("Cinnabar Nightly",
                    T("Nightly builds are experimental and use separate game data.", "Gecelik derlemeler deneyseldir ve ayrı oyun verisi kullanır."));
                ShowPage("Home", force: true);
            });
        };
        return selector;
    }


}
