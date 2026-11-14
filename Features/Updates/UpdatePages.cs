using System.Diagnostics;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    readonly CancellationTokenSource updatesLifetime = new();
    UpdateRelease? availableRelease;
    bool updatesBusy;
    string updateMessageEn = "Check for a new version when you are ready.";
    string updateMessageTr = "Yeni sürüm olup olmadığını kontrol edebilirsin.";
    Label? updateMessage;
    TextBox? releaseNotes;
    Button? checkUpdateButton, installUpdateButton;

    void BuildUpdatesPage()
    {
        var page = new Panel { Dock = DockStyle.Fill, AutoScroll = false, Tag = "page" };
        var card = new RoundedPanel { Dock = DockStyle.Fill, Height = 354, Padding = new Padding(20), CornerRadius = 24 };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Tag = "card", Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
        var version = WrappedText(T($"Installed version: {DisplayVersion}", $"Kurulu sürüm: {DisplayVersion}"), 12, true); version.Dock = DockStyle.Fill;
        updateMessage = WrappedText(T(updateMessageEn, updateMessageTr)); updateMessage.Dock = DockStyle.Fill;
        releaseNotes = new ScrollTextBox { Dock = DockStyle.Fill, Font = LauncherFont.Create(10) };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, Tag = "card", Padding = new Padding(0, 10, 0, 0), Margin = Padding.Empty };
        checkUpdateButton = MakeButton(T("Check for updates", "Güncellemeleri denetle"), CheckUpdates, 185, "update");
        checkUpdateButton.Tag = "card-button";
        installUpdateButton = MakeButton(T("Download and install", "İndir ve kur"), InstallUpdate, 185); installUpdateButton.Tag = "primary";
        actions.Controls.Add(checkUpdateButton); actions.Controls.Add(installUpdateButton);
        var releases = MakeButton(T("View releases", "Sürümleri aç"), () => OpenUrl(ReleaseUpdates.ReleasesPage), 185, "external");
        releases.Tag = "card-button"; actions.Controls.Add(releases);
        layout.Controls.Add(version, 0, 0); layout.Controls.Add(updateMessage, 0, 1); layout.Controls.Add(releaseNotes, 0, 2); layout.Controls.Add(actions, 0, 3);
        card.Controls.Add(layout); page.Controls.Add(card); contentHost.Controls.Add(page);
        AddPageHeading(T("Updates", "Güncellemeler"), T("Check for the latest BedrockLoader version.", "BedrockLoader'ın yeni sürümlerini kontrol et."));
        RenderUpdateState();
    }

    void RenderUpdateState()
    {
        if (IsDisposed) return;
        bool hasNewVersion = ReleaseUpdates.ShouldNotify(availableRelease);
        if (updateLauncher is { IsDisposed: false })
        {
            updateLauncher.ShowUpdateBadge = hasNewVersion;
            updateLauncher.AccessibleDescription = hasNewVersion ? T("1 new update available", "1 yeni güncelleme var") : T("Updates", "Güncellemeler");
        }
        if (currentPage != "Updates") return;
        if (updateMessage != null) updateMessage.Text = T(updateMessageEn, updateMessageTr);
        if (checkUpdateButton != null) checkUpdateButton.Enabled = !updatesBusy;
        if (installUpdateButton != null)
        {
            bool canInstall = hasNewVersion && availableRelease is { CanInstall: true };
            installUpdateButton.Visible = canInstall;
            installUpdateButton.Enabled = !updatesBusy && canInstall;
        }
        if (releaseNotes != null)
        {
            string notes = ReleaseUpdates.LocalizeNotes(availableRelease?.Notes ?? "", english);
            string displayNotes = string.IsNullOrWhiteSpace(notes)
                ? T("Release notes will appear here.", "Sürüm notları burada görünecek.")
                : notes.ReplaceLineEndings(Environment.NewLine);
            if (releaseNotes.Text != displayNotes)
            {
                releaseNotes.Text = displayNotes;
                releaseNotes.Select(0, 0);
                releaseNotes.ScrollToCaret();
            }
        }
    }

    void UpdateFeedback(string en, string tr) { updateMessageEn = en; updateMessageTr = tr; RenderUpdateState(); }

    async void CheckUpdates() => await CheckUpdatesAsync();

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ShowInstalledReleaseNotes();
        await CheckUpdatesAsync();
        if (IsDisposed || !Visible || availableRelease is not { } release || !ReleaseUpdates.ShouldNotify(release)) return;
        string notes = ReleaseUpdates.LocalizeNotes(release.Notes, english);
        string message = T($"{release.Tag} is available.", $"{release.Tag} güncellemesi var.");
        if (!string.IsNullOrWhiteSpace(notes)) message += "\n\n" + notes;
        if (ShowThemedPopup(T("Update available", "Güncelleme var"), message, true,
            release.CanInstall ? T("Download and install", "İndir ve kur") : T("View release", "Sürümü aç"),
            cancelLabel: T("Later", "Daha sonra")) != DialogResult.Yes) return;
        if (release.CanInstall) InstallUpdateCore(approved: true);
        else OpenUrl(release.Page.AbsoluteUri);
    }

    async Task CheckUpdatesAsync()
    {
        if (updatesBusy) return;
        updatesBusy = true;
        UpdateFeedback("Checking for updates…", "Güncellemeler kontrol ediliyor…");
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(updatesLifetime.Token); timeout.CancelAfter(TimeSpan.FromSeconds(30));
            availableRelease = await ReleaseUpdates.CheckAsync(Http, timeout.Token);
            if (availableRelease == null) UpdateFeedback("No public release is available yet. The update channel may not be ready or accessible.", "Henüz erişilebilir bir sürüm yok. Güncelleme kanalı hazır olmayabilir veya erişime kapalı olabilir.");
            else if (availableRelease.Version <= ReleaseUpdates.CurrentVersion) UpdateFeedback("You are using the latest version.", "En güncel sürümü kullanıyorsun.");
            else if (!availableRelease.CanInstall) UpdateFeedback($"Version {availableRelease.Tag} is available, but a verified x64 installer has not been published. Open releases for details.", $"{availableRelease.Tag} sürümü var; doğrulanabilir x64 kurulum dosyası henüz yayınlanmamış. Sürümler sayfasını açabilirsin.");
            else UpdateFeedback($"Version {availableRelease.Tag} is available ({availableRelease.Size / 1048576d:F1} MB). Installation requires your approval.", $"{availableRelease.Tag} sürümü hazır ({availableRelease.Size / 1048576d:F1} MB). Kurulum senin onayınla başlayacak.");
        }
        catch (OperationCanceledException) { if (!IsDisposed) UpdateFeedback("The check was canceled or timed out. You can try again.", "Kontrol iptal edildi veya zaman aşımına uğradı. Tekrar deneyebilirsin."); }
        catch (Exception ex) { LogAction($"Update check failed: {ex.Message}", $"Güncelleme denetimi başarısız: {ex.Message}"); UpdateFeedback("Updates could not be checked. Check your connection and try again.", "Güncellemeler kontrol edilemedi. Bağlantını kontrol edip tekrar dene."); }
        finally { updatesBusy = false; RenderUpdateState(); }
    }

    void InstallUpdate() => InstallUpdateCore(approved: false);

    async void InstallUpdateCore(bool approved)
    {
        if (updatesBusy || availableRelease is not { CanInstall: true } release || release.Version <= ReleaseUpdates.CurrentVersion) return;
        if (!approved && ShowThemedPopup(T("Update approval", "Güncelleme onayı"),
            T($"Download and install {release.Tag}?\n\nSource: {release.Page}\n\nAfter verification, the setup wizard will open and BedrockLoader will close. Your mods and settings will be kept.",
            $"{release.Tag} sürümü indirilip kurulsun mu?\n\nKaynak: {release.Page}\n\nDoğrulamadan sonra kurulum sihirbazı açılacak ve BedrockLoader kapanacak. Modların ve ayarların korunacak."),
            true, T("Download and install", "İndir ve kur")) != DialogResult.Yes) return;
        updatesBusy = true;
        string? staged = null;
        try
        {
            UpdateFeedback("Downloading the update…", "Güncelleme indiriliyor…");
            string folder = Path.Combine(appDataRoot, "Updates", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            staged = Path.Combine(folder, "setup.download");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(updatesLifetime.Token); timeout.CancelAfter(TimeSpan.FromMinutes(10));
            var clock = Stopwatch.StartNew();
            await ReleaseUpdates.DownloadAsync(Http, release, staged, bytes =>
            {
                if (clock.ElapsedMilliseconds < 150) return;
                UpdateFeedback($"Downloading… {bytes * 100 / release.Size}%", $"İndiriliyor… %{bytes * 100 / release.Size}"); clock.Restart();
            }, timeout.Token);
            timeout.Token.ThrowIfCancellationRequested();
            string setup = Path.Combine(folder, "BedrockLoader-Setup.exe"); File.Move(staged, setup); staged = null;
            var start = new ProcessStartInfo(setup) { UseShellExecute = true };
            start.ArgumentList.Add("/SP-"); start.ArgumentList.Add(english ? "/LANG=english" : "/LANG=turkish");
            if (File.Exists(Path.Combine(AppContext.BaseDirectory, "unins000.exe"))) start.ArgumentList.Add("/DIR=" + AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
            if (Process.Start(start) == null) throw new IOException("Setup did not start / Kurulum başlatılamadı.");
            LogAction($"Started verified update installer: {release.Tag}", $"Doğrulanmış güncelleme yükleyicisi başlatıldı: {release.Tag}"); CloseToDesktop();
        }
        catch (OperationCanceledException) { if (!IsDisposed) UpdateFeedback("The download was canceled or timed out.", "İndirme iptal edildi veya zaman aşımına uğradı."); }
        catch (Exception ex) { LogAction($"Update installation failed: {ex.Message}", $"Güncelleme kurulamadı: {ex.Message}"); if (!IsDisposed) { UpdateFeedback("The update could not be installed. You can try again.", "Güncelleme kurulamadı. Tekrar deneyebilirsin."); ShowThemedPopup(T("Update failed", "Güncelleme başarısız"), ex.Message); } }
        finally
        {
            if (staged != null) { try { File.Delete(staged); } catch { } }
            updatesBusy = false; RenderUpdateState();
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        presenceLifetime.Cancel();
        DisposeTray();
        updatesLifetime.Cancel(); updatesLifetime.Dispose(); base.OnFormClosed(e);
    }
}
