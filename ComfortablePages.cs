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
        var page = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Tag = "page" };
        var layout = new TableLayoutPanel { Dock = DockStyle.Top, Height = 350, ColumnCount = 1, RowCount = 3, Tag = "page", Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 106));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 126));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
        var hero = new RoundedPanel { Dock = DockStyle.Fill, CornerRadius = 22, Padding = new Padding(18), Margin = new Padding(0, 0, 0, 14) };
        var launch = MakeButton(T("▶  Launch Minecraft", "▶  Minecraft'ı başlat"), LaunchGame);
        launch.Dock = DockStyle.Fill; launch.Font = new Font("Segoe UI", 13, FontStyle.Bold); hero.Controls.Add(launch);
        layout.Controls.Add(hero, 0, 0);

        int mods = Directory.Exists(modsFolder) ? Directory.GetFiles(modsFolder, "*", SearchOption.AllDirectories).Length : 0;
        int addons = CountFolders(Path.Combine(comMojangRoot, "behavior_packs")) + CountFolders(Path.Combine(comMojangRoot, "skin_packs")) + CountFolders(Path.Combine(comMojangRoot, "world_templates"));
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
        Counter(0, mods, T("Mods", "Modlar")); Counter(1, addons, T("Add-ons", "Eklentiler")); Counter(2, resources, T("Resource packs", "Kaynak paketleri"));
        layout.Controls.Add(counts, 0, 1);
        var shortcuts = new FlowLayoutPanel { Dock = DockStyle.Fill, Tag = "page", WrapContents = true, Margin = Padding.Empty, Padding = new Padding(0, 8, 0, 0) };
        shortcuts.Controls.Add(MakeButton(T("View mods", "Modları gör"), () => ShowPage("Mods"), 145));
        shortcuts.Controls.Add(MakeButton(T("View add-ons", "Eklentileri gör"), () => ShowPage("Addons"), 145));
        installModloaderButton = MakeButton(T("Install modloader", "Modloader'ı kur"), InstallModloader, 155);
        installModloaderButton.Enabled = !installingModloader;
        shortcuts.Controls.Add(installModloaderButton);
        layout.Controls.Add(shortcuts, 0, 2); page.Controls.Add(layout); contentHost.Controls.Add(page);
    }

    void BuildSoftAddons()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Tag = "page", Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 144)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        ConfigureDropPanel(true); dropPanel.BorderStyle = BorderStyle.None;
        dropPanel.Dock = DockStyle.Fill; dropPanel.Margin = new Padding(0, 0, 0, 16); dropPanel.Padding = new Padding(18);
        dropTitle.Dock = DockStyle.Top; dropTitle.AutoSize = false; dropTitle.Height = 26; dropTitle.Font = new Font("Segoe UI", 11, FontStyle.Bold);
        dropSubtitle.Dock = DockStyle.Top; dropSubtitle.AutoSize = false; dropSubtitle.Height = 28;
        dropSubtitle.Text = T("Drop a .mcpack, .mcaddon or .zip package here.", ".mcpack, .mcaddon veya .zip paketini buraya bırak.");
        dropPanel.Controls.Clear();
        var install = MakeButton(T("Choose file & install", "Dosya seç ve yükle"), ChooseAndInstallAddon, 190); install.Dock = DockStyle.Bottom; install.Tag = "primary"; install.Height = 32;
        dropPanel.Controls.Add(install); dropPanel.Controls.Add(dropSubtitle); dropPanel.Controls.Add(dropTitle);
        layout.Controls.Add(dropPanel, 0, 0);
        var installed = new RoundedPanel { Dock = DockStyle.Fill, CornerRadius = 22, Padding = new Padding(18), Margin = Padding.Empty };
        var heading = WrappedText(T("Installed packs", "Yüklü eklentiler"), 11, true); heading.Height = 34;
        addonList.Dock = DockStyle.Fill; addonList.CheckOnClick = true; addonList.IntegralHeight = false; addonList.BorderStyle = BorderStyle.None; addonList.Font = new Font("Segoe UI", 10);
        addonList.ItemCheck -= AddonList_ItemCheck; addonList.ItemCheck += AddonList_ItemCheck;
        installed.Controls.Add(addonList); installed.Controls.Add(heading);
        deleteAddonsButton = MakeButton(T("Delete checked", "Seçilenleri sil"), RemoveSelectedAddon, 150);
        deleteAddonsButton.Dock = DockStyle.Bottom; deleteAddonsButton.Visible = false; installed.Controls.Add(deleteAddonsButton);
        layout.Controls.Add(installed, 0, 1); contentHost.Controls.Add(layout);
        AddPageHeading(T("Add-ons", "Eklentiler"), T("Install and manage your Bedrock packs.", "Bedrock paketlerini yükle ve yönet.")); RefreshAddonList();
    }

    void BuildComfortableAbout()
    {
        var page = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Tag = "page" };
        var card = new RoundedPanel { Dock = DockStyle.Top, Height = 294, CornerRadius = 24, Padding = new Padding(24) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Tag = "card", Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        var title = WrappedText("BedrockLoader", 22, true); title.Dock = DockStyle.Fill;
        var version = WrappedText(T("Version 0.12 • Windows", "Sürüm 0.12 • Windows")); version.Dock = DockStyle.Fill;
        var description = WrappedText(T("Manage your Minecraft Bedrock library in one place. Add local mods, install add-on packs and review your activity.", "Minecraft Bedrock kütüphaneni tek yerden yönet. Yerel modlarını ekle, eklenti paketlerini yükle ve işlem kayıtlarını incele."), 11);
        description.Dock = DockStyle.Fill; description.Padding = new Padding(0, 12, 0, 12);
        var github = MakeButton(T("Visit kokluk on GitHub ↗", "GitHub'da kokluk ↗"), () => { OpenUrl("https://github.com/kokluk"); LogAction("Opened GitHub profile: kokluk"); }, 220); github.Tag = "primary";
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
