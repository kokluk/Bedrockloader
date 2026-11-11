namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    int catalogGeneration;
    bool installingCatalogMod;

    void BuildModCatalogPage()
    {
        int generation = ++catalogGeneration;
        var page = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Tag = "page", Margin = Padding.Empty };
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, Tag = "page", Margin = Padding.Empty, WrapContents = false };
        toolbar.Controls.Add(MakeButton(T("Back to mods", "Modlara dön"), () => ShowPage("Mods"), 130));
        var refresh = MakeButton(T("Refresh", "Yenile"), () => { }, 100);
        toolbar.Controls.Add(refresh);
        toolbar.Controls.Add(MakeButton(T("Repository", "GitHub deposu"), () => OpenUrl(ModCatalog.Repository), 170, "external"));
        var feedback = WrappedText(T("Loading mods…", "Modlar yükleniyor…"), 10); feedback.Dock = DockStyle.Fill;
        var cards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Tag = "page", Margin = Padding.Empty };
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        cards.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var navigation = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Tag = "page", Margin = Padding.Empty, Padding = new Padding(0, 8, 0, 0) };
        navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110)); navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        navigation.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        IReadOnlyList<CatalogMod> entries = Array.Empty<CatalogMod>();
        int offset = 0;
        var installButtons = new List<AnimatedRoundButton>();
        void UpdateInstallButtons()
        {
            foreach (var button in installButtons)
                if (!button.IsDisposed) button.Enabled = !installingCatalogMod;
        }
        bool fetching = false;
        bool Current() => !IsDisposed && !page.IsDisposed && generation == catalogGeneration && currentPage == "Catalog";
        var previous = MakeButton(T("Previous", "Önceki"), () => { }, 110);
        var next = MakeButton(T("Next", "Sonraki"), () => { }, 110);
        previous.Dock = next.Dock = DockStyle.Fill; previous.Margin = next.Margin = Padding.Empty;
        var position = WrappedText("", 10); position.Dock = DockStyle.Fill; position.TextAlign = ContentAlignment.MiddleCenter;
        previous.Click += (_, _) => { offset = Math.Max(0, offset - 2); Render(); };
        next.Click += (_, _) => { offset += 2; Render(); };
        navigation.Controls.Add(previous, 0, 0); navigation.Controls.Add(position, 1, 0); navigation.Controls.Add(next, 2, 0);
        void Render()
        {
            if (!Current()) return;
            bool multiplePages = entries.Count > 2;
            page.RowStyles[3].Height = multiplePages ? 42 : 0;
            navigation.Visible = multiplePages;
            cards.SuspendLayout();
            installButtons.Clear();
            foreach (Control control in cards.Controls.Cast<Control>().ToArray()) control.Dispose();
            cards.Controls.Clear();
            int column = 0;
            foreach (var mod in entries.Skip(offset).Take(2))
            {
                var card = new RoundedPanel { Dock = DockStyle.Fill, CornerRadius = 22, Padding = new Padding(14), Margin = column == 0 ? new Padding(0, 0, 6, 0) : new Padding(6, 0, 0, 0) };
                var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Tag = "card", Margin = Padding.Empty };
                content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                content.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); content.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
                content.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); content.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); content.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
                var name = WrappedText(mod.Name, 13, true); name.Dock = DockStyle.Fill; name.AutoEllipsis = true;
                var developer = WrappedText(string.IsNullOrWhiteSpace(mod.Developer) ? "" : T($"Developers: {mod.Developer}", $"Geliştiriciler: {mod.Developer}"), 10);
                developer.Dock = DockStyle.Fill; developer.AutoEllipsis = true;
                var description = WrappedText(T($"Description: {mod.Description}\nMinimum version: {mod.MinimumVersion}", $"Açıklama: {mod.Description}\nMinimum sürüm: {mod.MinimumVersion}"), 10); description.Dock = DockStyle.Fill; description.AutoEllipsis = true;
                var size = WrappedText($"{mod.Size / 1024d:N1} KB • DLL", 9); size.Dock = DockStyle.Fill;
                void FitCard()
                {
                    bool compact = card.ClientSize.Height < 210;
                    content.RowStyles[3].Height = compact ? 0 : 24;
                    size.Visible = !compact;
                }
                card.Resize += (_, _) => FitCard();
                int actionCount = 1 + (mod.Discord == null ? 0 : 1) + (mod.Source == null ? 0 : 1);
                var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = actionCount, RowCount = 1, Tag = "card", Margin = Padding.Empty, Padding = new Padding(0, 4, 0, 0) };
                for (int i = 0; i < actions.ColumnCount; i++) actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / actions.ColumnCount));
                actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                var install = MakeButton(T("Download & install", "İndir ve kur"), () => InstallCatalogMod(mod, UpdateInstallButtons), icon: "download");
                install.Tag = "primary"; install.Dock = DockStyle.Fill; install.Margin = new Padding(0, 0, actionCount == 1 ? 0 : 3, 0); install.Enabled = !installingCatalogMod;
                install.Font = LauncherFont.Create(9);
                actions.Controls.Add(install, 0, 0);
                installButtons.Add(install);
                int actionColumn = 1;
                if (mod.Source is { } github)
                {
                    var source = MakeButton("GitHub", () => OpenUrl(github.AbsoluteUri), icon: "github");
                    source.Tag = "card-button";
                    source.Dock = DockStyle.Fill; source.Margin = new Padding(3, 0, actionColumn == actionCount - 1 ? 0 : 3, 0); source.Font = LauncherFont.Create(9);
                    actions.Controls.Add(source, actionColumn++, 0);
                }
                if (mod.Discord is { } discord)
                {
                    var source = MakeButton("Discord", () => OpenUrl(discord.AbsoluteUri), icon: "discord");
                    source.Tag = "card-button";
                    source.Dock = DockStyle.Fill; source.Margin = new Padding(3, 0, 0, 0); source.Font = LauncherFont.Create(9);
                    actions.Controls.Add(source, actionColumn, 0);
                }
                bool? stackedActions = null;
                void FitActions()
                {
                    bool stacked = actionCount > 1 && card.ClientSize.Width < 330;
                    if (stackedActions == stacked) return;
                    stackedActions = stacked;
                    actions.SuspendLayout();
                    actions.ColumnCount = stacked ? actionCount - 1 : actionCount;
                    actions.RowCount = stacked ? 2 : 1;
                    actions.ColumnStyles.Clear(); actions.RowStyles.Clear();
                    for (int i = 0; i < actions.ColumnCount; i++)
                        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,
                            !stacked && actionCount == 3 ? (i == 0 ? 42 : 29) : 100f / actions.ColumnCount));
                    for (int i = 0; i < actions.RowCount; i++) actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / actions.RowCount));
                    actions.SetCellPosition(install, new TableLayoutPanelCellPosition(0, 0));
                    actions.SetColumnSpan(install, stacked ? actions.ColumnCount : 1);
                    install.Margin = stacked ? new Padding(0, 0, 0, 4) : new Padding(0, 0, actionCount == 1 ? 0 : 3, 0);
                    int index = 0;
                    foreach (Control button in actions.Controls.Cast<Control>().Where(c => c != install))
                    {
                        actions.SetCellPosition(button, new TableLayoutPanelCellPosition(stacked ? index : index + 1, stacked ? 1 : 0));
                        button.Margin = new Padding(stacked && index == 0 ? 0 : 3, 0, index == actionCount - 2 ? 0 : 3, 0);
                        index++;
                    }
                    content.RowStyles[4].Height = stacked ? 84 : 44;
                    actions.ResumeLayout(true);
                }
                card.Resize += (_, _) => FitActions();
                content.Controls.Add(name, 0, 0); content.Controls.Add(developer, 0, 1); content.Controls.Add(description, 0, 2); content.Controls.Add(size, 0, 3); content.Controls.Add(actions, 0, 4);
                FitActions();
                card.Controls.Add(content); cards.Controls.Add(card, column++, 0); FitCard();
            }
            previous.Enabled = !fetching && offset > 0; next.Enabled = !fetching && offset + 2 < entries.Count;
            position.Text = entries.Count == 0 ? "" : $"{offset / 2 + 1} / {(entries.Count + 1) / 2}";
            cards.ResumeLayout(true); ApplyTheme();
        }
        async Task LoadCatalog()
        {
            if (fetching) return;
            fetching = true; refresh.Enabled = false;
            feedback.Text = T("Loading mods…", "Modlar yükleniyor…");
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(updatesLifetime.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(30));
                var loaded = await ModCatalog.FetchAsync(Http, timeout.Token);
                if (!Current()) return;
                entries = loaded; offset = 0;
                feedback.Text = entries.Count == 0 ? T("No mods have been published yet.", "Henüz mod yayımlanmamış.") : T($"{entries.Count} mods • Downloads require your approval.", $"{entries.Count} mod • İndirme senin onayınla başlar.");
            }
            catch (OperationCanceledException) { if (Current()) feedback.Text = T("Loading timed out. Try Refresh.", "Yükleme zaman aşımına uğradı. Yenile ile tekrar dene."); }
            catch (Exception ex) { if (Current()) { feedback.Text = T("The repository could not be loaded. Try Refresh.", "Depo yüklenemedi. Yenile ile tekrar dene."); LogAction($"Mod catalog loading failed: {ex.Message}", $"Mod kataloğu yüklenemedi: {ex.Message}"); } }
            finally { fetching = false; if (Current()) { refresh.Enabled = true; Render(); } }
        }
        refresh.Click += async (_, _) => await LoadCatalog();
        page.Controls.Add(toolbar, 0, 0); page.Controls.Add(feedback, 0, 1); page.Controls.Add(cards, 0, 2); page.Controls.Add(navigation, 0, 3);
        contentHost.Controls.Add(page);
        AddPageHeading(T("Mod Center", "Mod Merkezi"), T("Browse mods from the BedrockLoader repository.", "BedrockLoader deposundaki modları keşfet."));
        Render();
        // Start after ShowPage has completed layout and applied the theme.
        BeginInvoke(new Action(async () => { if (Current()) await LoadCatalog(); }));
    }

    async void InstallCatalogMod(CatalogMod mod, Action updateInstallButtons)
    {
        if (installingCatalogMod) return;
        installingCatalogMod = true;
        string? staged = null;
        bool downloadStarted = false;
        try
        {
            bool versionApproved = false;
            if (mod.Variants.Count > 0)
            {
                var selected = SelectCatalogVersion(mod);
                if (selected == null) return;
                mod = selected;
                versionApproved = true;
            }
            string target = Path.Combine(modsFolder, mod.FileName);
            if (!versionApproved && ShowThemedPopup(T("Install mod", "Mod kurulumu"), T($"Download {mod.Name} and install it in your local mods?\n\nSource:\n{mod.Download}\n\nDestination:\n{target}\n\n{mod.Credit}", $"{mod.Name} indirilip yerel modlarına kurulsun mu?\n\nKaynak:\n{mod.Download}\n\nHedef:\n{target}\n\n{mod.Credit}"), true) != DialogResult.Yes) return;
            bool replace = File.Exists(target);
            if (replace && ShowThemedPopup(T("Replace existing mod?", "Var olan mod değiştirilsin mi?"), T($"{mod.FileName} already exists. Replace it?", $"{mod.FileName} zaten var. Yerine indirilen dosya yazılsın mı?"), true) != DialogResult.Yes) return;
            downloadStarted = true;
            updateInstallButtons();
            string cache = Path.Combine(appDataRoot, "ModDownloads"); Directory.CreateDirectory(cache);
            staged = Path.Combine(cache, Guid.NewGuid().ToString("N") + ".download");
            UpdateStatus(T($"Downloading {mod.Name}…", $"{mod.Name} indiriliyor…"));
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(updatesLifetime.Token); timeout.CancelAfter(TimeSpan.FromMinutes(5));
            await ModCatalog.DownloadAsync(Http, mod, staged, timeout.Token);
            timeout.Token.ThrowIfCancellationRequested();
            Directory.CreateDirectory(modsFolder); File.Copy(staged, target, overwrite: replace);
            if (currentPage == "Mods") RefreshMods();
            LogAction($"Added verified catalog mod: {mod.FileName}", $"Doğrulanmış katalog modu eklendi: {mod.FileName}");
            UpdateStatus(T($"{mod.Name} added to your mods.", $"{mod.Name} modlarına eklendi."));
            if (!IsDisposed) ShowThemedPopup(T("Mod added", "Mod eklendi"), T($"{mod.FileName} was added to your local mods.", $"{mod.FileName} yerel modlarına eklendi."));
        }
        catch (OperationCanceledException) { if (!IsDisposed) UpdateStatus(T("The mod download was canceled or timed out.", "Mod indirmesi iptal edildi veya zaman aşımına uğradı.")); }
        catch (Exception ex) { if (!IsDisposed) { UpdateStatus(T("The mod could not be added.", "Mod eklenemedi.")); ShowThemedPopup(T("Mod download failed", "Mod indirilemedi"), ex.Message); } }
        finally
        {
            if (staged != null) { try { File.Delete(staged); } catch { } }
            installingCatalogMod = false;
            if (downloadStarted && !IsDisposed) updateInstallButtons();
        }
    }
}
