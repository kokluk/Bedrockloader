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
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Height = 350, ColumnCount = 1, RowCount = 3, Tag = "page", Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var hero = new RoundedPanel { Dock = DockStyle.Fill, CornerRadius = 22, Padding = new Padding(18), Margin = new Padding(0, 0, 0, 14) };
        var launch = MakeButton(T("Launch Minecraft", "Minecraft'ı başlat"), LaunchGame, icon: "play");
        launch.Dock = DockStyle.Fill; launch.Font = new Font("Segoe UI", 13, FontStyle.Bold); hero.Controls.Add(launch);
        layout.Controls.Add(hero, 0, 0);

        int mods = Directory.Exists(modsFolder) ? Directory.GetFiles(modsFolder, "*", SearchOption.AllDirectories).Length : 0;
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
        layout.Controls.Add(counts, 0, 1);
        var shortcuts = new FlowLayoutPanel { Dock = DockStyle.Fill, Tag = "page", WrapContents = true, Margin = Padding.Empty, Padding = new Padding(0, 8, 0, 0) };
        shortcuts.Controls.Add(MakeButton(T("View mods", "Modları gör"), () => ShowPage("Mods"), 155));
        shortcuts.Controls.Add(MakeButton(T("View packs", "Packleri gör"), () => ShowPage("Addons"), 155));
        installModloaderButton = MakeButton(T("Install modloader", "Modloader'ı kur"), InstallModloader, 155);
        installModloaderButton.Enabled = !installingModloader;
        shortcuts.Controls.Add(installModloaderButton);
        layout.Controls.Add(shortcuts, 0, 2); page.Controls.Add(layout); contentHost.Controls.Add(page);
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
    }

    void BuildComfortableAbout()
    {
        var page = new Panel { Dock = DockStyle.Fill, AutoScroll = false, Tag = "page" };
        var card = new RoundedPanel { Dock = DockStyle.Fill, Height = 294, CornerRadius = 24, Padding = new Padding(24) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Tag = "card", Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        var title = WrappedText("BedrockLoader", 22, true); title.Dock = DockStyle.Fill;
        var version = WrappedText(T($"Version {DisplayVersion} • Windows", $"Sürüm {DisplayVersion} • Windows")); version.Dock = DockStyle.Fill;
        var description = WrappedText(T("Manage your Minecraft Bedrock library in one place. Add local mods, install add-on packs and review your activity.", "Minecraft Bedrock kütüphaneni tek yerden yönet. Yerel modlarını ekle, eklenti paketlerini yükle ve işlem kayıtlarını incele."), 11);
        description.Dock = DockStyle.Fill; description.Padding = new Padding(0, 12, 0, 12);
        var github = MakeButton(T("Visit kokluk on GitHub", "GitHub'da kokluk"), () => { OpenUrl("https://github.com/kokluk"); LogAction("Opened GitHub profile: kokluk"); }, 220, "external"); github.Tag = "primary";
        layout.Controls.Add(title, 0, 0); layout.Controls.Add(version, 0, 1); layout.Controls.Add(description, 0, 2); layout.Controls.Add(github, 0, 3);
        card.Controls.Add(layout); page.Controls.Add(card); contentHost.Controls.Add(page);
        AddPageHeading(T("About", "Hakkında"), T("A little about your launcher.", "Yükleyicin hakkında."));
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
