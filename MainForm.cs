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
    const string DisplayVersion = "v0.17.9";
    SymbolButton? updateLauncher;
    static readonly HttpClient Http = CreateHttpClient();
    readonly string modsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Minecraft bedrock", "mods");
    readonly string settingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BedrockLoader", "settings.json");
    readonly string logFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BedrockLoader", "logs.txt");
    readonly string appDataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BedrockLoader");
    readonly string addonDownloads;
    readonly string comMojangRoot;
    readonly List<Button> navButtons = new();
    readonly Panel contentHost = new();
    readonly Label status = new SoftStatusLabel();
    readonly SelectorMenu languageMenu = new();
    bool showOtherModFiles;
    readonly Panel dropPanel = new RoundedPanel { CornerRadius = 20, Tag = "card" };
    readonly Label dropTitle = new();
    readonly Label dropSubtitle = new();
    readonly CheckedListBox modList = new SpacedCheckedListBox();
    Label? modCountLabel;
    readonly CheckedListBox resourcePackList = new SpacedCheckedListBox();
    readonly CheckedListBox behaviorPackList = new SpacedCheckedListBox();
    Label? resourcePackCount, behaviorPackCount, resourcePackEmpty, behaviorPackEmpty;
    Button? deleteModsButton;
    Button? deleteAddonsButton;
    AnimatedActionReveal? deleteModsReveal, deleteAddonsReveal;
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
        AnimatedRoundButton.AnimationsEnabled = settings.AnimationsEnabled;
        AnimatedRoundButton.AdvancedAnimationsEnabled = settings.AdvancedAnimationsEnabled;
        english = settings.Language == "EN";
        addonDownloads = Path.Combine(appDataRoot, "Addons", "Downloads");
        comMojangRoot = ResolveComMojangRoot();
        lightMode = settings.FollowWindowsTheme ? WindowsUsesLightTheme() : settings.LightMode;
        ApplyThemePalette(); ApplyLightPalette();

        Text = "BedrockLoader";
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Size = new Size(1040, 680);
        MinimumSize = new Size(800, 540);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;
        // Leave the resize perimeter on the form itself, outside child panels.
        Padding = new Padding(8);
        Resize += (_, _) => { int inset = WindowState == FormWindowState.Maximized ? 20 : 8; if (Padding.Left != inset) Padding = new Padding(inset); };
        Font = LauncherFont.Create(9f);
        DoubleBuffered = true;
        AllowDrop = true;
        DragEnter += AppDragEnter;
        DragOver += AppDragOver;
        DragLeave += AppDragLeave;
        DragDrop += AppDragDrop;

        BuildShell();
        foreach (Control target in new Control[] { dropPanel, dropTitle, dropSubtitle })
        {
            target.AllowDrop = true;
            target.DragEnter += AppDragEnter;
            target.DragOver += AppDragOver;
            target.DragLeave += AppDragLeave;
            target.DragDrop += AppDragDrop;
        }
        Directory.CreateDirectory(modsFolder);
        Directory.CreateDirectory(addonDownloads);
        ShowPage("Home");
        Shown += (_, _) => ShowLauncherIntro();
        LogAction("Application started", "Uygulama başlatıldı");
        ApplyTheme();
        UpdateStatus(T("Ready.", "Hazır."));
        StartDiscordPresence();
    }

    static HttpClient CreateHttpClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("BedrockLoader/0.17.9 (Windows; .NET 8)");
        return http;
    }

    void BuildShell()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(2, 0, 2, 0), ColumnCount = 1, RowCount = 3, Tag = "shell" };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        Controls.Add(root);
        var header = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, Tag = "shell" };
        topHeader = header;
        void Drag(object? sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero); } }
        header.MouseDown += Drag;
        header.Paint += (_, e) => { using var pen = new Pen(lightMode ? Color.FromArgb(206, 216, 225) : Color.FromArgb(35, 49, 65)); e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1); };
        var logo = new SymbolButton { Symbol = "update", Width = 40, Height = 40, CornerRadius = 20, Location = new Point(0, 11), Tag = "icon", ShowUpdateBadge = ReleaseUpdates.ShouldNotify(availableRelease) };
        updateLauncher = logo;
        logo.Click += (_, _) => { logo.Spin(); ShowPage("Updates"); };
        logo.AccessibleName = T("Updates", "Güncellemeler");
        header.Controls.Add(logo);
        var brand = new Label { Text = "BedrockLoader", Font = LauncherFont.Create(12, FontStyle.Bold), AutoSize = true, Location = new Point(48, 11) };
        var version = new Label { Text = DisplayVersion, Font = LauncherFont.Create(9, FontStyle.Bold), AutoSize = true, Location = new Point(49, 37) };
        brand.MouseDown += Drag; version.MouseDown += Drag;
        header.Controls.Add(brand); header.Controls.Add(version);
        var settingsButton = new SymbolButton { Symbol = "sliders", Text = T("Settings", "Ayarlar"), Width = 106, Height = 36, Tag = "icon", AccessibleName = T("Settings", "Ayarlar") };
        settingsButton.Click += (_, _) => ShowPage("Settings");
        var tips = new ToolTip(); tips.SetToolTip(settingsButton, T("Settings", "Ayarlar"));
        var traffic = new FlowLayoutPanel { Width = 108, Height = 36, Padding = new Padding(0, 3, 0, 0), FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Tag = "traffic", Margin = Padding.Empty };
        void Traffic(string tip, string symbol, Action action)
        {
            var button = new SymbolButton { Symbol = symbol, Width = 30, Height = 30, CornerRadius = 12, Margin = new Padding(3, 0, 3, 0), Cursor = Cursors.Hand, AccessibleName = tip };
            button.Click += (_, _) => action(); tips.SetToolTip(button, tip); traffic.Controls.Add(button);
        }
        Traffic(T("Minimize", "Küçült"), "minimize", () => ShowWindow(Handle, 6));
        Traffic(T("Maximize / restore", "Büyüt / geri al"), "maximize", () => ShowWindow(Handle, WindowState == FormWindowState.Maximized ? 9 : 3));
        Traffic(T("Close", "Kapat"), "close", Close);
        header.Controls.Add(settingsButton); header.Controls.Add(traffic);
        root.Controls.Add(header, 0, 0);
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(0, 20, 0, 20), Margin = Padding.Empty, Tag = "shell" };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210)); body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.Controls.Add(body, 0, 1);
        var nav = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 12, AutoScroll = false, Margin = new Padding(0, 0, 12, 0), Padding = new Padding(1, 0, 1, 0), Tag = "shell" };
        nav.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int row = 0; row < 11; row++) nav.RowStyles.Add(new RowStyle(SizeType.Absolute, row is 1 or 8 or 10 ? 16 : 42));
        nav.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        body.Controls.Add(nav, 0, 0);
        AddNav(nav, "Home", "", T("Home", "Ana ekran"));
        void Divider()
        {
            var divider = new Panel { Dock = DockStyle.Top, Height = 1, Margin = new Padding(8, 6, 8, 0) };
            divider.Paint += (_, e) => { using var pen = new Pen(MixColors(Bg, TextColor, .18)); e.Graphics.DrawLine(pen, 0, 0, divider.Width, 0); };
            nav.Controls.Add(divider, 0, nav.Controls.Count);
        }
        Divider();
        AddNav(nav, "Mods", "", T("Mods", "Modlar"));
        AddNav(nav, "CinnabarMods", "", T("Mods", "Modlar"));
        AddNav(nav, "Addons", "", T("Packs", "Packler")); AddNav(nav, "Skins", "", T("Skins", "Skinler")); AddNav(nav, "Worlds", "", T("Worlds", "Dünyalar")); AddNav(nav, "Servers", "", T("Servers", "Sunucular"));
        Divider();
        AddNav(nav, "About", "", T("About", "Hakkında"));
        Divider();
        contentHost.Dock = DockStyle.Fill; contentHost.BackColor = Surface; contentHost.Tag = "page";
        contentHost.Margin = Padding.Empty; contentHost.Padding = new Padding(12, 13, 12, 12);
        MakeRounded(contentHost, 20); body.Controls.Add(contentHost, 1, 0);
        var footer = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, Tag = "shell" }; footerBar = footer;
        footer.Paint += (_, e) => { using var pen = new Pen(lightMode ? Color.FromArgb(206, 216, 225) : Color.FromArgb(35, 49, 65)); e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0); };
        status.Dock = DockStyle.Fill; status.Font = LauncherFont.Create(8); status.TextAlign = ContentAlignment.MiddleLeft; status.Padding = Padding.Empty;
        footer.Controls.Add(status);
        usageBadge?.Dispose();
        usageBadge = new LauncherUsageBadge(this) { Visible = settings.ShowUsage };
        header.Controls.Add(usageBadge);
        usageBadge.BringToFront();
        var logs = new SymbolButton { Symbol = "Logs", Text = T("Logs", "Kayıtlar"), Size = new Size(106, 36), Cursor = Cursors.Hand };
        logs.Click += (_, _) => ShowPage("Logs");
        footer.Controls.Add(logs);
        logs.BringToFront();
        status.Padding = new Padding(0, 0, 156, 0);
        var logsTip = new ToolTip();
        logsTip.SetToolTip(logs, logs.AccessibleName);
        logs.Disposed += (_, _) => logsTip.Dispose();
        languageMenu.ShowImageMargin = false; languageMenu.ShowCheckMargin = false;
        languageMenu.Renderer = new SelectorMenuRenderer(this);
        languageMenu.Padding = new Padding(5);
        languageMenu.Items.Clear();
        foreach (var (title, code) in new[] { ("Türkçe", "TR"), ("English", "EN") })
        {
            var item = new ToolStripMenuItem(title) { AutoSize = false, Size = new Size(166, 38), Checked = settings.Language == code };
            item.Click += (_, _) => languageMenu.SelectAndClose(() => SetLanguage(code));
            languageMenu.Items.Add(item);
        }
        var language = new SymbolButton { Symbol = "language", Width = 36, Height = 36, Tag = "icon", AccessibleName = T("Language", "Dil") };
        languageMenu.ToggleOwner = language;
        language.Click += (_, _) =>
        {
            if (languageMenu.Visible) languageMenu.Close();
            else languageMenu.Show(language, new Point(language.Width, language.Height + 6), ToolStripDropDownDirection.BelowLeft);
        };
        var light = new SymbolButton { Symbol = lightMode ? "sun" : "moon", Width = 36, Height = 36, Tag = "icon", AccessibleName = T("Toggle theme", "Temayı değiştir") };
        light.Click += (_, _) => { lightMode = !lightMode; settings.LightMode = lightMode; settings.FollowWindowsTheme = false; SaveSettings(); BuildShellRefresh(); };
        tips.SetToolTip(language, T("Language", "Dil")); tips.SetToolTip(light, T("Toggle theme", "Temayı değiştir"));
        footer.Controls.Add(light); light.BringToFront();
        void ArrangeFooterActions()
        {
            light.Location = new Point(Math.Max(0, footer.ClientSize.Width - 40), 6);
            logs.Location = new Point(Math.Max(0, light.Left - logs.Width - 6), 6);
        }
        footer.SizeChanged += (_, _) => ArrangeFooterActions();
        ArrangeFooterActions();
        header.Controls.Add(language);
        void ArrangeHeaderActions()
        {
            traffic.Location = new Point(header.ClientSize.Width - traffic.Width, 13);
            settingsButton.Location = new Point(traffic.Left - settingsButton.Width - 6, 13);
            language.Location = new Point(settingsButton.Left - 42, 13);
            usageBadge.Location = new Point(Math.Max(0, language.Left - usageBadge.Width - 16), 16);
        }
        header.Resize += (_, _) => ArrangeHeaderActions();
        ArrangeHeaderActions();
        root.Controls.Add(footer, 0, 2);
        TrackProximity(root);
    }

    void SetLanguage(string code) { settings.Language = code; english = code == "EN"; SaveSettings(); RefreshDiscordOptions(); BuildShellRefresh(); }

    void BuildShellRefresh()
    {
        DisposeSelfContainedPage();
        navButtons.Clear(); pageHeadingPanel = null;
        Controls.Clear(); languageMenu.Items.Clear(); ApplyThemePalette(); ApplyLightPalette(); BuildShell(); ShowPage(currentPage, force: true); ApplyTheme();
        UpdateStatus(T("Ready.", "Hazır."));
    }

    void DisposeSelfContainedPage()
    {
        if (currentPage is not ("Home" or "Logs" or "Settings")) return;
        pageTitle.Parent?.Controls.Remove(pageTitle);
        pageSubtitle.Parent?.Controls.Remove(pageSubtitle);
        foreach (Control control in contentHost.Controls.Cast<Control>().ToArray()) control.Dispose();
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
        if (Program.RestoreMessage != 0 && m.Msg == Program.RestoreMessage)
        {
            RestoreFromTray();
            return;
        }
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
        var b = new NavIconButton { IconKey = key, Text = title, Font = LauncherFont.Create(9), CornerRadius = 17, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, BackColor = Bg, ForeColor = TextColor, Margin = new Padding(0, 0, 0, 7), Cursor = Cursors.Hand, Tag = key };
        b.Click += (_, _) => ShowPage(key); navButtons.Add(b); host.Controls.Add(b, 0, host.Controls.Count);
    }

    AnimatedRoundButton MakeButton(string text, Action action, int width = 130, string? icon = null)
    {
        var b = new AnimatedRoundButton { Text = text, Width = width, Height = 38, FlatStyle = FlatStyle.Flat, BackColor = SurfaceAlt, ForeColor = TextColor, Cursor = Cursors.Hand, Margin = new Padding(0, 0, 8, 8), Font = new Font(PreferredFontName(), 10.5f) };
        b.FlatAppearance.BorderSize = 0;
        b.CornerRadius = 18;
        b.LeadingIcon = icon;
        ApplyButtonPalette(b);
        b.Click += (_, _) => action();
        return b;
    }

    TextBox MakeTextBox(string value = "")
    {
        var input = new DragTextBox
        {
            Text = value, BackColor = SurfaceAlt, ForeColor = TextColor,
            BorderStyle = BorderStyle.FixedSingle, Font = new Font(PreferredFontName(), 10)
        };
        var menu = new ContextMenuStrip { ShowImageMargin = false };
        var copy = new ToolStripMenuItem(T("Copy", "Kopyala"));
        var paste = new ToolStripMenuItem(T("Paste", "Yapıştır"));
        var selectAll = new ToolStripMenuItem(T("Select all", "Tümünü seç"));
        copy.Click += (_, _) => RunClipboardAction(() =>
        {
            if (input.SelectionLength > 0) WriteClipboardText(input.SelectedText);
        });
        paste.Click += (_, _) => RunClipboardAction(input.Paste);
        selectAll.Click += (_, _) => input.SelectAll();
        menu.Items.AddRange([copy, paste, selectAll]);
        menu.Opening += (_, _) =>
        {
            copy.Enabled = input.SelectionLength > 0;
            paste.Enabled = !input.ReadOnly;
            selectAll.Enabled = input.TextLength > 0;
        };
        input.ContextMenuStrip = menu;
        input.Disposed += (_, _) => menu.Dispose();
        return input;
    }

    static void WriteClipboardText(string value) =>
        Clipboard.SetDataObject(new DataObject(DataFormats.UnicodeText, value), copy: true, retryTimes: 5, retryDelay: 30);

    void RunClipboardAction(Action action)
    {
        try { action(); }
        catch (System.Runtime.InteropServices.ExternalException ex)
        { ShowError(ex, T("Clipboard is unavailable", "Panoya erişilemedi")); }
    }
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

    void UpdateModeNavigation(string key)
    {
        foreach (var button in navButtons)
        {
            string navKey = (string)button.Tag!;
            ApplyNavigationColor(button, navKey == (key switch { "CurseForge" => "Addons", "CurseWorlds" => "Worlds", "CurseShaders" => "Mods", "CurseSkins" => "Skins", _ => key }));
            button.Text = navKey == "CinnabarMods" ? T("Mods", "Modlar") : navKey == "Mods" && cinnabarMode ? T("Shaders", "Shaderlar") : NavText(navKey, "").Trim();
            if (button is NavIconButton navIcon) navIcon.IconKey = navKey == "CinnabarMods" ? "Mods" : navKey == "Mods" && cinnabarMode ? "sun" : navKey;
            if (navKey is "Addons" or "Skins" or "CinnabarMods")
            {
                bool visible = navKey == "CinnabarMods" ? cinnabarMode : !cinnabarMode;
                button.Visible = visible;
                if (button.Parent is TableLayoutPanel nav) nav.RowStyles[nav.GetRow(button)].Height = visible ? 42 : 0;
            }
        }
    }

    void ShowLauncherIntro()
    {
        if (settings.IntroSeen) return;
        ShowSetupWizard();
        settings.IntroSeen = true;
        SaveSettings();
    }

    void ShowPage(string key, bool force = false)
    {
        if (!force && currentPage == key && contentHost.Controls.Count > 0) return;
        if (currentPage is "CurseForge" or "CurseWorlds" or "CurseShaders" or "CurseSkins") DisposeCursePage();
        if (currentPage == "Skins") DisposeSkinPage();
        if (currentPage == "Servers") { serversView?.Dispose(); serversView = null; }
        DisposeSelfContainedPage();
        currentPage = key;
        if (usageBadge is { IsDisposed: false })
        {
            usageBadge.Visible = settings.ShowUsage;
        }
        // Paint only after native child controls have their final colors and bounds.
        contentHost.Visible = false;
        contentHost.SuspendLayout();
        try
        {
        contentHost.Controls.Clear(); pageHeadingPanel = null;
        UpdateModeNavigation(key);
        switch (key)
        {
            case "Home": BuildComfortableHome(); break;
            case "Mods": if (cinnabarMode) BuildCinnabarShaders(); else BuildModsPage(); break;
            case "CinnabarMods": BuildCinnabarMods(); break;
            case "Addons": BuildSoftAddons(); break;
            case "Skins": BuildSkinPage(); break;
            case "Settings": BuildSettingsPage(); break;
            case "Worlds": BuildWorldsPage(); break;
            case "Servers": BuildServersPage(); break;
            case "Logs": BuildLogsPage(); break;
            case "Catalog": BuildModCatalogPage(); break;
            case "CurseForge": BuildCurseForgePage(); break;
            case "CurseWorlds": BuildCurseForgePage(true); break;
            case "CurseSkins": BuildCurseForgePage(skins: true); break;
            case "CurseShaders": BuildCurseForgePage(shaders: true); break;
            case "Updates": BuildUpdatesPage(); break;
            default: BuildComfortableAbout(); break;
        }
        // Dock the heading before the fill panel so they never overlap.
        if (pageHeadingPanel != null && contentHost.Controls.Contains(pageHeadingPanel)) contentHost.Controls.SetChildIndex(pageHeadingPanel, contentHost.Controls.Count - 1);
        ApplyTheme();
        ResetDropText();
        }
        finally
        {
            contentHost.ResumeLayout(true);
            contentHost.Visible = true;
            contentHost.Invalidate(true);
        }
    }

    string NavText(string key, string glyph)
    {
        string text = key switch { "Home" => T("Home", "Ana ekran"), "Mods" => T("Mods", "Modlar"), "Clients" => T("Client Manager", "Client Yöneticisi"), "Addons" => T("Packs", "Packler"), "Settings" => T("Settings", "Ayarlar"), "Skins" => T("Skins", "Skinler"), "Worlds" => T("Worlds", "Dünyalar"), "Servers" => T("Servers", "Sunucular"), "Logs" => T("Logs", "Kayıtlar"), _ => T("About", "Hakkında") };
        return text;
    }

    void AddPageHeading(string title, string subtitle)
    {
        var block = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Surface, Tag = "page", Margin = Padding.Empty };
        pageHeadingPanel = block;
        pageTitle.Dock = DockStyle.None;
        pageTitle.TextAlign = ContentAlignment.MiddleLeft;
        pageSubtitle.Visible = true;
        pageTitle.Text = title; pageTitle.Font = LauncherFont.Create(14, FontStyle.Bold); pageTitle.AutoSize = false; pageTitle.AutoEllipsis = true;
        pageSubtitle.Text = subtitle; pageSubtitle.Font = LauncherFont.Create(9.5f); pageSubtitle.AutoSize = false;
        pageSubtitle.TextAlign = ContentAlignment.MiddleLeft; pageSubtitle.AutoEllipsis = true;
        string? symbol = currentPage switch
        {
            "Home" or "Addons" or "Skins" or "Worlds" or "Servers" or "Logs" => currentPage,
            "Mods" => cinnabarMode ? "sun" : "Mods",
            "CinnabarMods" => "Mods",
            "Updates" => "update",
            "Settings" => "sliders",
            _ => null
        };
        var glyph = symbol == null ? null : new HeadingGlyph(this, symbol);
        if (glyph != null) block.Controls.Add(glyph);
        void AlignHeading()
        {
            if (pageTitle.Dock != DockStyle.None) return;
            int gap = glyph == null ? 0 : 30;
            int actionWidth = block.Controls.OfType<Button>().Select(b => b.Width + 12).DefaultIfEmpty(0).Max();
            int available = Math.Max(42, block.ClientSize.Width - actionWidth);
            int width = Math.Min(available, TextRenderer.MeasureText(title, pageTitle.Font).Width + gap + 8);
            const int left = 0;
            pageTitle.SetBounds(left + gap, 0, Math.Max(1, width - gap), 32);
            pageSubtitle.SetBounds(0, 35, Math.Max(1, block.ClientSize.Width - 8), 24);
            if (glyph != null) glyph.Location = new Point(left, 3);
        }
        block.Resize += (_, _) => AlignHeading();
        block.ControlAdded += (_, _) => AlignHeading();
        AlignHeading();
        block.Controls.Add(pageTitle); block.Controls.Add(pageSubtitle); contentHost.Controls.Add(block); block.BringToFront();
    }

    void BuildModsPage()
    {
        var layout = new BufferedActionLayout { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty, Tag = "page" };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        ConfigureDropPanel(false);
        var add = MakeButton(T("Choose file & install", "Dosya seç ve yükle"), ChooseModFiles, 180, "download");
        add.Tag = "primary"; add.Height = 32; add.Dock = DockStyle.Bottom;
        dropPanel.Controls.Add(add);
        dropPanel.Dock = DockStyle.Fill; dropPanel.Margin = new Padding(0, 0, 0, 12); dropPanel.Padding = new Padding(16);
        layout.Controls.Add(dropPanel, 0, 0);
        void FitModDropArea()
        {
            bool compact = layout.ClientSize.Height < 330;
            layout.RowStyles[0].Height = compact ? 114 : 137;
            dropPanel.Padding = new Padding(12, compact ? 10 : 16, 12, compact ? 10 : 16);
            dropTitle.Height = compact ? 22 : 26;
            dropSubtitle.Height = compact ? 24 : 28;
        }
        layout.Resize += (_, _) => FitModDropArea();
        FitModDropArea();
        var listPanel = new RoundedPanel { Dock = DockStyle.Fill, CornerRadius = 14, Padding = new Padding(13, 12, 13, 8), Margin = Padding.Empty, Tag = "card" };
        var listHeader = new Panel { Dock = DockStyle.Top, Height = 30, Tag = "card" };
        var listTitle = MakeLabel(T("Mod list", "Mod listesi"), 9, true); listTitle.Location = new Point(0, 4); listHeader.Controls.Add(listTitle);
        modCountLabel = MakeLabel("", 7); modCountLabel.Dock = DockStyle.Right; modCountLabel.AutoSize = false; modCountLabel.Width = 80; modCountLabel.TextAlign = ContentAlignment.TopRight; modCountLabel.Padding = new Padding(0, 6, 0, 0); listHeader.Controls.Add(modCountLabel);
        modList.Dock = DockStyle.Fill; modList.CheckOnClick = true; modList.IntegralHeight = false; modList.BorderStyle = BorderStyle.None;
        modList.Font = LauncherFont.Create(9); modList.ItemHeight = 34;
        modList.ItemCheck -= ModList_ItemCheck; modList.ItemCheck += ModList_ItemCheck;
        listPanel.Controls.Add(modList); listPanel.Controls.Add(listHeader);
        var deleteMods = MakeButton(T("Delete checked", "Seçilenleri sil"), DeleteCheckedMods, 150);
        deleteModsButton = deleteMods;
        deleteModsReveal = new AnimatedActionReveal(deleteMods, 28, 8) { Dock = DockStyle.Bottom, BackColor = SurfaceAlt, Tag = "card" };
        listPanel.Controls.Add(deleteModsReveal);
        layout.Controls.Add(listPanel, 0, 1);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 0), Margin = Padding.Empty, WrapContents = false, Tag = "page" };
        var refresh = MakeButton(T("Refresh", "Yenile"), RefreshMods, 100); refresh.Height = 32; refresh.Font = LauncherFont.Create(9);
        buttons.Controls.Add(refresh);
        string OtherFilesLabel() => showOtherModFiles
            ? T("Hide other files", "Diğer dosyaları gizle") : T("Show other files", "Diğer dosyaları göster");
        var otherFiles = MakeButton(OtherFilesLabel(), () =>
        {
            showOtherModFiles = !showOtherModFiles;
            RefreshMods();
        }, 176);
        otherFiles.Height = refresh.Height;
        otherFiles.Font = refresh.Font;
        otherFiles.Click += (_, _) => otherFiles.Text = OtherFilesLabel();
        buttons.Controls.Add(otherFiles);
        var menu = new ContextMenuStrip(); menu.Items.Add(T("Open mods folder", "Mod klasörünü aç"), null, (_, _) => OpenMods()); modList.ContextMenuStrip = menu;
        layout.Controls.Add(buttons, 0, 2); contentHost.Controls.Add(layout);
        AddPageHeading(T("Mods", "Modlar"), T("Manage your installed mods.", "Yüklü modlarını yönet.")); RefreshMods();
        var catalog = MakeButton(T("Mod Center", "Mod Merkezi"), () => ShowPage("Catalog"), 172);
        catalog.Height = 32; catalog.Font = LauncherFont.Create(9);
        // Reserve heading space so its title and action cannot overlap at 800px.
        if (pageHeadingPanel != null)
        {
            var heading = pageHeadingPanel;
            heading.Controls.Add(catalog);
            void PlaceCatalogButton() => catalog.Location = new Point(Math.Max(0, heading.ClientSize.Width - catalog.Width), 3);
            heading.Resize += (_, _) => PlaceCatalogButton();
            PlaceCatalogButton();
        }
    }

    void ModList_ItemCheck(object? sender, ItemCheckEventArgs e)
    {
        BeginInvoke(new Action(() => { if (currentPage == "Mods") deleteModsReveal?.SetShown(modList.CheckedItems.Count > 0); }));
    }

    void DeleteCheckedMods()
    {
        var selected = modList.CheckedItems.Cast<string>().ToList(); if (selected.Count == 0) return;
        if (MessageBox.Show(this, T($"Delete {selected.Count} checked file(s)?", $"İşaretli {selected.Count} dosya silinsin mi?"), T("Confirm deletion", "Silme onayı"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        int removed = 0;
        foreach (var relative in selected) { var full = Path.GetFullPath(Path.Combine(modsFolder, relative)); if (full.StartsWith(Path.GetFullPath(modsFolder) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && File.Exists(full)) { File.Delete(full); removed++; } }
        LogAction($"Deleted {removed} checked mod file(s)", $"Seçilen {removed} mod dosyası silindi"); RefreshMods(); UpdateStatus(T($"Deleted {removed} file(s).", $"{removed} dosya silindi."));
    }

    void AddonList_ItemCheck(object? sender, ItemCheckEventArgs e)
    {
        BeginInvoke(new Action(() => { if (currentPage == "Addons") deleteAddonsReveal?.SetShown(resourcePackList.CheckedItems.Count + behaviorPackList.CheckedItems.Count > 0); }));
    }

    void BuildStatsPage()
    {
        AddPageHeading(T("Minecraft statistics", "Minecraft istatistikleri"), T("Local pack stats and shortcuts. Xbox online achievements/playtime require Xbox account access.", "Yerel paket istatistikleri ve kısayollar. Çevrim içi başarımlar/oyun süresi için Xbox hesap erişimi gerekir."));
        var card = new Panel { Dock = DockStyle.Top, Height = 210, BackColor = Surface, Padding = new Padding(18) };
        int resources = CountFolders(Path.Combine(comMojangRoot, "resource_packs"));
        int behaviors = CountFolders(Path.Combine(comMojangRoot, "behavior_packs"));
        int stored = Directory.Exists(modsFolder) ? Directory.GetFiles(modsFolder, "*", SearchOption.AllDirectories).Length : 0;
        card.Controls.Add(new Label { Text = $"{T("Resource packs", "Kaynak paketleri")}: {resources}\n\n{T("Behavior packs", "Davranış paketleri")}: {behaviors}\n\n{T("Files in local mod folder", "Yerel mod klasöründeki dosyalar")}: {stored}", AutoSize = true, Font = LauncherFont.Create(12, FontStyle.Bold), ForeColor = TextColor, Location = new Point(18, 18) });
        var xbox = MakeButton(T("Open Xbox profile", "Xbox profilini aç"), OpenXboxProfile, 170); xbox.Location = new Point(18, 155); card.Controls.Add(xbox);
        contentHost.Controls.Add(card);
    }

    SoftSelector MakeThemeCombo(int width)
    {
        return new SoftSelector(this) { Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Width = width, Height = 40, CornerRadius = 18, BackColor = SurfaceAlt, ForeColor = TextColor,
            Font = LauncherFont.Create(10.5f), Margin = new Padding(0, 0, 0, 8) };
    }

    void BuildLogsPage()
    {
        AddPageHeading(T("Logs", "Kayıtlar"), T("Actions performed by BedrockLoader are recorded here.", "BedrockLoader ile yapılan işlemler burada kaydedilir."));
        var heading = pageHeadingPanel!;
        var export = MakeButton(T("Export logs", "Kayıtları dışa aktar"), ExportDiagnostics, 172, "download");
        export.Height = 32;
        heading.Controls.Add(export);
        void PlaceExport() => export.Location = new Point(Math.Max(0, heading.ClientSize.Width - export.Width), 3);
        heading.Resize += (_, _) => PlaceExport();
        PlaceExport();
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Tag = "page", Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        var logCard = new RoundedPanel { Dock = DockStyle.Fill, CornerRadius = 16, Padding = new Padding(12), Margin = Padding.Empty };
        var log = new ScrollTextBox { Dock = DockStyle.Fill, BackColor = Surface, ForeColor = TextColor, Font = new Font("Consolas", 10), Text = ReadLogs() };
        logCard.Controls.Add(log); layout.Controls.Add(logCard, 0, 0);
        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(0, 12, 0, 0), Tag = "page", Margin = Padding.Empty };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var refresh = MakeButton(T("Refresh logs", "Kayıtları yenile"), () => log.Text = ReadLogs(), 120);
        refresh.Dock = DockStyle.Fill; refresh.Margin = new Padding(0, 0, 5, 0);
        var clear = MakeButton(T("Clear logs", "Kayıtları temizle"), () =>
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logFile)!);
                lock (logLock) File.WriteAllText(logFile, string.Empty);
                log.Text = ReadLogs();
                UpdateStatus(T("Logs cleared.", "Kayıtlar temizlendi."));
            }
            catch (Exception ex) { ShowError(ex, T("Could not clear logs", "Kayıtlar temizlenemedi")); }
        }, 120);
        clear.Dock = DockStyle.Fill; clear.Margin = new Padding(5, 0, 0, 0);
        actions.Controls.Add(refresh, 0, 0); actions.Controls.Add(clear, 1, 0); layout.Controls.Add(actions, 0, 1);
        contentHost.Controls.Add(layout);
    }

    readonly object logLock = new();
    const int MaxLogLines = 300;

    void LogAction(string englishAction, string turkishAction)
    {
        try
        {
            lock (logLock)
            {
                if (deletingLauncherData) return;
                Directory.CreateDirectory(Path.GetDirectoryName(logFile)!);
                var lines = File.Exists(logFile) ? File.ReadLines(logFile).TakeLast(MaxLogLines - 1).ToList() : [];
                lines.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {T(englishAction, turkishAction).ReplaceLineEndings(" ")}");
                File.WriteAllLines(logFile, lines);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    string ReadLogs()
    {
        try
        {
            string contents;
            lock (logLock) contents = File.Exists(logFile) ? string.Join(Environment.NewLine, File.ReadLines(logFile).TakeLast(MaxLogLines)) : "";
            return string.IsNullOrWhiteSpace(contents) ? T("No actions recorded yet.", "Henüz işlem kaydı yok.") : contents;
        }
        catch { return T("Could not read logs.", "Kayıtlar okunamadı."); }
    }

    void ConfigureDropPanel(bool addonMode)
    {
        dropPanel.Dock = DockStyle.Top;
        dropPanel.Height = addonMode ? 66 : 104;
        dropPanel.BackColor = SurfaceAlt;
        dropPanel.BorderStyle = BorderStyle.None;
        dropPanel.Controls.Clear();
        dropTitle.Text = addonMode ? T("Drop pack files here", "Paket dosyalarını buraya bırak") : T("Drop mod files here", "Mod dosyalarını buraya bırak");
        dropTitle.Font = LauncherFont.Create(11, FontStyle.Bold);
        dropTitle.ForeColor = TextColor;
        dropTitle.AutoSize = false; dropTitle.Dock = DockStyle.Top; dropTitle.Height = 26;
        dropTitle.BackColor = Color.Transparent;
        dropSubtitle.Text = addonMode ? T(".mcpack, .mcaddon or compatible .zip files will be installed into Minecraft AppData.", ".mcpack, .mcaddon veya uyumlu .zip dosyaları Minecraft AppData'ya yüklenir.") : T("Files will be copied to the app's local mods folder.", "Dosyalar uygulamanın yerel mods klasörüne kopyalanır.");
        dropSubtitle.Font = LauncherFont.Create(10);
        dropSubtitle.ForeColor = Muted;
        dropSubtitle.AutoSize = false; dropSubtitle.Dock = DockStyle.Top; dropSubtitle.Height = 28;
        dropSubtitle.BackColor = Color.Transparent;
        dropPanel.Controls.Add(dropSubtitle); dropPanel.Controls.Add(dropTitle);
    }

    void AppDragEnter(object? sender, DragEventArgs e)
    {
        bool valid = currentPage is "Mods" or "Addons" && e.Data?.GetDataPresent(DataFormats.FileDrop) == true;
        e.Effect = valid ? DragDropEffects.Copy : DragDropEffects.None;
        if (!valid) return;
        isDragging = true;
        dropTitle.Text = currentPage == "Addons" ? T("Release to install pack(s)", "Paketleri yüklemek için bırak") : T("Release to add mod files", "Mod dosyalarını eklemek için bırak");
        dropSubtitle.Text = currentPage == "Addons" ? T("Files will be installed into Minecraft AppData.", "Dosyalar Minecraft AppData'ya yüklenir.") : T("Release to confirm adding the selected files to the local mods folder.", "Seçili dosyaları yerel mod klasörüne eklemek için bırak.");
        dropPanel.BackColor = MixColors(SurfaceAlt, Accent, lightMode ? 0.18 : 0.24);
    }

    void AppDragOver(object? sender, DragEventArgs e)
    {
        e.Effect = currentPage is "Mods" or "Addons" && e.Data?.GetDataPresent(DataFormats.FileDrop) == true
            ? DragDropEffects.Copy : DragDropEffects.None;
    }

    void AppDragLeave(object? sender, EventArgs e) { if (isDragging) ResetDropText(); }
    void AppDragDrop(object? sender, DragEventArgs e)
    {
        ResetDropText();
        if (currentPage is not ("Mods" or "Addons")) return;
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] files) return;
        if (currentPage == "Addons") InstallAddonFiles(files);
        else CopyMods(files);
    }

    void ResetDropText()
    {
        isDragging = false;
        if (dropPanel.IsDisposed) return;
        dropPanel.BackColor = SurfaceAlt;
        dropTitle.Text = currentPage == "Addons" ? T("Drop pack files here", "Paket dosyalarını buraya bırak") : T("Drop mod files here", "Mod dosyalarını buraya bırak");
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
            RefreshMods(); LogAction($"Installed {count} mod file(s) into {modsFolder}", $"{count} mod dosyası şu klasöre kuruldu: {modsFolder}"); UpdateStatus(T($"Copied {count} file(s) to local mods folder.", $"{count} dosya yerel mods klasörüne kopyalandı."));
        }
        catch (Exception ex) { ShowError(ex, T("File copy failed", "Dosya kopyalanamadı")); }
    }

    void RefreshMods()
    {
        Directory.CreateDirectory(modsFolder); modList.BeginUpdate(); modList.Items.Clear();
        foreach (var f in Directory.EnumerateFiles(modsFolder, "*", SearchOption.AllDirectories)
            .Where(file => showOtherModFiles || Path.GetExtension(file).Equals(".dll", StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)) modList.Items.Add(Path.GetRelativePath(modsFolder, f), false);
        modList.EndUpdate(); if (modCountLabel != null) modCountLabel.Text = $"{modList.Items.Count} {T("mods", "mod")}"; deleteModsReveal?.SetShown(false, animate: false);
    }

    void OpenMods() { Directory.CreateDirectory(modsFolder); LogAction("Opened local mods folder", "Yerel mod klasörü açıldı"); OpenPath(modsFolder); }

    void OpenContentFolder()
    {
        if (!Directory.Exists(settings.ContentPath))
        {
            var answer = MessageBox.Show(this, T("The configured Content folder does not exist. Open the settings page to choose it?", "Seçili Content klasörü bulunamadı. Ayarlardan seçmek ister misin?"), T("Content folder not found", "Content klasörü bulunamadı"), MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (answer == DialogResult.Yes) ShowPage("Settings");
            return;
        }
        LogAction("Opened Minecraft Content folder", "Minecraft Content klasörü açıldı"); OpenPath(settings.ContentPath);
    }

    void ChooseAndInstallAddon()
    {
        using var d = new OpenFileDialog { Title = T("Choose a Bedrock pack", "Bedrock paketi seç"), Filter = "Bedrock packs (*.mcpack;*.mcaddon;*.zip)|*.mcpack;*.mcaddon;*.zip|All files (*.*)|*.*", Multiselect = true };
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
            LogAction($"Installed {packCount} add-on pack(s) from {paths.Count()} source file(s)", $"{paths.Count()} kaynak dosyadan {packCount} eklenti paketi kuruldu");
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
        void Fill(CheckedListBox list, string category)
        {
            list.BeginUpdate();
            try
            {
                list.Items.Clear();
                foreach (var pack in PackLibrary.Read(comMojangRoot, category)) list.Items.Add(pack, false);
            }
            finally { list.EndUpdate(); }
        }
        Fill(resourcePackList, "resource_packs"); Fill(behaviorPackList, "behavior_packs");
        if (resourcePackCount != null) resourcePackCount.Text = resourcePackList.Items.Count.ToString();
        if (behaviorPackCount != null) behaviorPackCount.Text = behaviorPackList.Items.Count.ToString();
        resourcePackList.Visible = resourcePackList.Items.Count > 0; behaviorPackList.Visible = behaviorPackList.Items.Count > 0;
        if (resourcePackEmpty != null) resourcePackEmpty.Visible = resourcePackList.Items.Count == 0;
        if (behaviorPackEmpty != null) behaviorPackEmpty.Visible = behaviorPackList.Items.Count == 0;
        deleteAddonsReveal?.SetShown(false, animate: false);
    }

    void RemoveSelectedAddon()
    {
        var selected = resourcePackList.CheckedItems.Cast<InstalledPack>().Concat(behaviorPackList.CheckedItems.Cast<InstalledPack>()).ToList(); if (selected.Count == 0) return;
        if (MessageBox.Show(this, T($"Delete {selected.Count} checked pack(s)?", $"İşaretli {selected.Count} paket silinsin mi?"), T("Confirm deletion", "Silme onayı"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        int removed = 0;
        foreach (var item in selected)
        {
            var target = PackLibrary.ResolvePath(comMojangRoot, item);
            if (Directory.Exists(target)) { Directory.Delete(target, true); removed++; LogAction($"Deleted pack: {item.DirectoryName}", $"Paket silindi: {item.DirectoryName}"); }
        }
        RefreshAddonList(); UpdateStatus(T($"Deleted {removed} pack(s).", $"{removed} paket silindi."));
    }

    void OpenAddonFolder() { Directory.CreateDirectory(comMojangRoot); LogAction("Opened Minecraft add-on folder", "Minecraft eklenti klasörü açıldı"); OpenPath(comMojangRoot); }

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
        try { Process.Start(new ProcessStartInfo { FileName = "xbox:", UseShellExecute = true }); LogAction("Opened Xbox app/profile shortcut", "Xbox uygulaması veya profil kısayolu açıldı"); }
        catch { OpenUrl("https://www.xbox.com/profile"); LogAction("Opened Xbox profile website fallback", "Xbox profilinin web sayfası açıldı"); }
    }

    void LaunchGame()
    {
        if (backupBusy) return;
        if (!MinecraftInstallation.IsInstalled(settings.ContentPath)) { ShowMinecraftInstallPrompt(); return; }
        if (!MinecraftInstallation.TryLaunch(settings.ContentPath))
            ShowThemedPopup(T("Launch failed", "Oyun açılamadı"),
                T("Minecraft for Windows could not be launched.", "Minecraft for Windows başlatılamadı."));
    }

    const string MinecraftStoreId = "9NBLGGH2JHXJ";
    void ShowMinecraftInstallPrompt()
    {
        if (ShowThemedPopup(T("Install Minecraft", "Minecraft’ı yükle"),
            T("Install Minecraft for Windows from Microsoft Store or the Xbox app. Return to BedrockLoader after installation.",
              "Minecraft for Windows’u Microsoft Store veya Xbox uygulamasından indirip yüklemen gerekiyor. Kurulum tamamlanınca BedrockLoader’a dön."),
            true, T("Open Microsoft Store", "Microsoft Store’u aç")) == DialogResult.Yes)
            OpenXboxMinecraftPage();
    }
    void OpenXboxMinecraftPage()
    {
        try
        {
            using var store = Process.Start(new ProcessStartInfo("ms-windows-store://pdp/?productid=" + MinecraftStoreId)
            { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception)
        {
            OpenUrl("https://www.xbox.com/games/store/minecraft-for-windows/" + MinecraftStoreId);
        }
    }

    void ApplyThemePalette()
    {
        TextColor = Color.FromArgb(228, 230, 235); Muted = Color.FromArgb(188, 198, 208);
        if (settings.Theme == "Material You")
        {
            Color seed = MaterialAccent();
            Bg = MixColors(Color.FromArgb(12, 13, 18), seed, 0.06);
            Surface = MixColors(Color.FromArgb(22, 23, 29), seed, 0.10);
            SurfaceAlt = MixColors(Color.FromArgb(32, 33, 40), seed, 0.17);
        }
        else
        {
            Bg = Color.FromArgb(8, 15, 23); Surface = Color.FromArgb(17, 28, 40); SurfaceAlt = Color.FromArgb(28, 43, 59);
        }
        Accent = settings.Theme switch
        {
            "Purple" => Color.FromArgb(184, 126, 255),
            "Green" => Color.FromArgb(74, 210, 150),
            "Orange" => Color.FromArgb(255, 165, 80),
            "Red" => Color.FromArgb(255, 95, 105),
            "Material You" => MaterialAccent(),
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
        if (button is MaterialColorButton swatch)
        {
            button.BackColor = swatch.SwatchColor;
            button.ForeColor = ReadableText(swatch.SwatchColor);
            return;
        }
        if (Equals(button.Tag, "danger") || button.Text.Contains("Delete checked", StringComparison.OrdinalIgnoreCase) || button.Text.Contains("Seçilenleri sil", StringComparison.OrdinalIgnoreCase))
        {
            button.BackColor = Color.FromArgb(220, 55, 65);
            button.ForeColor = Color.White;
        }
        else if (Equals(button.Tag, "minecraft-launch") || button.Text.Contains("Minecraft'ı başlat", StringComparison.OrdinalIgnoreCase) || button.Text.Contains("Launch Minecraft", StringComparison.OrdinalIgnoreCase))
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
                button.BackColor = MixColors(SurfaceAlt, Accent, 0.72);
                button.ForeColor = ReadableText(button.BackColor);
            }
            else if (Equals(button.Tag, "card-button"))
            {
                // A neutral filled pill, separated from the card beneath it.
                button.BackColor = MixColors(SurfaceAlt, TextColor, lightMode ? 0.10 : 0.18);
                button.ForeColor = TextColor;
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
            nav.IsActive = selected;
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
        if (settings.Theme == "Material You")
        {
            var seed = MaterialAccent();
            Bg = MixColors(Color.FromArgb(246, 246, 250), seed, 0.08);
            Surface = MixColors(Color.White, seed, 0.06);
            SurfaceAlt = MixColors(Color.FromArgb(240, 240, 245), seed, 0.20);
        }
    }

    void ApplyTheme()
    {
        ApplyThemePalette(); ApplyLightPalette();
        BackColor = Bg; ForeColor = TextColor;
        ApplyDwmTheme();
        RecolorTree(this);
        contentHost.Invalidate(true);
        languageMenu.BackColor = SurfaceAlt; languageMenu.ForeColor = TextColor;
        foreach (var b in navButtons)
        {
            ApplyNavigationColor(b, Equals(b.Tag, currentPage));
        }
        if (status is not null) { status.BackColor = Bg; status.ForeColor = TextColor; if (status is SoftStatusLabel soft) soft.IconColor = Accent; }
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
            if (c is Panel || c is TableLayoutPanel || c is FlowLayoutPanel) { if (Equals(c.Tag, "shell") || Equals(c.Tag, "traffic")) c.BackColor = Bg; else if (Equals(c.Tag, "settings-card")) c.BackColor = MixColors(Surface, SurfaceAlt, .48); else if (Equals(c.Tag, "card") || c is RoundedPanel) c.BackColor = SurfaceAlt; else c.BackColor = Surface; }
            if (c.Tag is "server-latency" or "server-latency-ready" && c.Controls[0] is Label latency)
            {
                ApplyLatencyPalette(c, latency);
                continue;
            }
            if (c is TextBox tb) { tb.BackColor = SurfaceAlt; tb.ForeColor = TextColor; }
            if (c is ListBox lb) { lb.BackColor = SurfaceAlt; lb.ForeColor = TextColor; }
            if (c is SpacedCheckedListBox checks) { checks.CheckAccent = Accent; checks.Cursor = Cursors.Hand; }
            if (c is PopupToggle toggle)
            {
                toggle.AccentColor = Accent; toggle.TrackColor = SurfaceAlt; toggle.ForeColor = TextColor;
                toggle.Invalidate();
            }
            if (c is ComboBox cb) { cb.BackColor = SurfaceAlt; cb.ForeColor = TextColor; }
            if (c is PictureBox) c.BackColor = SurfaceAlt;
            if (c is AboutLogo) { c.ForeColor = TextColor; c.Invalidate(); }
            if (c is Label label) { label.ForeColor = label.Font.Size <= 9 ? Muted : TextColor; }
            if (c is Button button)
            {
                if (button is SymbolButton symbol)
                {
                    button.ForeColor = symbol.Symbol == "update" ? Accent : symbol.Symbol == "close" ? (lightMode ? Color.FromArgb(179, 38, 61) : Color.FromArgb(255, 165, 178)) : TextColor;
                    button.BackColor = MixColors(Bg, symbol.Symbol == "close" ? button.ForeColor : Accent, lightMode ? 0.08 : 0.12);
                    symbol.FocusBorderColor = Accent; continue;
                }
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
                string json = File.ReadAllText(settingsFile);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                using var document = JsonDocument.Parse(json);
                if (loaded.IntroSeen && !document.RootElement.TryGetProperty(nameof(AppSettings.FollowWindowsTheme), out _))
                    loaded.FollowWindowsTheme = false;
                if (!ThemeChoices.Contains(loaded.Theme)) loaded.Theme = "Midnight";
                if (!MaterialColors.Any(choice => choice.Id == loaded.MaterialYouColor)) loaded.MaterialYouColor = "Lavender";
                loaded.Servers ??= [];
                loaded.Discord ??= new DiscordOptions();
                return loaded;
            }
        }
        catch { }
        return new AppSettings();
    }

    void SaveSettings()
    {
        try { WriteSettings(); }
        catch (Exception ex) { ShowError(ex, T("Settings save error", "Ayar kaydetme hatası")); }
    }

    void WriteSettings()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settingsFile)!);
        string temporary = settingsFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, settingsFile, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
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
    void UpdateStatus(string message) => status.Text = message;
    void ShowError(Exception ex, string title) => MessageBox.Show(this, ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
    string T(string en, string tr) => english ? en : tr;

    sealed class SpacedCheckedListBox : CheckedListBox
    {
        int wheelRemainder;
        public Color CheckAccent { get; set; } = Color.FromArgb(50, 190, 240);
        public override int ItemHeight { get => 34; set { } }
        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                parameters.Style &= ~0x00200000; // WS_VSCROLL: keep the native white bar out of themed cards.
                return parameters;
            }
        }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x020A) // WM_MOUSEWHEEL
            {
                wheelRemainder += unchecked((short)((long)message.WParam >> 16));
                int steps = wheelRemainder / 120; wheelRemainder %= 120;
                int visibleRows = Math.Max(1, ClientSize.Height / ItemHeight);
                int lines = SystemInformation.MouseWheelScrollLines;
                if (lines < 0) lines = visibleRows;
                if (Items.Count > 0 && steps != 0 && lines > 0)
                    TopIndex = Math.Clamp(TopIndex - steps * lines, 0, Math.Max(0, Items.Count - visibleRows));
                message.Result = IntPtr.Zero; return;
            }
            base.WndProc(ref message);
        }
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            using var background = new SolidBrush(BackColor);
            e.Graphics.FillRectangle(background, e.Bounds);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var box = new Rectangle(e.Bounds.X + 1, e.Bounds.Y + (e.Bounds.Height - 14) / 2, 14, 14);
            using var path = RoundedPath(box, 4);
            using var pen = new Pen((e.State & DrawItemState.Focus) != 0 && Focused ? CheckAccent : Color.FromArgb(157, 176, 190));
            e.Graphics.DrawPath(pen, path);
            if (GetItemChecked(e.Index))
            {
                using var brush = new SolidBrush(CheckAccent); e.Graphics.FillPath(brush, path);
                SoftIcons.Draw(e.Graphics, "check-mark", new RectangleF(box.X + 1, box.Y + 1, 12, 12), ReadableText(CheckAccent));
            }
            TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, new Rectangle(e.Bounds.X+25,e.Bounds.Y,e.Bounds.Width-25,e.Bounds.Height), ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        }
    }

    sealed class RoundedPanel : Panel
    {
        public int CornerRadius { get; set; } = 18;
        public RoundedPanel() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
        protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(Parent?.BackColor ?? BackColor); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            using var path = RoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), CornerRadius);
            using var brush = new SolidBrush(BackColor);
            e.Graphics.FillPath(brush, path);
            base.OnPaint(e);
        }
    }

    static GraphicsPath RoundedPath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath(); int d = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (d < 2) { path.AddRectangle(bounds); return path; }
        path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90); path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90); path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90); path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
    }

    sealed class AppSettings
    {
        public List<SavedServer> Servers { get; set; } = [];
        public bool IntroSeen { get; set; }
        public string LastRunVersion { get; set; } = "";
        public bool CloseToTray { get; set; } = true;
        public bool TrayNoticeShown { get; set; }
        public bool ShinyHomeIcons { get; set; } = true;
        public bool AnimationsEnabled { get; set; } = true;
        public bool AdvancedAnimationsEnabled { get; set; } = true;
        public bool ClickSounds { get; set; }
        public string LocalName { get; set; } = "";
        public bool ShowUsage { get; set; }
        public bool FollowWindowsTheme { get; set; } = true;
        public string CinnabarExecutable { get; set; } = "";
        public string CinnabarInstalledTag { get; set; } = "";
        public DateTime CinnabarExecutableWriteUtc { get; set; }
        public List<ClientProfile> ClientProfiles { get; set; } = [];
        public DiscordOptions Discord { get; set; } = new();
        public string ContentPath { get; set; } = @"C:\XboxGames\Minecraft for Windows\Content";
        public string Language { get; set; } = "TR";
        public string Theme { get; set; } = "Midnight";
        public string MaterialYouColor { get; set; } = "Lavender";
        public bool LightMode { get; set; } = false;
    }
}
