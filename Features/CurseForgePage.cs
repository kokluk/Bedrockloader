using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    static readonly HttpClient CurseHttp = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(60) };
    int curseGeneration;
    TableLayoutPanel? curseRoot;
    bool installingCursePack;
    string CurseKeyPath => Path.Combine(appDataRoot, "curseforge-key.dat");

    string ReadCurseKey()
    {
        try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(CurseKeyPath), null, DataProtectionScope.CurrentUser)); }
        catch { }
        using var embedded = typeof(MainForm).Assembly.GetManifestResourceStream("BedrockLoader.CurseForgeKey");
        return embedded is null ? "" : new StreamReader(embedded).ReadToEnd().Trim();
    }

    void SaveCurseKey(string key)
    {
        Directory.CreateDirectory(appDataRoot);
        File.WriteAllBytes(CurseKeyPath, ProtectedData.Protect(Encoding.UTF8.GetBytes(key.Trim()), null, DataProtectionScope.CurrentUser));
    }

    bool ConfigureCurseKey()
    {
        using var popup = new RoundedPopupForm(Accent)
        {
            Text = T("CurseForge connection", "CurseForge bağlantısı"), FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.CenterParent, ShowInTaskbar = false, BackColor = Surface,
            ClientSize = new Size(Math.Min(520, ClientSize.Width - 48), 260), Font = Font
        };
        var card = new RoundedPanel { Dock = DockStyle.Fill, BackColor = Surface, CornerRadius = 24, Padding = new Padding(22) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Surface };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        var title = WrappedText(T("CurseForge connection", "CurseForge bağlantısı"), 16, true); title.Dock = DockStyle.Fill;
        var header = new Panel { Dock = DockStyle.Fill, BackColor = Surface, Margin = Padding.Empty };
        var close = new SymbolButton { Symbol = "close", Dock = DockStyle.Right, Width = 36, BackColor = SurfaceAlt, ForeColor = TextColor, CornerRadius = 18, AccessibleName = T("Close", "Kapat") };
        close.Click += (_, _) => popup.DialogResult = DialogResult.Cancel;
        header.Controls.Add(title); header.Controls.Add(close);
        var help = WrappedText(T("Enter your CurseForge API key. It is stored encrypted for your Windows account.", "CurseForge API anahtarını gir. Anahtar, Windows hesabın için şifrelenerek saklanır.")); help.Dock = DockStyle.Fill;
        var key = MakeTextBox(); key.UseSystemPasswordChar = true; key.Dock = DockStyle.Top; key.AccessibleName = "CurseForge API key";
        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        var cancel = MakeButton(T("Cancel", "Vazgeç"), () => popup.DialogResult = DialogResult.Cancel); cancel.Dock = DockStyle.Fill;
        var save = MakeButton(T("Connect", "Bağlan"), () => { }); save.Tag = "primary"; save.Dock = DockStyle.Fill;
        ApplyButtonPalette(cancel); ApplyButtonPalette(save);
        save.Click += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(key.Text)) return;
            save.Enabled = false;
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
                await CurseForgeApi.ProbeAsync(CurseHttp, key.Text.Trim(), timeout.Token);
                if (popup.IsDisposed) return;
                SaveCurseKey(key.Text); popup.DialogResult = DialogResult.OK;
            }
            catch { if (!popup.IsDisposed) help.Text = T("Connection failed. Check the API key and try again.", "Bağlantı kurulamadı. API anahtarını kontrol edip tekrar dene."); }
            finally { if (!save.IsDisposed) save.Enabled = true; }
        };
        actions.Controls.Add(cancel, 0, 0); actions.Controls.Add(save, 1, 0);
        layout.Controls.Add(header, 0, 0); layout.Controls.Add(help, 0, 1); layout.Controls.Add(key, 0, 2); layout.Controls.Add(actions, 0, 3);
        card.Controls.Add(layout); popup.Controls.Add(card); popup.CancelButton = cancel; popup.AcceptButton = save;
        return popup.ShowDialog(this) == DialogResult.OK;
    }

    void BuildCurseForgePage(bool worlds = false, bool shaders = false, bool skins = false)
    {
        int generation = ++curseGeneration;
        var lifetime = CancellationTokenSource.CreateLinkedTokenSource(updatesLifetime.Token);
        var page = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Tag = "page", Margin = Padding.Empty };
        curseRoot = page;
        page.Disposed += (_, _) => { lifetime.Cancel(); lifetime.Dispose(); };
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 88)); page.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); page.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        bool Current() => !IsDisposed && !page.IsDisposed && generation == curseGeneration && currentPage == (skins ? "CurseSkins" : shaders ? "CurseShaders" : worlds ? "CurseWorlds" : "CurseForge");
        var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Tag = "page", Margin = Padding.Empty };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); toolbar.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); toolbar.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        var tabs = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Tag = "page", Margin = Padding.Empty };
        tabs.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tabs.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112)); tabs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); tabs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        var back = MakeButton(skins ? T("Back to skins", "Skinlere dön") : shaders ? T("Back to shaders", "Shaderlara dön") : worlds ? T("Back to worlds", "Dünyalara dön") : T("Back to packs", "Packlere dön"), () => ShowPage(skins ? "Skins" : shaders ? "Mods" : worlds ? "Worlds" : "Addons"));
        var resources = MakeButton(T("Resource packs", "Kaynak paketleri"), () => { });
        var behaviors = MakeButton(T("Behavior packs", "Davranış paketleri"), () => { });
        int category = skins ? CurseForgeApi.Skins : shaders ? CurseForgeApi.Shaders : worlds ? 0 : CurseForgeApi.Resources, offset = 0, requestGeneration = 0, filter = skins ? CurseForgeApi.SkinPacks : 0, activeFilterIndex = 0;
        var filters = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Tag = "page" };
        var filterWindow = new Panel { Dock = DockStyle.Fill, Tag = "page" };
        bool hasNext = false;
        tabs.Controls.Add(back, 0, 0);
        if (worlds)
        {
            var strip = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
            strip.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38));
            strip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            strip.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38));
            var left = MakeButton("‹", () => { }, 34);
            var right = MakeButton("›", () => { }, 34);
            left.Click += (_, _) => MoveCategories(-180);
            right.Click += (_, _) => MoveCategories(180);
            left.AccessibleName = T("Previous categories", "Önceki kategoriler");
            right.AccessibleName = T("More categories", "Diğer kategoriler");
            left.Height = right.Height = 32;
            filterWindow.Controls.Add(filters);
            strip.Controls.Add(left, 0, 0); strip.Controls.Add(filterWindow, 1, 0); strip.Controls.Add(right, 2, 0);
            tabs.Controls.Add(strip, 1, 0); tabs.SetColumnSpan(strip, 2);
            filterWindow.Resize += (_, _) => MoveCategories(0);
            filters.SizeChanged += (_, _) => MoveCategories(0);
            void MoveCategories(int amount)
            {
                int limit = Math.Max(0, filters.Width - filterWindow.ClientSize.Width);
                filters.Left = -Math.Clamp(-filters.Left + amount, 0, limit);
                left.Enabled = filters.Left < 0;
                right.Enabled = -filters.Left < limit;
            }
        }
        else if (!shaders && !skins) { tabs.Controls.Add(resources, 1, 0); tabs.Controls.Add(behaviors, 2, 0); }
        foreach (Control button in tabs.Controls) { button.Dock = DockStyle.Fill; button.Margin = new Padding(0, 0, 6, 6); if (button is Button) button.Font = LauncherFont.Create(9); }
        var searchRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Tag = "page", Margin = Padding.Empty };
        searchRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84)); searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108));
        var searchHost = new RoundedPanel { Dock = DockStyle.Fill, CornerRadius = 16, Tag = "card", Padding = new Padding(12, 8, 12, 4), Margin = new Padding(0, 0, 6, 6) };
        var search = MakeTextBox(); search.BorderStyle = BorderStyle.None; search.Dock = DockStyle.Fill; search.PlaceholderText = T("Search packs…", "Paket ara…"); search.AccessibleName = T("Search CurseForge packs", "CurseForge paketlerinde ara");
        searchHost.Controls.Add(search);
        var find = MakeButton(T("Search", "Ara"), () => { }); find.Dock = DockStyle.Fill; find.Margin = new Padding(0, 0, 6, 6);
        var connect = MakeButton(T("Connection", "Bağlantı"), () => { }); connect.Dock = DockStyle.Fill; connect.Margin = new Padding(0, 0, 0, 6); connect.Font = LauncherFont.Create(9);
        searchRow.Controls.Add(searchHost, 0, 0); searchRow.Controls.Add(find, 1, 0); searchRow.Controls.Add(connect, 2, 0);
        toolbar.Controls.Add(tabs, 0, 0); toolbar.Controls.Add(searchRow, 0, 1);
        var feedback = WrappedText(""); feedback.Dock = DockStyle.Fill;
        var cards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Tag = "page", Margin = Padding.Empty };
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); cards.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var navigation = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Tag = "page", Margin = Padding.Empty, Padding = new Padding(0, 6, 0, 0) };
        navigation.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100)); navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        var previous = MakeButton(T("Previous", "Önceki"), () => { }); var next = MakeButton(T("Next", "Sonraki"), () => { });
        previous.Dock = next.Dock = DockStyle.Fill; previous.Margin = next.Margin = Padding.Empty;
        var position = WrappedText(""); position.Dock = DockStyle.Fill; position.TextAlign = ContentAlignment.MiddleCenter;
        navigation.Controls.Add(previous, 0, 0); navigation.Controls.Add(position, 1, 0); navigation.Controls.Add(next, 2, 0);

        async Task Load()
        {
            int request = ++requestGeneration;
            hasNext = false;
            foreach (Control control in cards.Controls.Cast<Control>().ToArray()) control.Dispose();
            cards.Controls.Clear();
            resources.Tag = category == CurseForgeApi.Resources ? "primary" : null; behaviors.Tag = category == CurseForgeApi.Behaviors ? "primary" : null;
            ApplyButtonPalette(resources); ApplyButtonPalette(behaviors);
            find.Enabled = previous.Enabled = next.Enabled = false;
            feedback.Text = T("Loading CurseForge packs…", "CurseForge paketleri yükleniyor…");
            try
            {
                string key = ReadCurseKey();
                if (key.Length == 0) { feedback.Text = T("Use Connection to add your CurseForge API key.", "CurseForge API anahtarını eklemek için Bağlantı’yı aç."); return; }
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); timeout.CancelAfter(TimeSpan.FromSeconds(30));
                if (worlds && category == 0)
                {
                    var maps = await CurseForgeApi.WorldCategoriesAsync(CurseHttp, key, timeout.Token);
                    if (!Current() || request != requestGeneration) return;
                    category = maps.ClassId;
                    foreach (var item in new[] { (Id: 0, Name: T("All", "Tümü")) }.Concat(maps.Categories))
                    {
                        var tab = MakeButton(item.Name, () => { }, Math.Max(90, TextRenderer.MeasureText(item.Name, Font).Width + 24));
                        tab.Height = 31;
                        tab.Click += async (_, _) => { activeFilterIndex = filters.Controls.IndexOf(tab); filter = item.Id; offset = 0; await Load(); };
                        filters.Controls.Add(tab);
                    }
                }
                foreach (var tab in filters.Controls.OfType<AnimatedRoundButton>())
                {
                    tab.Tag = filters.Controls.IndexOf(tab) == activeFilterIndex ? "primary" : null;
                    ApplyButtonPalette(tab);
                }
                var result = await CurseForgeApi.SearchAsync(CurseHttp, key, category, search.Text, offset, timeout.Token, filter, shaders);
                if (!Current() || request != requestGeneration) return;
                hasNext = result.HasNext;
                cards.SuspendLayout();
                foreach (Control control in cards.Controls.Cast<Control>().ToArray()) control.Dispose();
                cards.Controls.Clear();
                int column = 0;
                foreach (var pack in result.Packs)
                {
                    var card = new RoundedPanel { Dock = DockStyle.Fill, CornerRadius = 22, Padding = new Padding(12), Margin = column == 0 ? new Padding(0, 0, 6, 0) : new Padding(6, 0, 0, 0) };
                    var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Tag = "card", Margin = Padding.Empty };
                    content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); content.RowStyles.Add(new RowStyle(SizeType.Absolute, 76)); content.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); content.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
                    var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Tag = "card", Margin = Padding.Empty };
                    header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                    header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 68)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                    var logo = new PictureBox { Dock = DockStyle.Fill, BackColor = SurfaceAlt, SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(0, 0, 8, 8), AccessibleName = pack.Name + " logo" };
                    logo.Disposed += (_, _) => logo.Image?.Dispose();
                    var identity = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Tag = "card", Margin = Padding.Empty };
                    identity.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); identity.RowStyles.Add(new RowStyle(SizeType.Percent, 65)); identity.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
                    var name = WrappedText(pack.Name, 11, true); name.Dock = DockStyle.Fill; name.AutoEllipsis = true;
                    var authors = WrappedText(pack.Authors, 9); authors.Dock = DockStyle.Fill; authors.AutoEllipsis = true;
                    identity.Controls.Add(name, 0, 0); identity.Controls.Add(authors, 0, 1); header.Controls.Add(logo, 0, 0); header.Controls.Add(identity, 1, 0);
                    var summary = WrappedText(pack.Summary, 10); summary.Dock = DockStyle.Fill; summary.AutoEllipsis = true;
                    var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Tag = "card", Margin = Padding.Empty };
                    actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                    actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
                    var install = MakeButton(T("Download & install", "İndir ve kur"), () => { }, icon: "download"); install.Tag = "primary"; install.Dock = DockStyle.Fill; install.Margin = new Padding(0, 0, 4, 0); install.Font = LauncherFont.Create(9); install.Enabled = pack.CanDistribute && !installingCursePack;
                    install.Click += async (_, _) => await InstallCursePack(pack, lifetime.Token, worlds, shaders, skins);
                    var website = MakeButton("CurseForge", () => OpenUrl(pack.Website.AbsoluteUri)); website.Tag = "card-button"; website.Dock = DockStyle.Fill; website.Margin = new Padding(4, 0, 0, 0); website.Font = LauncherFont.Create(9);
                    actions.Controls.Add(install, 0, 0);
                    if (worlds) { actions.SetColumnSpan(install, 2); website.Dispose(); }
                    else actions.Controls.Add(website, 1, 0);
                    content.Controls.Add(header, 0, 0); content.Controls.Add(summary, 0, 1); content.Controls.Add(actions, 0, 2); card.Controls.Add(content); cards.Controls.Add(card, column++, 0);
                    void FitCard() { content.RowStyles[0].Height = card.Height < 180 ? 56 : 76; summary.Visible = card.Height >= 150; }
                    card.Resize += (_, _) => FitCard(); FitCard();
                    _ = LoadCurseLogo(logo, pack.Logo, lifetime.Token);
                }
                cards.ResumeLayout(true); ApplyTheme();
                feedback.Text = result.Packs.Count == 0 ? T("No matching packs found.", "Eşleşen paket bulunamadı.") : T("Downloads require your approval • CurseForge", "İndirme senin onayınla başlar • CurseForge");
                position.Text = (offset / CurseForgeApi.PageSize + 1).ToString();
            }
            catch (OperationCanceledException) { if (Current() && request == requestGeneration) feedback.Text = T("Loading canceled or timed out. Try Search again.", "Yükleme zaman aşımına uğradı. Ara ile tekrar dene."); }
            catch (HttpRequestException ex) { if (Current() && request == requestGeneration) feedback.Text = ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ? T("API access failed. Check Connection.", "API erişimi reddedildi. Bağlantı’yı kontrol et.") : T("CurseForge could not be reached. Try again.", "CurseForge’a ulaşılamadı. Tekrar dene."); }
            catch { if (Current() && request == requestGeneration) feedback.Text = T("The pack list could not be loaded.", "Paket listesi yüklenemedi."); }
            finally { if (Current() && request == requestGeneration) { find.Enabled = true; previous.Enabled = offset > 0; next.Enabled = hasNext; } }
        }
        resources.Click += async (_, _) => { if (category == CurseForgeApi.Resources) return; category = CurseForgeApi.Resources; offset = 0; await Load(); };
        behaviors.Click += async (_, _) => { if (category == CurseForgeApi.Behaviors) return; category = CurseForgeApi.Behaviors; offset = 0; await Load(); };
        find.Click += async (_, _) => { offset = 0; await Load(); };
        search.KeyDown += async (_, e) => { if (e.KeyCode != Keys.Enter) return; e.SuppressKeyPress = true; offset = 0; await Load(); };
        connect.Click += async (_, _) => { if (ConfigureCurseKey()) { offset = 0; await Load(); } };
        previous.Click += async (_, _) => { offset = Math.Max(0, offset - CurseForgeApi.PageSize); await Load(); };
        next.Click += async (_, _) => { if (!hasNext || offset >= 9998) return; offset += CurseForgeApi.PageSize; await Load(); };
        page.Controls.Add(toolbar, 0, 0); page.Controls.Add(feedback, 0, 1); page.Controls.Add(cards, 0, 2); page.Controls.Add(navigation, 0, 3); contentHost.Controls.Add(page);
        AddPageHeading("CurseForge", skins ? T("Discover Bedrock skin packs.", "Bedrock skin paketlerini keşfet.") : shaders ? T("Java shader packs for Cinnabar.", "Cinnabar için Java shader paketleri.") : worlds ? T("Discover Bedrock worlds.", "Bedrock dünyalarını keşfet.") : T("Discover Bedrock resource and behavior packs.", "Bedrock kaynak ve davranış paketlerini keşfet."));
        BeginInvoke(new Action(async () => { if (Current()) await Load(); }));
    }

    void DisposeCursePage() { curseRoot?.Dispose(); curseRoot = null; }

    async Task LoadCurseLogo(PictureBox picture, Uri? url, CancellationToken cancel)
    {
        if (url == null) return;
        try
        {
            var bytes = await CurseForgeApi.LogoAsync(CurseHttp, url, cancel);
            using var stream = new MemoryStream(bytes); using var source = Image.FromStream(stream);
            if (source.Width > 4096 || source.Height > 4096) return;
            if (!picture.IsDisposed && !cancel.IsCancellationRequested) picture.Image = new Bitmap(source);
        }
        catch { /* Missing artwork never blocks the pack list. */ }
    }

    async Task InstallCursePack(CursePack pack, CancellationToken pageCancel, bool world = false, bool shaders = false, bool skins = false)
    {
        if (installingCursePack) return;
        installingCursePack = true;
        string? staged = null;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(pageCancel, updatesLifetime.Token); timeout.CancelAfter(TimeSpan.FromMinutes(5));
            UpdateStatus(T("Checking pack files…", "Paket dosyaları kontrol ediliyor…"));
            var file = await CurseForgeApi.LatestFileAsync(CurseHttp, ReadCurseKey(), pack, timeout.Token, world, shaders, skins);
            if (file == null)
            {
                ShowThemedPopup("CurseForge", T("This pack has no available download for third-party launchers. You can visit its CurseForge page.", "Bu paket için harici yükleyicilere açık indirme bulunmuyor. CurseForge sayfasını ziyaret edebilirsin.")); return;
            }
            if (ShowThemedPopup(shaders ? T("Install shader", "Shader kur") : world ? T("Import world", "Dünya içe aktar") : T("Install pack", "Paket kurulumu"), shaders ? T($"Download {pack.Name} into Cinnabar?", $"{pack.Name} Cinnabar’a indirilsin mi?") : world || skins ? T($"Download and open {pack.Name} in Minecraft?", $"{pack.Name} indirilip Minecraft’ta açılsın mı?") : T($"Download and install {pack.Name}?\n\n{file.Name}\n{file.Size / 1048576d:N1} MB\n\nEnable installed packs in Minecraft world settings.", $"{pack.Name} indirilip kurulsun mu?\n\n{file.Name}\n{file.Size / 1048576d:N1} MB\n\nKurulan paketleri Minecraft dünya ayarlarından etkinleştirebilirsin."), true) != DialogResult.Yes)
            {
                UpdateStatus(T("Pack download canceled.", "Paket indirmesi iptal edildi.")); return;
            }
            timeout.Token.ThrowIfCancellationRequested();
            Directory.CreateDirectory(addonDownloads); staged = Path.Combine(addonDownloads, Guid.NewGuid().ToString("N") + Path.GetExtension(file.Name));
            UpdateStatus(T($"Downloading {pack.Name}…", $"{pack.Name} indiriliyor…"));
            await CurseForgeApi.DownloadAsync(CurseHttp, file, staged, timeout.Token);
            timeout.Token.ThrowIfCancellationRequested();
            if (shaders)
            {
                InstallCinnabarShader(staged, file.Name);
                UpdateStatus(T("Shader added. Select it in Cinnabar.", "Shader eklendi. Cinnabar içinden seçebilirsin."));
                return;
            }
            if (world)
            {
                OpenWorldArchive(staged);
                staged = null; // Minecraft opens the archive asynchronously.
                return;
            }
            if (skins)
            {
                CurseForgeApi.ValidateSkinPack(staged);
                if (!Path.GetExtension(staged).Equals(".mcpack", StringComparison.OrdinalIgnoreCase))
                {
                    string package = Path.ChangeExtension(staged, ".mcpack");
                    File.Move(staged, package);
                    staged = package;
                }
                using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(staged) { UseShellExecute = true });
                staged = null;
                UpdateStatus(T("Import requested. Check the result in Minecraft.", "İçe aktarma isteği açıldı. Sonucu Minecraft’ta kontrol et."));
                return;
            }
            int count = InstallPackageArchive(staged, 0);
            LogAction($"Installed {count} CurseForge pack(s), project {pack.Id}", $"{count} CurseForge paketi kuruldu, proje {pack.Id}");
            UpdateStatus(T($"Installed {count} pack(s). Enable them in Minecraft world settings.", $"{count} paket kuruldu. Minecraft dünya ayarlarından etkinleştir."));
            ShowThemedPopup(T("Pack installed", "Paket kuruldu"), T($"{pack.Name} is installed. You can enable it in Minecraft.", $"{pack.Name} kuruldu. Minecraft içinden etkinleştirebilirsin."));
        }
        catch (OperationCanceledException) { if (!IsDisposed) UpdateStatus(T("Pack download canceled or timed out.", "Paket indirmesi iptal edildi veya zaman aşımına uğradı.")); }
        catch (Exception ex) { if (!IsDisposed) { UpdateStatus(T("Pack installation failed.", "Paket kurulamadı.")); ShowThemedPopup(T("Pack installation failed", "Paket kurulamadı"), ex.Message); } }
        finally { installingCursePack = false; if (staged != null) { try { File.Delete(staged); } catch { } } }
    }
}
