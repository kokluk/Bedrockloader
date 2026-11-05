using System.Drawing.Drawing2D;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    Label WrappedText(string text, float size = 10, bool bold = false) => new()
    {
        Text = text, AutoSize = false, Dock = DockStyle.Top,
        Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
        ForeColor = TextColor, BackColor = Color.Transparent
    };

    void BuildComfortableHome()
    {
        AddPageHeading(T("Welcome", "Hoş geldin"), T("Your Minecraft Bedrock hub", "Minecraft Bedrock merkeziniz"));
        var page = new Panel { Dock = DockStyle.Fill, AutoScroll = false, Tag = "page" };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Height = 350, ColumnCount = 1, RowCount = 4, Tag = "page", Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var hero = new RoundedPanel { Dock = DockStyle.Fill, CornerRadius = 22, Padding = new Padding(18), Margin = new Padding(0, 0, 0, 14) };
        bool installed = MinecraftInstallation.IsInstalled(settings.ContentPath);
        var launch = MakeButton(cinnabarMode ? CinnabarLaunchText() : installed ? T("Launch Minecraft", "Minecraft'ı başlat") : T("Install Minecraft", "Minecraft'ı yükle"), () => { if (cinnabarMode) _ = LaunchCinnabarAsync(); else LaunchGame(); }, icon: "play");
        launch.Tag = "minecraft-launch";
        minecraftLaunchButton = cinnabarMode ? null : launch;
        launch.Dock = DockStyle.Fill; launch.Font = new Font("Segoe UI", 13, FontStyle.Bold); hero.Controls.Add(launch);
        var toggle = new SymbolButton { Symbol = "game-switch", Dock = DockStyle.Right, Width = 44, AccessibleName = T("Switch game", "Oyunu değiştir") };

        hero.Controls.Add(toggle);
        layout.Controls.Add(hero, 0, 0);

        int mods = Directory.Exists(modsFolder) ? Directory.EnumerateFiles(modsFolder, "*.dll", SearchOption.AllDirectories).Count() : 0;
        int behaviors = CountFolders(Path.Combine(comMojangRoot, "behavior_packs"));
        int resources = CountFolders(Path.Combine(comMojangRoot, "resource_packs"));
        var counts = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Tag = "page", Margin = Padding.Empty };
        for (int i = 0; i < 3; i++) counts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
        void Counter(int column, int value, string title)
        {
            var card = new RoundedPanel { Dock = DockStyle.Fill, CornerRadius = 20, Padding = new Padding(14), Margin = new Padding(column == 0 ? 0 : 6, 0, column == 2 ? 0 : 6, 12) };
            var name = WrappedText(title, 9); name.Height = 28; name.Dock = DockStyle.Bottom;
            var number = WrappedText(value.ToString(), 26, true); number.Dock = DockStyle.Fill;
            card.Controls.Add(number); card.Controls.Add(name); counts.Controls.Add(card, column, 0);
        }
        Counter(0, mods, T("Mods", "Modlar")); Counter(1, resources, T("Resource packs", "Kaynak paketleri")); Counter(2, behaviors, T("Behavior packs", "Davranış paketleri"));
        var countHost = new Panel { Dock = DockStyle.Fill, Tag = "page", Margin = Padding.Empty };
        countHost.Controls.Add(counts);
        var cinnabarCounts = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Tag = "page", Margin = Padding.Empty };
        string cinnabarRoot = CinnabarDataRoot();
        foreach (var (folder, label) in new[]
        {
            ("mods", T("Mods", "Modlar")), ("resource-packs/global", T("Resource packs", "Kaynak paketleri")),
            ("shaderpacks", T("Shaders", "Shaderlar"))
        })
        {
            cinnabarCounts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
            string path = Path.Combine(cinnabarRoot, folder);
            int count = folder == "shaderpacks" ? ListCinnabarShaders(path).Count() :
                Directory.Exists(path) ? Directory.EnumerateFileSystemEntries(path).Count() : 0;
            var card = new RoundedPanel { Dock = DockStyle.Fill, CornerRadius = 20, Padding = new Padding(14), Margin = new Padding(0, 0, 8, 12) };
            var title = WrappedText(label, 9); title.Height = 28; title.Dock = DockStyle.Bottom;
            var number = WrappedText(count.ToString(), 26, true); number.Dock = DockStyle.Fill;
            card.Controls.Add(number); card.Controls.Add(title);
            cinnabarCounts.Controls.Add(card);
        }
        countHost.Controls.Add(cinnabarCounts);
        layout.Controls.Add(countHost, 0, 2);
        var shortcuts = new FlowLayoutPanel { Dock = DockStyle.Fill, Tag = "page", WrapContents = true, Margin = Padding.Empty, Padding = new Padding(0, 8, 0, 0) };
        shortcuts.Controls.Add(MakeButton(T("View mods", "Modları gör"), () => ShowPage("Mods"), 155));
        shortcuts.Controls.Add(MakeButton(T("View packs", "Packleri gör"), () => ShowPage("Addons"), 155));
        installModloaderButton = MakeButton(T("Install modloader", "Modloader'ı kur"), InstallModloader, 155);
        installModloaderButton.Enabled = !installingModloader;
        shortcuts.Controls.Add(installModloaderButton);
        var minecraftShortcuts = shortcuts.Controls.Cast<Control>().ToArray();
        var compare = MakeButton("", () => { if (cinnabarMode) ExplainCinnabar(); else CompareGames(); }, 155);
        shortcuts.Controls.Add(compare);
        var cinnabarUpdates = MakeButton(T("Check for updates", "Güncellemeleri denetle"), () => _ = CheckCinnabarUpdateAsync(), 185, icon: "Updates");
        shortcuts.Controls.Add(cinnabarUpdates);
        layout.Controls.Add(shortcuts, 0, 3);
        var progress = new ProgressBar { Dock = DockStyle.Fill, Margin = new Padding(18, 0, 18, 6), Maximum = 100 };
        layout.Controls.Add(progress, 0, 1);
        cinnabarProgress = progress;
        cinnabarProgressRow = layout.RowStyles[1];
        page.Controls.Add(layout);
        contentHost.Controls.Add(page);
        toggle.Click += (_, _) => { cinnabarMode = !cinnabarMode; UpdateHomeMode(); };
        UpdateHomeMode();

        void UpdateHomeMode()
        {
            layout.SuspendLayout();
            launch.Text = cinnabarMode ? CinnabarLaunchText() : installed ? T("Launch Minecraft", "Minecraft'ı başlat") : T("Install Minecraft", "Minecraft'ı yükle");
            minecraftLaunchButton = cinnabarMode ? null : launch;
            compare.Text = cinnabarMode ? T("What is Cinnabar?", "Cinnabar nedir?") : T("Version differences", "Sürüm farkları");
            cinnabarUpdates.Visible = cinnabarMode;
            counts.Visible = !cinnabarMode;
            cinnabarCounts.Visible = cinnabarMode;
            foreach (var shortcut in minecraftShortcuts) shortcut.Visible = !cinnabarMode;
            ShowCinnabarProgress(cinnabarBusy ? cinnabarPercent : -1);
            layout.ResumeLayout(true);
            UpdateModeNavigation("Home");
        }
    }

    void BuildSoftAddons()
    {
        var page = new Panel { Dock = DockStyle.Fill, AutoScroll = false, Tag = "page" };
        var layout = new BufferedActionLayout { Dock = DockStyle.Fill, Height = 390, ColumnCount = 1, RowCount = 3, Tag = "page", Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 158)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        ConfigureDropPanel(true); dropPanel.BorderStyle = BorderStyle.None;
        dropPanel.Dock = DockStyle.Fill; dropPanel.Margin = new Padding(0, 0, 0, 16); dropPanel.Padding = new Padding(18);
        dropTitle.Dock = DockStyle.Top; dropTitle.AutoSize = false; dropTitle.Height = 30; dropTitle.Font = new Font("Segoe UI", 11, FontStyle.Bold);
        dropSubtitle.Dock = DockStyle.Top; dropSubtitle.AutoSize = false; dropSubtitle.Height = 32;
        dropSubtitle.Text = T("Drop a .mcpack, .mcaddon or .zip package here.", ".mcpack, .mcaddon veya .zip paketini buraya bırak.");
        dropPanel.Controls.Clear();
        var install = MakeButton(T("Choose file & install", "Dosya seç ve yükle"), ChooseAndInstallAddon, 190); install.Dock = DockStyle.Bottom; install.Tag = "primary"; install.Height = 32;
        dropPanel.Controls.Add(install); dropPanel.Controls.Add(dropSubtitle); dropPanel.Controls.Add(dropTitle);
        layout.Controls.Add(dropPanel, 0, 0);
        void FitPackHeader()
        {
            bool compact = page.ClientSize.Height < 330;
            layout.RowStyles[0].Height = compact ? 118 : 158;
            dropPanel.Padding = new Padding(compact ? 12 : 18);
            dropPanel.Margin = new Padding(0, 0, 0, compact ? 12 : 16);
            dropTitle.Height = compact ? 24 : 30;
            dropSubtitle.Height = compact ? 24 : 32;
        }
        page.Resize += (_, _) => FitPackHeader();
        FitPackHeader();
        var columns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Tag = "page", Margin = Padding.Empty };
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        RoundedPanel PackColumn(CheckedListBox list, string headingText, string emptyText, int column, out Label count, out Label empty)
        {
            var card = new RoundedPanel { Dock = DockStyle.Fill, CornerRadius = 22, Padding = new Padding(16), Margin = column == 0 ? new Padding(0, 0, 6, 0) : new Padding(6, 0, 0, 0) };
            var header = new Panel { Dock = DockStyle.Top, Height = 38, Tag = "card" };
            var heading = WrappedText(headingText, 11, true); heading.Dock = DockStyle.Fill;
            count = WrappedText("0", 9); count.Dock = DockStyle.Right; count.Width = 30; count.TextAlign = ContentAlignment.TopRight;
            header.Controls.Add(heading); header.Controls.Add(count);
            list.Dock = DockStyle.Fill; list.CheckOnClick = true; list.IntegralHeight = false; list.BorderStyle = BorderStyle.None; list.Font = new Font("Segoe UI", 9.5f);
            list.ItemCheck -= AddonList_ItemCheck; list.ItemCheck += AddonList_ItemCheck;
            empty = WrappedText(emptyText, 10); empty.Dock = DockStyle.Fill;
            card.Controls.Add(list); card.Controls.Add(empty); card.Controls.Add(header);
            return card;
        }
        columns.Controls.Add(PackColumn(resourcePackList, T("Resource packs", "Kaynak paketleri"), T("No resource packs yet.", "Henüz kaynak paketi yok."), 0, out var resourcesCount, out var resourcesEmpty), 0, 0);
        columns.Controls.Add(PackColumn(behaviorPackList, T("Behavior packs", "Davranış paketleri"), T("No behavior packs yet.", "Henüz davranış paketi yok."), 1, out var behaviorsCount, out var behaviorsEmpty), 1, 0);
        resourcePackCount = resourcesCount; resourcePackEmpty = resourcesEmpty; behaviorPackCount = behaviorsCount; behaviorPackEmpty = behaviorsEmpty;
        var deletePacks = MakeButton(T("Delete checked", "Seçilenleri sil"), RemoveSelectedAddon, 150);
        deleteAddonsButton = deletePacks;
        deleteAddonsReveal = new AnimatedActionReveal(deletePacks, 38, 10, height =>
        {
            if (layout.IsDisposed || layout.RowStyles[2].Height == height) return;
            layout.RowStyles[2].Height = height;
        }) { Dock = DockStyle.Fill, BackColor = Surface, Tag = "page" };
        layout.Controls.Add(columns, 0, 1); layout.Controls.Add(deleteAddonsReveal, 0, 2);
        page.Controls.Add(layout); contentHost.Controls.Add(page);
        AddPageHeading(T("Packs", "Packler"), T("Install and manage your Bedrock packs.", "Bedrock paketlerini yükle ve yönet.")); RefreshAddonList();
        AddCurseForgeHeadingButton("CurseForge");
    }

    void AddCurseForgeHeadingButton(string page)
    {
        var curseForge = MakeButton("CurseForge", () => ShowPage(page), 166, "external");
        curseForge.Height = 32; curseForge.Font = new Font("Segoe UI", 9);
        if (pageHeadingPanel is { } heading)
        {
            heading.Controls.Add(curseForge);
            void PlaceCurseForge() => curseForge.Location = new Point(Math.Max(0, heading.ClientSize.Width - curseForge.Width), 3);
            heading.Resize += (_, _) => PlaceCurseForge(); PlaceCurseForge();
        }
    }

    void BuildComfortableAbout()
    {
        var page = new Panel { Dock = DockStyle.Fill, AutoScroll = false, Tag = "page" };
        var card = new RoundedPanel { Dock = DockStyle.Fill, CornerRadius = 24, Padding = new Padding(20) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Tag = "card", Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        var logo = new AboutLogo { Dock = DockStyle.Fill, ForeColor = TextColor, Margin = Padding.Empty, AccessibleName = "BedrockLoader logo" };
        var title = WrappedText("BedrockLoader", 22, true); title.Dock = DockStyle.Fill; title.TextAlign = ContentAlignment.MiddleCenter;
        var description = WrappedText(T("Manage your Minecraft Bedrock library in one place. Add local mods, install add-on packs and review your activity.", "Minecraft Bedrock kütüphaneni tek yerden yönet. Yerel modlarını ekle, eklenti paketlerini yükle ve işlem kayıtlarını incele."), 11);
        description.Dock = DockStyle.Fill; description.TextAlign = ContentAlignment.TopCenter; description.Padding = new Padding(12, 6, 12, 0);
        var version = WrappedText(T($"Version {DisplayVersion} • Windows", $"Sürüm {DisplayVersion} • Windows"), 9); version.Dock = DockStyle.Fill; version.TextAlign = ContentAlignment.MiddleCenter;
        var github = MakeButton("GitHub", () => { OpenUrl("https://github.com/kokluk"); LogAction("Opened GitHub profile: kokluk"); }, 220, "github");
        github.Tag = "primary"; github.Anchor = AnchorStyles.None; github.Margin = Padding.Empty;
        layout.Controls.Add(logo, 0, 0); layout.Controls.Add(title, 0, 1); layout.Controls.Add(description, 0, 2); layout.Controls.Add(version, 0, 3); layout.Controls.Add(github, 0, 5);
        void FitAbout()
        {
            bool compact = card.ClientSize.Height < 360;
            card.Padding = new Padding(compact ? 14 : 20);
            layout.RowStyles[0].Height = compact ? 68 : 112;
            layout.RowStyles[1].Height = compact ? 38 : 46;
            layout.RowStyles[2].Height = compact ? 64 : 72;
            layout.RowStyles[3].Height = compact ? 22 : 26;
            layout.RowStyles[5].Height = compact ? 40 : 44;
        }
        card.Resize += (_, _) => FitAbout();
        card.Controls.Add(layout); page.Controls.Add(card); contentHost.Controls.Add(page); FitAbout();
        AddPageHeading(T("About", "Hakkında"), "");
        pageHeadingPanel!.Height = 42;
        pageTitle.AutoSize = false;
        pageTitle.Dock = DockStyle.Fill;
        pageTitle.TextAlign = ContentAlignment.MiddleCenter;
        pageSubtitle.Visible = false;
    }

    sealed class AboutLogo : Control
    {
        readonly Bitmap image;
        public AboutLogo()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.Transparent;
            using var stream = typeof(MainForm).Assembly.GetManifestResourceStream("BedrockLoader.WolfTransparent.png")!;
            using var source = Image.FromStream(stream);
            image = new Bitmap(source);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            int size = Math.Max(1, Math.Min(104, Math.Min(Width, Height)));
            var bounds = new Rectangle((Width - size) / 2, (Height - size) / 2, size, size);
            using var attributes = new System.Drawing.Imaging.ImageAttributes();
            var tint = new System.Drawing.Imaging.ColorMatrix { Matrix00 = ForeColor.R / 255f, Matrix11 = ForeColor.G / 255f, Matrix22 = ForeColor.B / 255f };
            attributes.SetColorMatrix(tint);
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.DrawImage(image, bounds, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
        }
        protected override void Dispose(bool disposing) { if (disposing) image.Dispose(); base.Dispose(disposing); }
    }

    sealed class SoftMenuRenderer(Func<Color> background, Func<Color> foreground, Func<Color> accent) : ToolStripProfessionalRenderer
    {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) { e.Graphics.Clear(background()); }
        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { }
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            using var brush = new SolidBrush(e.Item.Selected ? MixColors(background(), accent(), 0.18) : background());
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = RoundedPath(new Rectangle(0, 0, e.Item.Width - 1, e.Item.Height - 1), 7); e.Graphics.FillPath(brush, path);
        }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) { e.TextColor = foreground(); base.OnRenderItemText(e); }
    }
}
