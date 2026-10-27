using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace KoklukBedrockModloader;

public sealed partial class MainForm : Form
{
    const string VersionText = "BEDROCKLOADER • v0.12";
    static readonly HttpClient Http = CreateHttpClient();
    readonly string modsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Minecraft bedrock", "mods");
    readonly string settingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BedrockLoader", "settings.json");
    readonly string logFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BedrockLoader", "logs.txt");
    readonly string appDataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BedrockLoader");
    readonly string addonDownloads;
    readonly string comMojangRoot;
    readonly List<Button> navButtons = new();
    readonly Panel contentHost = new();
    readonly Label status = new();
    readonly ContextMenuStrip languageMenu = new();
    readonly Panel dropPanel = new RoundedPanel { CornerRadius = 20, Tag = "card" };
    readonly Label dropTitle = new();
    readonly Label dropSubtitle = new();
    readonly CheckedListBox modList = new SpacedCheckedListBox();
    Label? modCountLabel;
    readonly CheckedListBox addonList = new SpacedCheckedListBox();
    Button? deleteModsButton;
    Button? deleteAddonsButton;
    readonly Label pageTitle = new();
    readonly Label pageSubtitle = new();
    Panel? pageHeadingPanel;
    AppSettings settings;
    bool english;
    string currentPage = "Mods";
    bool isDragging;
    bool lightMode;
    Panel? topHeader;
    Panel? footerBar;

    Color Bg = Color.FromArgb(10, 14, 23);
    Color Surface = Color.FromArgb(17, 25, 40);
    Color SurfaceAlt = Color.FromArgb(25, 35, 52);
    Color Accent = Color.FromArgb(50, 190, 240);
    Color TextColor = Color.White;
    Color Muted = Color.Silver;

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyDwmTheme();
    }

    public MainForm()
    {
        settings = LoadSettings();
        english = settings.Language == "EN";
        addonDownloads = Path.Combine(appDataRoot, "Addons", "Downloads");
        comMojangRoot = ResolveComMojangRoot();
        lightMode = settings.LightMode;
        ApplyThemePalette(); ApplyLightPalette();

        Text = "BedrockLoader";
        Size = new Size(1040, 680);
        MinimumSize = new Size(800, 540);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;
        // Leave the resize perimeter on the form itself, outside child panels.
        Padding = new Padding(8);
        Font = new Font("Segoe UI", 9f);
        DoubleBuffered = true;
        AllowDrop = true;
        DragEnter += AppDragEnter;
        DragLeave += AppDragLeave;
        DragDrop += AppDragDrop;

        BuildShell();
        foreach (Control target in new Control[] { dropPanel, dropTitle, dropSubtitle })
        {
            target.AllowDrop = true;
            target.DragEnter += AppDragEnter;
            target.DragLeave += AppDragLeave;
            target.DragDrop += AppDragDrop;
        }
        Directory.CreateDirectory(modsFolder);
        Directory.CreateDirectory(addonDownloads);
        ShowPage("Mods");
        LogAction("Application started");
        ApplyTheme();
        UpdateStatus(T("Ready.", "Hazır."));
    }

    static HttpClient CreateHttpClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("BedrockLoader/0.12 (Windows; .NET 8)");
        return http;
    }

    void BuildShell()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(2, 0, 2, 0), ColumnCount = 1, RowCount = 3, Tag = "shell" };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        Controls.Add(root);
        var header = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, Tag = "shell" };
        topHeader = header;
        void Drag(object? sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero); } }
        header.MouseDown += Drag;
        header.Paint += (_, e) => { using var pen = new Pen(lightMode ? Color.FromArgb(206, 216, 225) : Color.FromArgb(35, 49, 65)); e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1); };
        var logo = new SymbolButton { Symbol = "game", Width = 23, Height = 30, Location = new Point(0, 16), Tag = "icon" };
        logo.Click += (_, _) => MessageBox.Show(this, T("Update checking is not available yet.", "Güncelleme kontrolü henüz aktif değil."), "BedrockLoader", MessageBoxButtons.OK, MessageBoxIcon.Information);
        header.Controls.Add(logo);
        var brand = new Label { Text = "BedrockLoader", Font = new Font("Segoe UI", 12, FontStyle.Bold), AutoSize = true, Location = new Point(26, 11) };
        var version = new Label { Text = "v0.12", Font = new Font("Segoe UI", 10), AutoSize = true, Location = new Point(27, 35) };
        brand.MouseDown += Drag; version.MouseDown += Drag;
        header.Controls.Add(brand); header.Controls.Add(version);
        var settingsButton = new SymbolButton { Symbol = "settings", Width = 28, Height = 30, Tag = "icon", AccessibleName = T("Settings", "Ayarlar") };
        settingsButton.Click += (_, _) => ShowPage("Settings");
        var tips = new ToolTip(); tips.SetToolTip(settingsButton, T("Settings", "Ayarlar"));
        var traffic = new FlowLayoutPanel { Width = 66, Height = 30, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Tag = "traffic", Margin = Padding.Empty };
        void Traffic(string tip, Color color, Action action)
        {
            var button = new AnimatedRoundButton { Width = 16, Height = 16, CornerRadius = 8, BackColor = color, Margin = new Padding(3, 7, 3, 0), Cursor = Cursors.Hand, AccessibleName = tip };
            button.Click += (_, _) => action(); tips.SetToolTip(button, tip); traffic.Controls.Add(button);
        }
        Traffic(T("Minimize", "Küçült"), Color.FromArgb(145, 104, 0), () => ShowWindow(Handle, 6));
        Traffic(T("Maximize / restore", "Büyüt / geri al"), Color.FromArgb(22, 158, 47), () => ShowWindow(Handle, WindowState == FormWindowState.Maximized ? 9 : 3));
        Traffic(T("Close", "Kapat"), Color.FromArgb(176, 10, 6), Close);
        header.Controls.Add(settingsButton); header.Controls.Add(traffic);
        header.Resize += (_, _) => { traffic.Location = new Point(header.ClientSize.Width - 66, 16); settingsButton.Location = new Point(traffic.Left - 33, 16); };
        root.Controls.Add(header, 0, 0);
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(0, 20, 0, 20), Margin = Padding.Empty, Tag = "shell" };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210)); body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.Controls.Add(body, 0, 1);
        var nav = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, AutoScroll = false, Margin = new Padding(0, 0, 12, 0), Padding = new Padding(1, 0, 1, 0), Tag = "shell" };
        nav.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int row = 0; row < 5; row++) nav.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        nav.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        body.Controls.Add(nav, 0, 0);
        AddNav(nav, "Home", "", T("Home", "Ana ekran")); AddNav(nav, "Mods", "", T("Mods", "Modlar"));
        AddNav(nav, "Addons", "", T("Add-ons", "Eklentiler")); AddNav(nav, "Logs", "", T("Logs", "Kayıtlar")); AddNav(nav, "About", "", T("About", "Hakkında"));
        contentHost.Dock = DockStyle.Fill; contentHost.BackColor = Surface; contentHost.Tag = "page";
        contentHost.Margin = Padding.Empty; contentHost.Padding = new Padding(12, 13, 12, 12);
        MakeRounded(contentHost, 20); body.Controls.Add(contentHost, 1, 0);
        var footer = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, Tag = "shell" }; footerBar = footer;
        footer.Paint += (_, e) => { using var pen = new Pen(lightMode ? Color.FromArgb(206, 216, 225) : Color.FromArgb(35, 49, 65)); e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0); };
        status.Dock = DockStyle.Fill; status.Font = new Font("Segoe UI", 8); status.TextAlign = ContentAlignment.MiddleLeft; status.Padding = Padding.Empty;
        footer.Controls.Add(status);
        languageMenu.ShowImageMargin = false; languageMenu.ShowCheckMargin = false;
        languageMenu.Renderer = new SoftMenuRenderer(() => SurfaceAlt, () => TextColor, () => Accent);
        languageMenu.Padding = new Padding(5); languageMenu.Items.Clear(); languageMenu.Items.Add("Türkçe", null, (_, _) => SetLanguage("TR")); languageMenu.Items.Add("English", null, (_, _) => SetLanguage("EN"));
        var language = new SymbolButton { Symbol = "language", Width = 32, Dock = DockStyle.Right, Tag = "icon", AccessibleName = T("Language", "Dil") };
        language.Click += (_, _) => languageMenu.Show(language, new Point(0, 0), ToolStripDropDownDirection.AboveLeft);
        var light = new SymbolButton { Symbol = "moon", Width = 32, Dock = DockStyle.Right, Tag = "icon", AccessibleName = T("Toggle theme", "Temayı değiştir") };
        light.Click += (_, _) => { lightMode = !lightMode; settings.LightMode = lightMode; SaveSettings(); BuildShellRefresh(); };
        tips.SetToolTip(language, T("Language", "Dil")); tips.SetToolTip(light, T("Toggle theme", "Temayı değiştir"));
        footer.Controls.Add(language); footer.Controls.Add(light); root.Controls.Add(footer, 0, 2);
    }

    void SetLanguage(string code) { settings.Language = code; english = code == "EN"; SaveSettings(); BuildShellRefresh(); }

    void BuildShellRefresh()
    {
        navButtons.Clear(); pageHeadingPanel = null;
        Controls.Clear(); languageMenu.Items.Clear(); ApplyThemePalette(); ApplyLightPalette(); BuildShell(); ShowPage(currentPage); ApplyTheme();
        UpdateStatus(T("Ready.", "Hazır."));
    }

    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            // Keep the native resize frame and caption styles for DWM transitions;
            // WM_NCCALCSIZE below lets our custom UI occupy the entire window.
            cp.Style |= 0x00C00000 | 0x00040000 | 0x00020000 | 0x00010000 | 0x00080000;
            return cp;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MinMaxInfo
    {
        public Point Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x83 && m.WParam != IntPtr.Zero)
        {
            m.Result = IntPtr.Zero;
            return;
        }
        if (m.Msg == 0x24)
        {
            base.WndProc(ref m);
            var info = Marshal.PtrToStructure<MinMaxInfo>(m.LParam);
            var screen = Screen.FromHandle(Handle);
            info.MaxPosition = new Point(screen.WorkingArea.Left - screen.Bounds.Left, screen.WorkingArea.Top - screen.Bounds.Top);
            info.MaxSize = new Point(screen.WorkingArea.Width, screen.WorkingArea.Height);
            Marshal.StructureToPtr(info, m.LParam, false);
            return;
        }
        if (m.Msg == 0x84 && WindowState == FormWindowState.Normal)
        {
            base.WndProc(ref m);
            var point = PointToClient(new Point(unchecked((short)((long)m.LParam & 0xffff)), unchecked((short)(((long)m.LParam >> 16) & 0xffff))));
            int edge = Math.Max(8, (int)(8 * DeviceDpi / 96f));
            bool l = point.X < edge, r = point.X >= Width - edge, t = point.Y < edge, b = point.Y >= Height - edge;
            if (l && t) m.Result = (IntPtr)13;
            else if (r && t) m.Result = (IntPtr)14;
            else if (l && b) m.Result = (IntPtr)16;
            else if (r && b) m.Result = (IntPtr)17;
            else if (l) m.Result = (IntPtr)10;
            else if (r) m.Result = (IntPtr)11;
            else if (t) m.Result = (IntPtr)12;
            else if (b) m.Result = (IntPtr)15;
            return;
        }
        base.WndProc(ref m);
    }

    void AddNav(TableLayoutPanel host, string key, string glyph, string title)
    {
        var b = new NavIconButton { IconKey = key, Text = title, Font = new Font("Segoe UI", 9), CornerRadius = 12, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, BackColor = Bg, ForeColor = TextColor, Margin = new Padding(0, 0, 0, 7), Cursor = Cursors.Hand, Tag = key };
        b.Click += (_, _) => ShowPage(key); navButtons.Add(b); host.Controls.Add(b, 0, host.Controls.Count);
    }

    Button MakeButton(string text, Action action, int width = 130)
    {
        var b = new AnimatedRoundButton { Text = text, Width = width, Height = 38, FlatStyle = FlatStyle.Flat, BackColor = SurfaceAlt, ForeColor = TextColor, Cursor = Cursors.Hand, Margin = new Padding(0, 0, 8, 8), Font = new Font(PreferredFontName(), 10.5f) };
        b.FlatAppearance.BorderSize = 0;
        b.CornerRadius = 18;
        ApplyButtonPalette(b);
        b.Click += (_, _) => action();
        return b;
    }

    TextBox MakeTextBox(string value = "") => new() { Text = value, BackColor = SurfaceAlt, ForeColor = TextColor, BorderStyle = BorderStyle.FixedSingle, Font = new Font(PreferredFontName(), 10) };
    Label MakeLabel(string text, int size = 10, bool strong = false) => new() { Text = text, AutoSize = true, ForeColor = TextColor, Font = new Font(strong ? PreferredFontName() : "Segoe UI", size, strong ? FontStyle.Bold : FontStyle.Regular) };

    static string PreferredFontName()
    {
        try { using var fonts = new InstalledFontCollection(); if (fonts.Families.Any(f => f.Name.Equals("Torus", StringComparison.OrdinalIgnoreCase))) return "Torus"; } catch { }
        return "Segoe UI";
    }

    void MakeRounded(Control control, int radius)
    {
        void Apply() { if (control.Width < 2 || control.Height < 2) return; using var path = new GraphicsPath(); int d = Math.Min(radius * 2, Math.Min(control.Width, control.Height)); path.AddArc(0, 0, d, d, 180, 90); path.AddArc(control.Width - d, 0, d, d, 270, 90); path.AddArc(control.Width - d, control.Height - d, d, d, 0, 90); path.AddArc(0, control.Height - d, d, d, 90, 90); path.CloseFigure(); control.Region = new Region(path); }
        control.Resize += (_, _) => Apply(); Apply();
    }

    void ShowPage(string key)
    {
        currentPage = key;
        contentHost.SuspendLayout(); contentHost.Controls.Clear(); pageHeadingPanel = null;
        foreach (var b in navButtons)
        {
            ApplyNavigationColor(b, Equals(b.Tag, key));
            b.Text = NavText(key: (string)b.Tag!, glyph: "").Trim();
        }
        switch (key)
        {
            case "Home": BuildComfortableHome(); break;
            case "Mods": BuildModsPage(); break;
            case "Addons": BuildSoftAddons(); break;
            case "Settings": BuildSettingsPage(); break;
            case "Logs": BuildLogsPage(); break;
            default: BuildComfortableAbout(); break;
        }
        // Dock the heading before the fill panel so they never overlap.
        if (pageHeadingPanel != null && contentHost.Controls.Contains(pageHeadingPanel)) contentHost.Controls.SetChildIndex(pageHeadingPanel, contentHost.Controls.Count - 1);
        contentHost.ResumeLayout(true);
        ApplyTheme();
        ResetDropText();
    }

    string NavText(string key, string glyph)
    {
        string text = key switch { "Home" => T("Home", "Ana ekran"), "Mods" => T("Mods", "Modlar"), "Addons" => T("Add-ons", "Eklentiler"), "Settings" => T("Settings", "Ayarlar"), "Logs" => T("Logs", "Kayıtlar"), _ => T("About", "Hakkında") };
        return text;
    }

    void AddPageHeading(string title, string subtitle)
    {
        var block = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Surface, Tag = "page", Margin = Padding.Empty };
        pageHeadingPanel = block;
        pageTitle.Text = title; pageTitle.Font = new Font("Segoe UI", 14, FontStyle.Bold); pageTitle.AutoSize = true; pageTitle.Location = new Point(0, 0);
        pageSubtitle.Text = subtitle; pageSubtitle.Font = new Font("Segoe UI", 7); pageSubtitle.AutoSize = true; pageSubtitle.Location = new Point(0, 28);
        block.Controls.Add(pageTitle); block.Controls.Add(pageSubtitle); contentHost.Controls.Add(block); block.BringToFront();
    }

    void BuildModsPage()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty, Tag = "page" };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        var listPanel = new RoundedPanel { Dock = DockStyle.Fill, CornerRadius = 14, Padding = new Padding(13, 12, 13, 8), Margin = Padding.Empty, Tag = "card" };
        var listHeader = new Panel { Dock = DockStyle.Top, Height = 30, Tag = "card" };
        var listTitle = MakeLabel(T("Mod list", "Mod listesi"), 9, true); listTitle.Location = new Point(0, 4); listHeader.Controls.Add(listTitle);
        modCountLabel = MakeLabel("", 7); modCountLabel.Dock = DockStyle.Right; modCountLabel.AutoSize = false; modCountLabel.Width = 80; modCountLabel.TextAlign = ContentAlignment.TopRight; modCountLabel.Padding = new Padding(0, 6, 0, 0); listHeader.Controls.Add(modCountLabel);
        modList.Dock = DockStyle.Fill; modList.CheckOnClick = true; modList.IntegralHeight = false; modList.BorderStyle = BorderStyle.None;
        modList.Font = new Font("Segoe UI", 9); modList.ItemHeight = 34;
        modList.ItemCheck -= ModList_ItemCheck; modList.ItemCheck += ModList_ItemCheck;
        listPanel.Controls.Add(modList); listPanel.Controls.Add(listHeader);
        deleteModsButton = MakeButton(T("Delete checked", "Seçilenleri sil"), DeleteCheckedMods, 150); deleteModsButton.Height = 28; deleteModsButton.Visible = false; deleteModsButton.Dock = DockStyle.Bottom; listPanel.Controls.Add(deleteModsButton);
        layout.Controls.Add(listPanel, 0, 0);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 0), Margin = Padding.Empty, WrapContents = false, Tag = "page" };
        var add = MakeButton(T("Add mod", "Mod ekle"), ChooseModFiles, 60); add.Tag = "primary"; add.Height = 26; add.Font = new Font("Segoe UI", 7);
        var refresh = MakeButton(T("Refresh", "Yenile"), RefreshMods, 46); refresh.Height = 26; refresh.Font = new Font("Segoe UI", 7);
        buttons.Controls.Add(add); buttons.Controls.Add(refresh);
        var menu = new ContextMenuStrip(); menu.Items.Add(T("Open mods folder", "Mod klasörünü aç"), null, (_, _) => OpenMods()); modList.ContextMenuStrip = menu;
        layout.Controls.Add(buttons, 0, 1); contentHost.Controls.Add(layout);
        AddPageHeading(T("Mods", "Modlar"), T("Manage your installed mods.", "Yüklü modlarını yönet.")); RefreshMods();
    }

    void ModList_ItemCheck(object? sender, ItemCheckEventArgs e)
    {
        BeginInvoke(new Action(() => { if (deleteModsButton != null) deleteModsButton.Visible = modList.CheckedItems.Count > 0; }));
    }

    void DeleteCheckedMods()
    {
        var selected = modList.CheckedItems.Cast<string>().ToList(); if (selected.Count == 0) return;
        if (MessageBox.Show(this, T($"Delete {selected.Count} checked file(s)?", $"İşaretli {selected.Count} dosya silinsin mi?"), T("Confirm deletion", "Silme onayı"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        int removed = 0;
        foreach (var relative in selected) { var full = Path.GetFullPath(Path.Combine(modsFolder, relative)); if (full.StartsWith(Path.GetFullPath(modsFolder) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && File.Exists(full)) { File.Delete(full); removed++; } }
        LogAction($"Deleted {removed} checked mod file(s)"); RefreshMods(); UpdateStatus(T($"Deleted {removed} file(s).", $"{removed} dosya silindi."));
    }

    void AddonList_ItemCheck(object? sender, ItemCheckEventArgs e)
    {
        BeginInvoke(new Action(() => { if (deleteAddonsButton != null) deleteAddonsButton.Visible = addonList.CheckedItems.Count > 0; }));
    }

    void BuildStatsPage()
    {
        AddPageHeading(T("Minecraft statistics", "Minecraft istatistikleri"), T("Local pack stats and shortcuts. Xbox online achievements/playtime require Xbox account access.", "Yerel paket istatistikleri ve kısayollar. Çevrim içi başarımlar/oyun süresi için Xbox hesap erişimi gerekir."));
        var card = new Panel { Dock = DockStyle.Top, Height = 210, BackColor = Surface, Padding = new Padding(18) };
        int resources = CountFolders(Path.Combine(comMojangRoot, "resource_packs"));
        int behaviors = CountFolders(Path.Combine(comMojangRoot, "behavior_packs"));
        int stored = Directory.Exists(modsFolder) ? Directory.GetFiles(modsFolder, "*", SearchOption.AllDirectories).Length : 0;
        card.Controls.Add(new Label { Text = $"{T("Resource packs", "Kaynak paketleri")}: {resources}\n\n{T("Behavior packs", "Davranış paketleri")}: {behaviors}\n\n{T("Files in local mod folder", "Yerel mod klasöründeki dosyalar")}: {stored}", AutoSize = true, Font = new Font("Segoe UI Semibold", 12), ForeColor = TextColor, Location = new Point(18, 18) });
        var xbox = MakeButton(T("Open Xbox profile", "Xbox profilini aç"), OpenXboxProfile, 170); xbox.Location = new Point(18, 155); card.Controls.Add(xbox);
        contentHost.Controls.Add(card);
    }

    ComboBox MakeThemeCombo(int width)
    {
        var cb = new ComboBox { Dock = DockStyle.Left, Width = width, Height = 38, DropDownStyle = ComboBoxStyle.DropDownList,
            DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 30, BackColor = SurfaceAlt, ForeColor = TextColor,
            FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 11.5f) };
        cb.DrawItem += (_, e) => {
            e.DrawBackground();
            using var brush = new SolidBrush(SurfaceAlt);
            e.Graphics.FillRectangle(brush, e.Bounds);
            if (e.Index >= 0) TextRenderer.DrawText(e.Graphics, cb.Items[e.Index]?.ToString() ?? "", cb.Font,
                new Rectangle(e.Bounds.X + 9, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height), TextColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        };
        return cb;
    }

    void BuildSettingsPage()
    {
        AddPageHeading(T("Settings", "Ayarlar"), T("Choose the game path, language, and accent color.", "Oyun yolunu, dili ve vurgu rengini seç."));
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true }; contentHost.Controls.Add(scroll);
        var layout = new TableLayoutPanel { Dock = DockStyle.Top, Height = 332, MinimumSize = new Size(500, 332), ColumnCount = 2, RowCount = 4, Padding = new Padding(14), BackColor = Surface };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int row = 0; row < 3; row++) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        scroll.Controls.Add(layout);

        layout.Controls.Add(MakeLabel(T("Content folder", "Content klasörü"), 10, true), 0, 0);
        var pathRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));
        var path = MakeTextBox(settings.ContentPath); path.Dock = DockStyle.Fill;
        pathRow.Controls.Add(path, 0, 0);
        pathRow.Controls.Add(MakeButton(T("Browse", "Gözat"), () => { using var d = new FolderBrowserDialog { Description = T("Select the Minecraft Content folder", "Minecraft Content klasörünü seç"), SelectedPath = Directory.Exists(path.Text) ? path.Text : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) }; if (d.ShowDialog(this) == DialogResult.OK) path.Text = d.SelectedPath; }, 80), 1, 0);
        layout.Controls.Add(pathRow, 1, 0);

        layout.Controls.Add(MakeLabel(T("Language", "Dil"), 10, true), 0, 1);
        var lang = MakeThemeCombo(160);
        lang.Items.AddRange(new object[] { "Türkçe", "English" }); lang.SelectedIndex = english ? 1 : 0; layout.Controls.Add(lang, 1, 1);

        layout.Controls.Add(MakeLabel(T("Theme", "Tema"), 10, true), 0, 2);
        var theme = MakeThemeCombo(250);
        theme.Items.AddRange(new object[] { "Midnight", "Blue", "Purple", "Green", "Orange", "Red", "Material You" });
        theme.SelectedItem = settings.Theme; if (theme.SelectedIndex < 0) theme.SelectedIndex = 0;
        theme.SelectedIndexChanged += (_, _) => { settings.Theme = theme.SelectedItem?.ToString() ?? "Midnight"; ApplyThemePalette(); SaveSettings(); ApplyTheme(); };
        layout.Controls.Add(theme, 1, 2);

        layout.Controls.Add(MakeLabel(T("Game shortcuts", "Oyun kısayolları"), 10, true), 0, 3);
        var links = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true };
        links.Controls.Add(MakeButton(T("Launch Minecraft", "Minecraft'ı başlat"), LaunchGame, 155));
        links.Controls.Add(MakeButton(T("Xbox / Store", "Xbox / Store"), OpenXboxMinecraftPage, 120));
        var save = MakeButton(T("Save settings", "Ayarları kaydet"), () =>
        {
            if (string.IsNullOrWhiteSpace(path.Text)) { MessageBox.Show(this, T("Choose a folder first.", "Önce klasör seç.")); return; }
            settings.ContentPath = path.Text.Trim().Trim('"');
            settings.Language = lang.SelectedIndex == 1 ? "EN" : "TR";
            english = settings.Language == "EN";
            SaveSettings(); BuildShellRefresh(); UpdateStatus(T("Settings saved.", "Ayarlar kaydedildi."));
        }, 135);
        links.Controls.Add(save); layout.Controls.Add(links, 1, 3);


    }

    void BuildLogsPage()
    {
        AddPageHeading(T("Logs", "Kayıtlar"), T("Actions performed by BedrockLoader are recorded here.", "BedrockLoader ile yapılan işlemler burada kaydedilir."));
        var log = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = true, BackColor = Surface, ForeColor = TextColor, BorderStyle = BorderStyle.None, Font = new Font("Consolas", 10), Text = ReadLogs() };
        contentHost.Controls.Add(log);
        var refresh = MakeButton(T("Refresh logs", "Kayıtları yenile"), () => { log.Text = ReadLogs(); LogAction("Viewed logs"); }, 120); refresh.Dock = DockStyle.Bottom; contentHost.Controls.Add(refresh); if (pageHeadingPanel != null) { contentHost.Controls.SetChildIndex(pageHeadingPanel, 0); pageHeadingPanel.BringToFront(); }
    }

    void LogAction(string action)
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(logFile)!); File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {action}{Environment.NewLine}"); } catch { }
    }

    string ReadLogs()
    {
        try { return File.Exists(logFile) ? File.ReadAllText(logFile) : T("No actions recorded yet.", "Henüz işlem kaydı yok."); }
        catch { return T("Could not read logs.", "Kayıtlar okunamadı."); }
    }

    void ConfigureDropPanel(bool addonMode)
    {
        dropPanel.Dock = DockStyle.Top;
        dropPanel.Height = addonMode ? 66 : 104;
        dropPanel.BackColor = Surface;
        dropPanel.BorderStyle = BorderStyle.FixedSingle;
        dropPanel.Controls.Clear();
        dropTitle.Text = addonMode ? T("Drop add-on files here", "Eklenti dosyalarını buraya bırak") : T("Drop files here", "Dosyaları buraya bırak");
        dropTitle.Font = new Font("Segoe UI Semibold", 11);
        dropTitle.ForeColor = TextColor;
        dropTitle.AutoSize = true;
        dropTitle.Location = new Point(18, addonMode ? 9 : 20);
        dropSubtitle.Text = addonMode ? T(".mcpack, .mcaddon or compatible .zip files will be installed into Minecraft AppData.", ".mcpack, .mcaddon veya uyumlu .zip dosyaları Minecraft AppData'ya yüklenir.") : T("Files will be copied to the app's local mods folder.", "Dosyalar uygulamanın yerel mods klasörüne kopyalanır.");
        dropSubtitle.Font = new Font("Segoe UI", 8.5f);
        dropSubtitle.ForeColor = Muted;
        dropSubtitle.AutoSize = true;
        dropSubtitle.Location = new Point(20, addonMode ? 34 : 51);
        dropPanel.Controls.Add(dropTitle);
        dropPanel.Controls.Add(dropSubtitle);
    }

    void AppDragEnter(object? sender, DragEventArgs e)
    {
        bool valid = e.Data?.GetDataPresent(DataFormats.FileDrop) == true;
        e.Effect = valid ? DragDropEffects.Copy : DragDropEffects.None;
        if (!valid) return;
        isDragging = true;
        dropTitle.Text = currentPage == "Addons" ? T("Release to install pack(s)", "Paketleri yüklemek için bırak") : T("Release to add mod files", "Mod dosyalarını eklemek için bırak");
        dropSubtitle.Text = currentPage == "Addons" ? T("Files will be installed into Minecraft AppData.", "Dosyalar Minecraft AppData'ya yüklenir.") : T("Release to confirm adding the selected files to the local mods folder.", "Seçili dosyaları yerel mod klasörüne eklemek için bırak.");
        dropPanel.BackColor = MixColors(SurfaceAlt, Accent, lightMode ? 0.18 : 0.24);
    }

    void AppDragLeave(object? sender, EventArgs e) { if (isDragging) ResetDropText(); }
    void AppDragDrop(object? sender, DragEventArgs e)
    {
        ResetDropText();
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] files) return;
        if (currentPage == "Addons") InstallAddonFiles(files);
        else CopyMods(files);
    }

    void ResetDropText()
    {
        isDragging = false;
        if (dropPanel.IsDisposed) return;
        dropPanel.BackColor = Surface;
        dropTitle.Text = currentPage == "Addons" ? T("Drop add-on files here", "Eklenti dosyalarını buraya bırak") : T("Drop files here", "Dosyaları buraya bırak");
        dropSubtitle.Text = currentPage == "Addons" ? T("Drop a .mcpack, .mcaddon or .zip package here.", ".mcpack, .mcaddon veya .zip paketini buraya bırak.") : T("Files will be copied to the app's local mods folder.", "Dosyalar uygulamanın yerel mods klasörüne kopyalanır.");
    }

    void ChooseModFiles()
    {
        using var d = new OpenFileDialog { Title = T("Select mod files", "Mod dosyalarını seç"), Filter = T("All files (*.*)|*.*", "Tüm dosyalar (*.*)|*.*"), Multiselect = true };
        if (d.ShowDialog(this) == DialogResult.OK) CopyMods(d.FileNames);
    }

    void CopyMods(IEnumerable<string> paths)
    {
        try
        {
            Directory.CreateDirectory(modsFolder); int count = 0;
            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    var dest = Path.Combine(modsFolder, Path.GetFileName(path));
                    if (File.Exists(dest) && MessageBox.Show(this, T($"{Path.GetFileName(path)} already exists. Overwrite?", $"{Path.GetFileName(path)} zaten var. Üzerine yazılsın mı?"), T("Confirm", "Onayla"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) continue;
                    if (Path.GetFullPath(path).Equals(Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase)) continue;
                    File.Copy(path, dest, true); count++;
                }
                else if (Directory.Exists(path))
                {
                    foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                    {
                        var relative = Path.GetRelativePath(path, file); var dest = Path.GetFullPath(Path.Combine(modsFolder, relative));
                        if (!dest.StartsWith(Path.GetFullPath(modsFolder) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
                        Directory.CreateDirectory(Path.GetDirectoryName(dest)!); if (!File.Exists(dest)) { File.Copy(file, dest); count++; }
                    }
                }
            }
            RefreshMods(); LogAction($"Installed {count} mod file(s) into {modsFolder}"); UpdateStatus(T($"Copied {count} file(s) to local mods folder.", $"{count} dosya yerel mods klasörüne kopyalandı."));
        }
        catch (Exception ex) { ShowError(ex, T("File copy failed", "Dosya kopyalanamadı")); }
    }

    void RefreshMods()
    {
        Directory.CreateDirectory(modsFolder); modList.BeginUpdate(); modList.Items.Clear();
        foreach (var f in Directory.GetFiles(modsFolder, "*", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.OrdinalIgnoreCase)) modList.Items.Add(Path.GetRelativePath(modsFolder, f), false);
        modList.EndUpdate(); if (modCountLabel != null) modCountLabel.Text = $"{modList.Items.Count} {T("mods", "mod")}"; if (deleteModsButton != null) deleteModsButton.Visible = false;
    }

    void OpenMods() { Directory.CreateDirectory(modsFolder); LogAction("Opened local mods folder"); OpenPath(modsFolder); }

    void OpenContentFolder()
    {
        if (!Directory.Exists(settings.ContentPath))
        {
            var answer = MessageBox.Show(this, T("The configured Content folder does not exist. Open the settings page to choose it?", "Seçili Content klasörü bulunamadı. Ayarlardan seçmek ister misin?"), T("Content folder not found", "Content klasörü bulunamadı"), MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (answer == DialogResult.Yes) ShowPage("Settings");
            return;
        }
        LogAction("Opened Minecraft Content folder"); OpenPath(settings.ContentPath);
    }

    void ChooseAndInstallAddon()
    {
        using var d = new OpenFileDialog { Title = T("Choose a Bedrock add-on pack", "Bedrock eklenti paketi seç"), Filter = "Bedrock packs (*.mcpack;*.mcaddon;*.zip)|*.mcpack;*.mcaddon;*.zip|All files (*.*)|*.*", Multiselect = true };
        if (d.ShowDialog(this) == DialogResult.OK) InstallAddonFiles(d.FileNames);
    }

    async void DownloadAndInstallAddon(string urlText)
    {
        if (!Uri.TryCreate(urlText.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            MessageBox.Show(this, T("Paste a direct HTTPS/HTTP file URL, not a project page.", "Proje sayfası değil, doğrudan HTTPS/HTTP dosya bağlantısı yapıştır.")); return;
        }
        try
        {
            Directory.CreateDirectory(addonDownloads);
            using var response = await Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long length && length > 250L * 1024 * 1024) throw new InvalidDataException(T("Download exceeds the 250 MB safety limit.", "İndirme 250 MB güvenlik sınırını aşıyor."));
            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? Path.GetFileName(uri.LocalPath);
            fileName = SanitizeName(Uri.UnescapeDataString(fileName));
            if (string.IsNullOrWhiteSpace(fileName) || !IsSupportedPackage(fileName)) throw new InvalidDataException(T("The URL did not return a .mcpack, .mcaddon, or .zip file.", "Bağlantı .mcpack, .mcaddon veya .zip dosyası döndürmedi."));
            var dest = UniquePath(addonDownloads, fileName);
            await using (var input = await response.Content.ReadAsStreamAsync())
            await using (var output = new FileStream(dest, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                var buffer = new byte[81920]; long total = 0; int read;
                while ((read = await input.ReadAsync(buffer)) > 0)
                {
                    total += read; if (total > 250L * 1024 * 1024) throw new InvalidDataException(T("Download exceeds the 250 MB safety limit.", "İndirme 250 MB güvenlik sınırını aşıyor."));
                    await output.WriteAsync(buffer.AsMemory(0, read));
                }
            }
            InstallAddonFiles(new[] { dest });
        }
        catch (Exception ex) { ShowError(ex, T("Add-on download failed", "Eklenti indirilemedi")); }
    }

    void InstallAddonFiles(IEnumerable<string> paths)
    {
        try
        {
            Directory.CreateDirectory(addonDownloads);
            int packCount = 0; var errors = new List<string>();
            foreach (var source in paths)
            {
                if (!File.Exists(source)) { errors.Add(Path.GetFileName(source) + ": " + T("not a file", "dosya değil")); continue; }
                var ext = Path.GetExtension(source);
                if (!IsSupportedPackage(source)) { errors.Add(Path.GetFileName(source) + ": " + T("unsupported file type", "desteklenmeyen dosya türü")); continue; }
                string staged = source;
                if (!Path.GetFullPath(source).StartsWith(Path.GetFullPath(addonDownloads) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && !Path.GetFullPath(source).Equals(Path.GetFullPath(addonDownloads), StringComparison.OrdinalIgnoreCase))
                {
                    staged = UniquePath(addonDownloads, Path.GetFileName(source)); File.Copy(source, staged, false);
                }
                if (ext.Equals(".mcworld", StringComparison.OrdinalIgnoreCase)) { errors.Add(Path.GetFileName(source) + ": " + T("world files are saved but not installed as packs", "dünya dosyası kaydedildi ancak paket olarak yüklenmedi")); continue; }
                packCount += InstallPackageArchive(staged, 0);
            }
            RefreshAddonList();
            LogAction($"Installed {packCount} add-on pack(s) from {paths.Count()} source file(s)");
            UpdateStatus(T($"Installed {packCount} pack(s). Enable them in Minecraft world settings.", $"{packCount} paket yüklendi. Minecraft dünya ayarlarından etkinleştir."));
            if (errors.Count > 0) MessageBox.Show(this, string.Join(Environment.NewLine, errors), T("Some files were skipped", "Bazı dosyalar atlandı"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex) { ShowError(ex, T("Add-on installation failed", "Eklenti yüklenemedi")); }
    }

    int InstallPackageArchive(string archivePath, int depth)
    {
        if (depth > 4) throw new InvalidDataException(T("Nested archives are too deep.", "İç içe arşiv sayısı çok fazla."));
        if (new FileInfo(archivePath).Length > 250L * 1024 * 1024) throw new InvalidDataException(T("Package exceeds the 250 MB safety limit.", "Paket 250 MB güvenlik sınırını aşıyor."));
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count > 25000) throw new InvalidDataException(T("Archive contains too many entries.", "Arşivde çok fazla dosya var."));
        var nested = archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name) && (e.Name.EndsWith(".mcpack", StringComparison.OrdinalIgnoreCase) || e.Name.EndsWith(".mcaddon", StringComparison.OrdinalIgnoreCase))).ToList();
        if (nested.Count > 0)
        {
            int count = 0; var tempRoot = Path.Combine(addonDownloads, "_extract_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(tempRoot);
            try
            {
                foreach (var entry in nested)
                {
                    var temp = Path.Combine(tempRoot, SanitizeName(Path.GetFileName(entry.FullName)));
                    using var input = entry.Open(); using var output = File.Create(temp); input.CopyTo(output);
                    count += InstallPackageArchive(temp, depth + 1);
                }
            }
            finally { try { Directory.Delete(tempRoot, true); } catch { } }
            if (count > 0) return count;
        }

        var manifests = archive.Entries.Where(e => e.Name.Equals("manifest.json", StringComparison.OrdinalIgnoreCase)).ToList();
        if (manifests.Count == 0) throw new InvalidDataException(T("No manifest.json was found. This may not be a Bedrock pack ZIP.", "manifest.json bulunamadı. Bu ZIP, Bedrock paketi olmayabilir."));
        int installed = 0;
        foreach (var manifest in manifests)
        {
            string prefix = manifest.FullName[..(manifest.FullName.Length - manifest.Name.Length)];
            string destFolder = ClassifyPackFolder(manifest, archivePath);
            string packName = GetPackName(manifest, archivePath, prefix);
            var dest = UniquePath(destFolder, SanitizeName(packName));
            Directory.CreateDirectory(dest);
            int copied = 0;
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;
                if (!entry.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                var relative = entry.FullName[prefix.Length..].Replace('/', Path.DirectorySeparatorChar);
                if (string.IsNullOrWhiteSpace(relative)) continue;
                var target = Path.GetFullPath(Path.Combine(dest, relative));
                if (!target.StartsWith(Path.GetFullPath(dest) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                using var input = entry.Open(); using var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None); input.CopyTo(output); copied++;
            }
            if (copied == 0) { try { Directory.Delete(dest, true); } catch { } continue; }
            installed++;
        }
        if (installed == 0) throw new InvalidDataException(T("Could not install any valid pack from this archive.", "Bu arşivden geçerli paket yüklenemedi."));
        return installed;
    }

    string ClassifyPackFolder(ZipArchiveEntry manifestEntry, string archivePath)
    {
        try
        {
            using var stream = manifestEntry.Open(); using var doc = JsonDocument.Parse(stream);
            if (doc.RootElement.TryGetProperty("modules", out var modules) && modules.ValueKind == JsonValueKind.Array)
            {
                foreach (var module in modules.EnumerateArray())
                {
                    if (!module.TryGetProperty("type", out var type)) continue;
                    var t = type.GetString();
                    if (t == "resources") return Path.Combine(comMojangRoot, "resource_packs");
                    if (t == "data" || t == "script") return Path.Combine(comMojangRoot, "behavior_packs");
                    if (t == "skin_pack") return Path.Combine(comMojangRoot, "skin_packs");
                    if (t == "world_template") return Path.Combine(comMojangRoot, "world_templates");
                }
            }
        }
        catch { }
        return Path.Combine(comMojangRoot, "resource_packs");
    }

    string GetPackName(ZipArchiveEntry manifestEntry, string archivePath, string prefix)
    {
        try
        {
            using var stream = manifestEntry.Open(); using var doc = JsonDocument.Parse(stream);
            if (doc.RootElement.TryGetProperty("header", out var header) && header.TryGetProperty("name", out var name))
            {
                var value = name.GetString(); if (!string.IsNullOrWhiteSpace(value)) return value;
            }
        }
        catch { }
        var folderName = prefix.TrimEnd('/').Split('/').LastOrDefault();
        return string.IsNullOrWhiteSpace(folderName) ? Path.GetFileNameWithoutExtension(archivePath) : folderName;
    }

    void RefreshAddonList()
    {
        addonList.Items.Clear();
        foreach (var folder in new[] { "resource_packs", "behavior_packs", "skin_packs", "world_templates" })
        {
            var dir = Path.Combine(comMojangRoot, folder);
            if (!Directory.Exists(dir)) continue;
            foreach (var item in Directory.GetDirectories(dir).OrderBy(x => x, StringComparer.OrdinalIgnoreCase)) addonList.Items.Add(folder + "  /  " + Path.GetFileName(item), false);
        if (deleteAddonsButton != null) deleteAddonsButton.Visible = false;
        }
    }

    void RemoveSelectedAddon()
    {
        var selected = addonList.CheckedItems.Cast<string>().ToList(); if (selected.Count == 0) return;
        if (MessageBox.Show(this, T($"Delete {selected.Count} checked pack(s)?", $"İşaretli {selected.Count} eklenti silinsin mi?"), T("Confirm deletion", "Silme onayı"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        int removed = 0;
        foreach (var item in selected)
        {
            var parts = item.Split(new[] { "  /  " }, 2, StringSplitOptions.None); if (parts.Length != 2) continue;
            var target = Path.GetFullPath(Path.Combine(comMojangRoot, parts[0], parts[1]));
            if (target.StartsWith(Path.GetFullPath(comMojangRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && Directory.Exists(target)) { Directory.Delete(target, true); removed++; LogAction($"Deleted add-on pack: {parts[1]}"); }
        }
        RefreshAddonList(); UpdateStatus(T($"Deleted {removed} pack(s).", $"{removed} eklenti silindi."));
    }

    void OpenAddonFolder() { Directory.CreateDirectory(comMojangRoot); LogAction("Opened Minecraft add-on folder"); OpenPath(comMojangRoot); }

    void ShowXboxStats()
    {
        int resources = CountFolders(Path.Combine(comMojangRoot, "resource_packs"));
        int behaviors = CountFolders(Path.Combine(comMojangRoot, "behavior_packs"));
        var message = T($"Local Minecraft packs\nResource packs: {resources}\nBehavior packs: {behaviors}\n\nXbox online profile, achievements and play time are not read by this app. Open Xbox to view account details.", $"Yerel Minecraft paketleri\nKaynak paketleri: {resources}\nDavranış paketleri: {behaviors}\n\nBu uygulama Xbox çevrim içi profilini, başarımları veya oyun süresini okuyamaz. Hesap bilgileri için Xbox'ı aç.");
        var result = MessageBox.Show(this, message + "\n\n" + T("Open Xbox app?", "Xbox uygulaması açılsın mı?"), T("Minecraft profile & stats", "Minecraft profili ve istatistikleri"), MessageBoxButtons.YesNo, MessageBoxIcon.Information);
        if (result == DialogResult.Yes) OpenXboxProfile();
    }

    void OpenXboxProfile()
    {
        try { Process.Start(new ProcessStartInfo { FileName = "xbox:", UseShellExecute = true }); LogAction("Opened Xbox app/profile shortcut"); }
        catch { OpenUrl("https://www.xbox.com/profile"); LogAction("Opened Xbox profile website fallback"); }
    }

    void LaunchGame()
    {
        try { Process.Start(new ProcessStartInfo { FileName = "minecraft:", UseShellExecute = true }); }
        catch
        {
            try { Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = @"shell:AppsFolder\Microsoft.MinecraftUWP_8wekyb3d8bbwe!App", UseShellExecute = true }); }
            catch { MessageBox.Show(this, T("Minecraft for Windows could not be launched. Check if it's installed.", "Minecraft for Windows açılamadı. Oyunun kurulu olup olmadığını kontrol et."), T("Launch failed", "Oyun açılamadı"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
    }

    void OpenXboxMinecraftPage()
    {
        try { Process.Start(new ProcessStartInfo { FileName = "ms-windows-store://pdp/?productid=9NBLGGH2JHXJ", UseShellExecute = true }); LogAction("Opened Microsoft Store product page"); } catch { OpenUrl("ms-windows-store://pdp/?productid=9NBLGGH2JHXJ"); }
    }

    void ApplyThemePalette()
    {
        TextColor = Color.FromArgb(214, 216, 220); Muted = Color.FromArgb(174, 184, 194);
        if (settings.Theme == "Material You")
        {
            Bg = Color.FromArgb(20, 18, 28); Surface = Color.FromArgb(34, 30, 45); SurfaceAlt = Color.FromArgb(53, 46, 68);
        }
        else
        {
            Bg = Color.FromArgb(8, 15, 23); Surface = Color.FromArgb(14, 23, 35); SurfaceAlt = Color.FromArgb(22, 35, 48);
        }
        Accent = settings.Theme switch
        {
            "Blue" => Color.FromArgb(50, 190, 240),
            "Purple" => Color.FromArgb(184, 126, 255),
            "Green" => Color.FromArgb(74, 210, 150),
            "Orange" => Color.FromArgb(255, 165, 80),
            "Red" => Color.FromArgb(255, 95, 105),
            "Material You" => Color.FromArgb(190, 170, 255),
            _ => Color.FromArgb(50, 190, 240)
        };
    }

    static Color MixColors(Color background, Color tint, double amount) => Color.FromArgb(
        (int)Math.Round(background.R + (tint.R - background.R) * amount),
        (int)Math.Round(background.G + (tint.G - background.G) * amount),
        (int)Math.Round(background.B + (tint.B - background.B) * amount));

    static Color ReadableText(Color background)
    {
        static double Linear(byte channel) { double value = channel / 255d; return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4); }
        double luminance = 0.2126 * Linear(background.R) + 0.7152 * Linear(background.G) + 0.0722 * Linear(background.B);
        return (luminance + 0.05) / 0.05 >= 1.05 / (luminance + 0.05) ? Color.FromArgb(12, 16, 25) : Color.White;
    }

    void ApplyButtonPalette(AnimatedRoundButton button)
    {
        button.FocusBorderColor = Accent;
        if (button.Text.Contains("Delete checked", StringComparison.OrdinalIgnoreCase) || button.Text.Contains("Seçilenleri sil", StringComparison.OrdinalIgnoreCase))
        {
            button.BackColor = Color.FromArgb(220, 55, 65);
            button.ForeColor = Color.White;
        }
        else if (button.Text.Contains("Minecraft'ı başlat", StringComparison.OrdinalIgnoreCase) || button.Text.Contains("Launch Minecraft", StringComparison.OrdinalIgnoreCase))
        {
            // Keep the prominent launch button from the original design.
            button.BackColor = Accent;
            button.ForeColor = Color.FromArgb(8, 16, 26);
        }
        else
        {
            if (Equals(button.Tag, "primary"))
            {
                // This was a fixed blue; use a darker shade of the current accent.
                button.BackColor = MixColors(SurfaceAlt, Accent, 0.55);
                button.ForeColor = ReadableText(button.BackColor);
            }
            else
            {
                button.BackColor = SurfaceAlt;
                button.ForeColor = TextColor;
            }
        }
    }

    void ApplyNavigationColor(Button button, bool selected)
    {
        double tint = lightMode ? 0.16 : 0.20;
        button.BackColor = selected ? Color.FromArgb(
            (int)(Bg.R + (Accent.R - Bg.R) * tint),
            (int)(Bg.G + (Accent.G - Bg.G) * tint),
            (int)(Bg.B + (Accent.B - Bg.B) * tint)) : Bg;
        button.ForeColor = TextColor;
        if (button is NavIconButton nav)
        {
            nav.IconColor = selected ? (lightMode ? ControlPaint.Dark(Accent, 0.35f) : Accent) : TextColor;
            nav.FocusBorderColor = Accent;
        }
        button.Invalidate();
    }

    void ApplyLightPalette()
    {
        if (!lightMode) return;
        Bg = Color.FromArgb(242, 244, 250);
        Surface = Color.FromArgb(255, 255, 255);
        SurfaceAlt = Color.FromArgb(226, 231, 241);
        TextColor = Color.FromArgb(27, 33, 47);
        Muted = Color.FromArgb(90, 99, 116);
    }

    void ApplyTheme()
    {
        ApplyThemePalette(); ApplyLightPalette();
        BackColor = Bg; ForeColor = TextColor;
        ApplyDwmTheme();
        RecolorTree(this);
        languageMenu.BackColor = SurfaceAlt; languageMenu.ForeColor = TextColor;
        foreach (var b in navButtons)
        {
            ApplyNavigationColor(b, Equals(b.Tag, currentPage));
        }
        if (status is not null) { status.BackColor = Bg; status.ForeColor = TextColor; }
    }

    void ApplyDwmTheme()
    {
        if (!IsHandleCreated) return;
        try
        {
            int dark = !lightMode ? 1 : 0;
            _ = DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
            _ = DwmSetWindowAttribute(Handle, 19, ref dark, sizeof(int));
            int corners = 2;
            _ = DwmSetWindowAttribute(Handle, 33, ref corners, sizeof(int));

        }
        catch { }
    }

    void RecolorTree(Control root)
    {
        foreach (Control c in root.Controls)
        {
            if (c is Panel || c is TableLayoutPanel || c is FlowLayoutPanel) { if (Equals(c.Tag, "shell") || Equals(c.Tag, "traffic")) c.BackColor = Bg; else if (Equals(c.Tag, "card") || c is RoundedPanel) c.BackColor = SurfaceAlt; else c.BackColor = Surface; }
            if (c is TextBox tb) { tb.BackColor = SurfaceAlt; tb.ForeColor = TextColor; }
            if (c is ListBox lb) { lb.BackColor = SurfaceAlt; lb.ForeColor = TextColor; }
            if (c is SpacedCheckedListBox checks) checks.CheckAccent = Accent;
            if (c is ComboBox cb) { cb.BackColor = SurfaceAlt; cb.ForeColor = TextColor; }
            if (c is Label label) { label.ForeColor = label.Font.Size <= 9 ? Muted : TextColor; }
            if (c is Button button)
            {
                if (button is SymbolButton symbol) { button.BackColor = Bg; button.ForeColor = symbol.Symbol == "game" ? Accent : TextColor; symbol.FocusBorderColor = Accent; continue; }
                if (c.Parent?.Tag is string trafficTag && trafficTag == "traffic") continue;
                if (button.Tag is string key && navButtons.Contains(button)) continue;
                if (button is AnimatedRoundButton animated) ApplyButtonPalette(animated);
            }
            RecolorTree(c);
        }
    }

    AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(settingsFile))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(settingsFile)) ?? new AppSettings();
                if (!new[] { "Midnight", "Blue", "Purple", "Green", "Orange", "Red", "Material You" }.Contains(loaded.Theme)) loaded.Theme = "Blue";
                return loaded;
            }
        }
        catch { }
        return new AppSettings();
    }

    void SaveSettings()
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(settingsFile)!); File.WriteAllText(settingsFile, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true })); }
        catch (Exception ex) { ShowError(ex, T("Settings save error", "Ayar kaydetme hatası")); }
    }

    static string ResolveComMojangRoot()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var current = Path.Combine(appData, "Minecraft Bedrock", "Users", "Shared", "games", "com.mojang");
        var old = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", "Microsoft.MinecraftUWP_8wekyb3d8bbwe", "LocalState", "games", "com.mojang");
        if (Directory.Exists(current)) return current;
        if (Directory.Exists(old)) return old;
        return current;
    }

    static int CountFolders(string path) => Directory.Exists(path) ? Directory.GetDirectories(path).Length : 0;
    static bool IsSupportedPackage(string path) { var ext = Path.GetExtension(path); return ext.Equals(".mcpack", StringComparison.OrdinalIgnoreCase) || ext.Equals(".mcaddon", StringComparison.OrdinalIgnoreCase) || ext.Equals(".zip", StringComparison.OrdinalIgnoreCase) || ext.Equals(".mcworld", StringComparison.OrdinalIgnoreCase); }
    static string SanitizeName(string name)
    {
        name = Path.GetFileName(name).Trim(); foreach (char invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
        name = name.Trim('.', ' '); return string.IsNullOrWhiteSpace(name) ? "BedrockPack" : name;
    }
    static string UniquePath(string folder, string name)
    {
        Directory.CreateDirectory(folder); var safe = SanitizeName(name); var candidate = Path.Combine(folder, safe); int i = 2;
        while (Directory.Exists(candidate) || File.Exists(candidate)) candidate = Path.Combine(folder, Path.GetFileNameWithoutExtension(safe) + "_" + i++ + Path.GetExtension(safe));
        return candidate;
    }
    void OpenPath(string path) { try { Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true }); } catch (Exception ex) { ShowError(ex, T("Could not open folder", "Klasör açılamadı")); } }
    void OpenUrl(string url) { try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); } catch (Exception ex) { ShowError(ex, T("Could not open link", "Bağlantı açılamadı")); } }
    void UpdateStatus(string message) => status.Text = "☑  " + message;
    void ShowError(Exception ex, string title) => MessageBox.Show(this, ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
    string T(string en, string tr) => english ? en : tr;

    sealed class SymbolButton : AnimatedRoundButton
    {
        public string Symbol { get; set; } = "settings";
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            ApplyContentMotion(g);
            g.TranslateTransform(Width / 2f - 9, Height / 2f - 9);
            using var pen = new Pen(ForeColor, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            if (Symbol == "game")
            {
                g.DrawPolygon(pen, new[] { new Point(3,4), new Point(15,4), new Point(18,14), new Point(14,14), new Point(11,11), new Point(7,11), new Point(4,14), new Point(0,14) });
                g.DrawLine(pen,4,7,4,11); g.DrawLine(pen,2,9,6,9); g.DrawEllipse(pen,12,7,1,1); g.DrawEllipse(pen,14,10,1,1);
            }
            else if (Symbol == "moon")
            {
                g.DrawArc(pen,1,1,15,15,65,270); g.DrawArc(pen,6,-3,13,15,75,160);
            }
            else if (Symbol == "language")
            {
                using var font = new Font("Segoe UI", 9, FontStyle.Bold);
                using var brush = new SolidBrush(ForeColor);
                g.DrawString("文", font, brush, -1,0); g.DrawString("A", font, brush, 8,7);
            }
            else
            {
                var points = Enumerable.Range(0,24).Select(i => { double angle = i * Math.PI / 12; float r = i % 4 < 2 ? 8 : 6; return new PointF(9 + (float)Math.Cos(angle) * r, 9 + (float)Math.Sin(angle) * r); }).ToArray();
                g.DrawPolygon(pen, points); g.DrawEllipse(pen,6,6,6,6);
            }
        }
    }

    sealed class SpacedCheckedListBox : CheckedListBox
    {
        public Color CheckAccent { get; set; } = Color.FromArgb(50, 190, 240);
        public override int ItemHeight { get => 34; set { } }
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            using var background = new SolidBrush(BackColor);
            e.Graphics.FillRectangle(background, e.Bounds);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var box = new Rectangle(e.Bounds.X + 1, e.Bounds.Y + (e.Bounds.Height - 12) / 2, 12, 12);
            using var path = RoundedPath(box, 2);
            using var pen = new Pen(Color.FromArgb(157, 176, 190));
            e.Graphics.DrawPath(pen, path);
            if (GetItemChecked(e.Index))
            {
                using var brush = new SolidBrush(CheckAccent); e.Graphics.FillPath(brush, path);
                using var tick = new Pen(ReadableText(CheckAccent), 1.5f); e.Graphics.DrawLines(tick, new[] { new Point(box.X+3,box.Y+6), new Point(box.X+5,box.Y+9), new Point(box.X+10,box.Y+3) });
            }
            TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, new Rectangle(e.Bounds.X+25,e.Bounds.Y,e.Bounds.Width-25,e.Bounds.Height), ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            if ((e.State & DrawItemState.Focus) != 0 && Focused) ControlPaint.DrawFocusRectangle(e.Graphics, e.Bounds, ForeColor, BackColor);
        }
    }

    sealed class NavIconButton : AnimatedRoundButton
    {
        public string IconKey { get; set; } = "Home";
        Color iconColor = Color.LightGray, paintedIcon = Color.LightGray;
        public Color IconColor
        {
            get => iconColor;
            set { iconColor = value; if (!IsHandleCreated) paintedIcon = value; else Animate(); }
        }
        protected override bool AdvanceExtraMotion(double factor)
        {
            paintedIcon = Blend(paintedIcon, iconColor, factor);
            return paintedIcon.ToArgb() != iconColor.ToArgb();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            ApplyContentMotion(e.Graphics);
            using var pen = new Pen(paintedIcon, 2.1f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            var g = e.Graphics;
            e.Graphics.TranslateTransform(8, Height / 2f - 8); e.Graphics.ScaleTransform(0.7f, 0.7f);
            var x = 0; var y = 0;
            if (IconKey == "Home") { g.DrawLines(pen, new[] { new Point(x,y+11),new Point(x+11,y+2),new Point(x+22,y+11) }); g.DrawLines(pen,new[]{new Point(x+3,y+10),new Point(x+3,y+22),new Point(x+19,y+22),new Point(x+19,y+10)}); }
            else if (IconKey == "Mods") { g.DrawPolygon(pen,new[]{new Point(x+11,y+1),new Point(x+21,y+6),new Point(x+21,y+18),new Point(x+11,y+23),new Point(x+1,y+18),new Point(x+1,y+6)}); }
            else if (IconKey == "Addons") { g.DrawRectangle(pen,x+2,y+2,18,18);g.DrawLine(pen,x+11,y+2,x+11,y+20);g.DrawLine(pen,x+2,y+11,x+20,y+11); }
            else if (IconKey == "Logs") { g.DrawRectangle(pen,x+3,y+1,17,22);for(int i=0;i<3;i++)g.DrawLine(pen,x+7,y+7+i*5,x+17,y+7+i*5); }
            else { g.DrawEllipse(pen,x+2,y+2,19,19);g.DrawLine(pen,x+11,y+10,x+11,y+17);g.DrawEllipse(pen,x+10,y+6,2,2); }
        }
    }

    sealed class RoundedPanel : Panel
    {
        public int CornerRadius { get; set; } = 18;
        public RoundedPanel() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
        protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(Parent?.BackColor ?? BackColor); }
        protected override void OnPaint(PaintEventArgs e) { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality; using var path = RoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), CornerRadius); using var brush = new SolidBrush(BackColor); e.Graphics.FillPath(brush, path); }
    }

    static GraphicsPath RoundedPath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath(); int d = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (d < 2) { path.AddRectangle(bounds); return path; }
        path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90); path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90); path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90); path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
    }

    sealed class AppSettings
    {
        public string ContentPath { get; set; } = @"C:\XboxGames\Minecraft for Windows\Content";
        public string Language { get; set; } = "TR";
        public string Theme { get; set; } = "Blue";
        public bool LightMode { get; set; } = false;
    }
}



