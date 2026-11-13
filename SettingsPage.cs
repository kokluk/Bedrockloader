using System.Drawing.Drawing2D;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    bool showAdvancedSettings;

    void BuildSettingsPage()
    {
        AddPageHeading(T("Settings", "Ayarlar"), T("Choose the game path, language, and accent color.", "Oyun yolunu, dili ve vurgu rengini seç."));
        const int advancedRow = 4, separatorRow = 5, rpcRow = 6, backupRow = 7, settingsBackupRow = 8, fullBackupRow = 9, contentRow = 10, shortcutsRow = 11;
        var layout = new RoundedSettingsList(this) { Height = 640, ColumnCount = 2, RowCount = 12, Padding = new Padding(16, 14, 16, 14), Tag = "settings-card" };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        int[] rowHeights = [52, 52, 0, 58, 56, 1, 58, 55, 52, 52, 62, 58];
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

        layout.Controls.Add(MakeLabel(cinnabarMode ? T("Cinnabar folder", "Cinnabar klasörü") : T("Content folder", "Content klasörü"), 10, true), 0, contentRow);
        var pathRow = new TableLayoutPanel { Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Height = 40, Tag = "settings-card", ColumnCount = cinnabarMode ? 2 : 3, RowCount = 1, Margin = new Padding(0, 8, 0, 8) };
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
        if (!cinnabarMode) pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
        pathRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        string? cinnabarPath = cinnabarMode ? Path.GetDirectoryName(FindCinnabar()) : null;
        var path = MakeTextBox(cinnabarMode ? cinnabarPath ?? "" : settings.ContentPath); path.BorderStyle = BorderStyle.None;
        path.ReadOnly = cinnabarMode;
        path.PlaceholderText = cinnabarMode ? T("Cinnabar is not installed", "Cinnabar kurulu değil") : "";
        var pathSurface = new RoundedPanel { Dock = DockStyle.Fill, CornerRadius = 16, Tag = "card", Margin = new Padding(0, 0, 8, 0) };
        pathSurface.Controls.Add(path);
        void AlignPath() { path.SetBounds(12, Math.Max(0, (pathSurface.ClientSize.Height - path.Height) / 2), Math.Max(1, pathSurface.ClientSize.Width - 24), path.Height); }
        pathSurface.Resize += (_, _) => AlignPath(); AlignPath();
        pathRow.Controls.Add(pathSurface, 0, 0);
        var browse = MakeButton(cinnabarMode ? T("Open folder", "Klasörü aç") : T("Browse", "Gözat"), () =>
        {
            if (cinnabarMode) { if (cinnabarPath != null) OpenPath(cinnabarPath); return; }
            using var d = new FolderBrowserDialog { Description = T("Select the Minecraft Content folder", "Minecraft Content klasörünü seç"),
                SelectedPath = Directory.Exists(path.Text) ? path.Text : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) };
            if (d.ShowDialog(this) == DialogResult.OK) path.Text = d.SelectedPath;
        }, 100);
        browse.Enabled = !cinnabarMode || cinnabarPath != null;
        browse.Dock = DockStyle.Fill; browse.Margin = Padding.Empty; pathRow.Controls.Add(browse, 1, 0);
        if (!cinnabarMode)
        {
            var savePath = MakeButton(T("Save", "Kaydet"), () =>
            {
                string selected = path.Text.Trim().Trim('"');
                if (!Path.IsPathFullyQualified(selected) || !Directory.Exists(selected))
                {
                    ShowThemedPopup(T("Content folder", "Content klasörü"),
                        T("Choose an existing folder.", "Var olan bir klasör seç."));
                    return;
                }
                settings.ContentPath = selected;
                SaveSettings();
                RefreshDiscordOptions();
                UpdateStatus(T("Content folder saved.", "Content klasörü kaydedildi."));
            }, 94);
            savePath.Dock = DockStyle.Fill;
            savePath.Margin = new Padding(6, 0, 0, 0);
            pathRow.Controls.Add(savePath, 2, 0);
        }
        layout.Controls.Add(pathRow, 1, contentRow);

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
            bool visible = settings.Theme == "Material You";
            layout.SuspendLayout();
            paletteLabel.Visible = materialColors.Visible = visible;
            layout.RowStyles[2].Height = visible ? 100 : 0;
            foreach (int row in Enumerable.Range(separatorRow, shortcutsRow - separatorRow + 1))
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
        };
        layout.Controls.Add(MakeLabel("Discord RPC", 10, true), 0, rpcRow);
        var discord = MakeButton(T("RPC settings", "RPC ayarları"), ConfigureDiscordPresence, 154, "discord");
        discord.Anchor = AnchorStyles.Left | AnchorStyles.Top;
        discord.Margin = new Padding(0, 6, 0, 0);
        layout.Controls.Add(discord, 1, rpcRow);
        layout.Controls.Add(MakeLabel(T("Customization", "Özelleştirme"), 10, true), 0, 3);
        var customize = MakeButton(T("Customization settings", "Özelleştirme ayarları"), ShowCustomization, 205, "sliders");
        customize.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        customize.Margin = new Padding(0, 9, 3, 0);
        layout.Controls.Add(customize, 1, 3);
        layout.Controls.Add(MakeLabel(T("Shortcuts", "Kısayollar"), 10, true), 0, shortcutsRow);
        var wizard = MakeButton(T("Show setup wizard", "Kurulum sihirbazını tekrar göster"), ShowSetupWizard, 275, "settings");
        wizard.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        wizard.Margin = new Padding(0, 8, 3, 0);
        layout.Controls.Add(wizard, 1, shortcutsRow);
        layout.Controls.Add(MakeLabel(T("Backups", "Yedekler"), 10, true), 0, backupRow);
        var backupCaption = MakeLabel(T("JSON contains settings and servers. Full ZIP backups can include selected worlds, skins, packs and mods.",
            "JSON, ayarları ve sunucuları içerir. Tam ZIP yedeğine seçtiğin dünyaları, skinleri, paketleri ve modları ekleyebilirsin."), 9);
        backupCaption.Dock = DockStyle.Fill; backupCaption.AutoSize = false;
        layout.Controls.Add(backupCaption, 1, backupRow);
        AddBackupRow(settingsBackupRow, false);
        AddBackupRow(fullBackupRow, true);
        void AddBackupRow(int row, bool full)
        {
            var pair = new TableLayoutPanel { Tag = "settings-card", Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0, 0, 0, 8) };
            pair.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            pair.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            var export = MakeButton(full ? T("Export full backup", "Tam yedeği dışa aktar") : T("Export settings", "Ayarları dışa aktar"), () => _ = ExportBackupAsync(full));
            var import = MakeButton(full ? T("Restore full backup", "Tam yedeği içe aktar") : T("Import settings", "Ayarları içe aktar"), () => _ = ImportBackupAsync(full));
            export.Dock = import.Dock = DockStyle.Fill;
            pair.Controls.Add(export, 0, 0);
            pair.Controls.Add(import, 1, 0);
            layout.Controls.Add(pair, 1, row);
        }
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
