using System.Drawing.Drawing2D;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    bool showAdvancedSettings;

    void BuildSettingsPage()
    {
        AddPageHeading(T("Settings", "Ayarlar"), T("Choose the game path, language, and accent color.", "Oyun yolunu, dili ve vurgu rengini seç."));
        var heading = pageHeadingPanel!;
        var light = new SymbolButton { Symbol = lightMode ? "sun" : "moon", Size = new Size(36, 36),
            Tag = "icon", Cursor = Cursors.Hand, AccessibleName = T("Toggle light / dark mode", "Gece / gündüz modunu değiştir") };
        void PositionLight() => light.Location = new Point(Math.Max(0, heading.ClientSize.Width - light.Width - 4), 3);
        heading.Resize += (_, _) => PositionLight();
        PositionLight();
        heading.Controls.Add(light);
        light.BringToFront();
        var themeTip = new ToolTip();
        themeTip.SetToolTip(light, light.AccessibleName);
        light.Disposed += (_, _) => themeTip.Dispose();
        light.Click += (_, _) =>
        {
            lightMode = !lightMode;
            settings.LightMode = lightMode;
            settings.FollowWindowsTheme = false;
            SaveSettings();
            light.Symbol = lightMode ? "sun" : "moon";
            ApplyTheme();
            light.Invalidate();
        };
        const int advancedRow = 4, separatorRow = 5, rpcRow = 6, shortcutsRow = 7;
        const int removalRow = shortcutsRow + 1;
        var layout = new RoundedSettingsList(this) { Height = 640, ColumnCount = 2, RowCount = removalRow + 1, Padding = new Padding(16, 14, 16, 14), Tag = "settings-card" };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        int[] rowHeights = [52, 52, 0, 58, 56, 1, 58, 58, 104];
        foreach (int height in rowHeights)
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        var advancedSeparator = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, TabStop = false, Tag = "settings-card" };
        advancedSeparator.Paint += (_, e) =>
        {
            using var pen = new Pen(MixColors(advancedSeparator.BackColor, TextColor, .13));
            e.Graphics.DrawLine(pen, 0, 0, advancedSeparator.ClientSize.Width, 0);
        };
        layout.Controls.Add(advancedSeparator, 0, separatorRow);
        layout.SetColumnSpan(advancedSeparator, 2);

        layout.Controls.Add(MakeLabel(T("Language", "Dil"), 10, true), 0, 0);
        var lang = MakeThemeCombo(250); lang.Margin = new Padding(0, 3, 8, 8);
        lang.Items.AddRange(new object[] { "Türkçe", "English" }); lang.SelectedIndex = english ? 1 : 0; layout.Controls.Add(lang, 1, 0);
        lang.SelectedIndexChanged += (_, _) =>
        {
            string selected = lang.SelectedIndex == 1 ? "EN" : "TR";
            if (selected != settings.Language) SetLanguage(selected);
        };

        layout.Controls.Add(MakeLabel(T("Theme", "Tema"), 10, true), 0, 1);
        var theme = MakeThemeCombo(250); theme.Margin = new Padding(0, 3, 8, 8);
        theme.Items.AddRange(ThemeChoices);
        theme.SelectedItem = settings.Theme; if (theme.SelectedIndex < 0) theme.SelectedIndex = 0;
        layout.Controls.Add(theme, 1, 1);
        var paletteLabel = MakeLabel(T("Color palette", "Renk paleti"), 10, true);
        var materialColors = BuildMaterialColorChoices();
        layout.Controls.Add(paletteLabel, 0, 2); layout.Controls.Add(materialColors, 1, 2);
        materialColors.Tag = "settings-card";
        var advancedButton = MakeButton("", () => { }, 225, "sliders");
        advancedButton.Click += (_, _) => { showAdvancedSettings = !showAdvancedSettings; RefreshSettingsRows(); };
        advancedButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        advancedButton.Margin = new Padding(0, 8, 3, 0);
        layout.Controls.Add(advancedButton, 1, advancedRow);
        void RefreshSettingsRows()
        {
            bool visible = UsesAccentPalette(settings.Theme);
            layout.SuspendLayout();
            paletteLabel.Visible = materialColors.Visible = visible;
            layout.RowStyles[2].Height = visible ? 100 : 0;
            foreach (int row in Enumerable.Range(separatorRow, removalRow - separatorRow + 1))
            {
                layout.RowStyles[row].Height = showAdvancedSettings ? rowHeights[row] : 0;
                for (int column = 0; column < 2; column++)
                    if (layout.GetControlFromPosition(column, row) is { } item) item.Visible = showAdvancedSettings;
            }
            advancedButton.Text = showAdvancedSettings ? T("Hide advanced settings", "Gelişmiş ayarları gizle") :
                T("Show advanced settings", "Gelişmiş ayarları göster");
            layout.Height = (int)layout.RowStyles.Cast<RowStyle>().Sum(style => style.Height) + layout.Padding.Vertical;
            layout.ResumeLayout(true);
        }
        theme.SelectedIndexChanged += (_, _) =>
        {
            settings.Theme = theme.SelectedItem?.ToString() ?? "Midnight";
            RefreshSettingsRows(); SaveSettings(); ApplyTheme();
            UpdateStatus(T($"Theme: {settings.Theme}", $"Tema: {settings.Theme}"));
        };
        layout.Controls.Add(MakeLabel(T("Folder shortcuts", "Klasör kısayolları"), 10, true), 0, rpcRow);
        var integrations = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Tag = "settings-card" };
        integrations.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        integrations.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        var discord = MakeButton("Discord RPC", ConfigureDiscordPresence, icon: "discord");
        var paths = MakeButton(T("Installation folders", "Kurulum klasörleri"), ShowInstallationFolders, icon: "Addons");
        discord.Dock = paths.Dock = DockStyle.Fill;
        discord.Margin = new Padding(0, 7, 6, 7);
        paths.Margin = new Padding(6, 7, 0, 7);

        integrations.Controls.Add(paths, 1, 0);
        layout.Controls.Add(integrations, 1, rpcRow);
        layout.Controls.Add(MakeLabel(T("Customization", "Özelleştirme"), 10, true), 0, 3);
        var customize = MakeButton(T("Customization settings", "Özelleştirme ayarları"), ShowCustomization, 205, "sliders");
        customize.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        customize.Margin = new Padding(0, 9, 3, 0);
        var customizationActions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Tag = "settings-card" };
        customizationActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        customizationActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        customize.Dock = DockStyle.Fill;
        customize.Margin = new Padding(6, 7, 0, 7);
        customizationActions.Controls.Add(discord, 0, 0);
        customizationActions.Controls.Add(customize, 1, 0);
        layout.Controls.Add(customizationActions, 1, 3);
        layout.Controls.Add(MakeLabel(T("Shortcuts", "Kısayollar"), 10, true), 0, shortcutsRow);
        var wizard = MakeButton(T("Show setup wizard", "Kurulum sihirbazını tekrar göster"), ShowSetupWizard, 275, "settings");
        wizard.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        wizard.Margin = new Padding(0, 8, 3, 0);
        layout.Controls.Add(wizard, 1, shortcutsRow);
        layout.Controls.Add(MakeLabel(T("Warning", "Uyarı"), 10, true), 0, removalRow);
        var removal = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Tag = "settings-card", Margin = Padding.Empty };
        removal.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        removal.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        var deleteData = MakeButton(T("Delete app data", "Uygulama verilerini sil"), DeleteLauncherData, icon: "trash");
        var uninstall = MakeButton(T("Uninstall app", "Uygulamayı sil"), UninstallLauncher, icon: "trash");
        foreach (var button in new[] { deleteData, uninstall })
        {
            button.Tag = "danger";
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(0, 5, 8, 5);
            removal.Controls.Add(button);
        }
        layout.Controls.Add(removal, 1, removalRow);
        foreach (Control label in layout.Controls)
            if (label is Label) { label.Anchor = AnchorStyles.Top | AnchorStyles.Left; label.Margin = new Padding(12, 10, 8, 0); }
        RefreshSettingsRows();
        contentHost.Controls.Add(new ThemedScrollPanel(this, layout) { Dock = DockStyle.Fill });
    }

    sealed class RoundedSettingsList : TableLayoutPanel
    {
        readonly MainForm owner;

        public RoundedSettingsList(MainForm owner)
        {
            this.owner = owner;
            DoubleBuffered = true;
            ResizeRedraw = true;
            owner.MakeRounded(this, 19);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width < 20 || Height < 20) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var outline = new Pen(MixColors(BackColor, owner.TextColor, .12));
            using var path = SoftIcons.Round(1, 1, Width - 3, Height - 3, 18);
            e.Graphics.DrawPath(outline, path);
        }
    }
}
