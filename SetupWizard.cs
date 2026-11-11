using System.Text;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    static readonly string[] ThemeChoices = ["Midnight", "Purple", "Green", "Orange", "Red", "Material You"];

    static string NormalizeLocalName(string? name) => string.Concat((name ?? "").EnumerateRunes()
        .Where(rune => !Rune.IsControl(rune) && Rune.GetUnicodeCategory(rune) != System.Globalization.UnicodeCategory.Format)
        .Take(24).Select(rune => rune.ToString())).Trim();

    SoftSelector MakeAppearanceSelector()
    {
        var selector = MakeThemeCombo(220);
        selector.Items.AddRange([T("Windows theme", "Windows teması"), T("Light", "Aydınlık"), T("Dark", "Karanlık")]);
        selector.SelectedIndex = settings.FollowWindowsTheme ? 0 : settings.LightMode ? 1 : 2;
        return selector;
    }

    void SaveAppearanceChoice(int index)
    {
        settings.FollowWindowsTheme = index == 0;
        settings.LightMode = index == 1;
        lightMode = settings.FollowWindowsTheme ? WindowsUsesLightTheme() : settings.LightMode;
    }

    PopupToggle MakeShineToggle() => new(Accent, SurfaceAlt)
    {
        Text = T("Button glow", "Düğme parlaması"),
        Checked = settings.ShinyHomeIcons, Dock = DockStyle.Fill, Cursor = Cursors.Hand
    };

    PopupToggle MakeAnimationToggle(bool advanced) => new(Accent, SurfaceAlt)
    {
        Text = advanced ? T("Advanced animations", "Gelişmiş animasyonlar") : T("Interface animations", "Arayüz animasyonları"),
        Checked = advanced ? settings.AdvancedAnimationsEnabled : settings.AnimationsEnabled,
        Dock = DockStyle.Fill, Cursor = Cursors.Hand
    };

    void ShowSetupWizard()
    {
        using var popup = new RoundedPopupForm(Accent)
        {
            Text = "BedrockLoader", StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false,
            ClientSize = new Size(590, 390), BackColor = Surface, ForeColor = TextColor
        };
        var original = (settings.Theme, settings.MaterialYouColor, settings.FollowWindowsTheme, settings.LightMode, lightMode);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 10, Padding = new Padding(18, 18, 18, 6) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (int height in new[] { 40, 62, 48, 48, 48, 132, 48, 44 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        popup.Controls.Add(layout);
        var heading = WrappedText("", 17, true);
        var body = WrappedText("", 11);
        heading.Dock = body.Dock = DockStyle.Fill;
        var accent = MakeThemeCombo(220);
        accent.Items.AddRange(ThemeChoices);
        accent.SelectedItem = settings.Theme;
        var appearance = MakeAppearanceSelector();
        var customization = BuildChoiceGrid(MakeCustomizationChoices(out var applyCustomization, previewMotion: true), 2);
        var name = MakeTextBox(settings.LocalName);
        name.MaxLength = 48;
        name.AccessibleName = name.PlaceholderText = T("Local name (optional)", "Yerel ad (isteğe bağlı)");
        var nameBox = WrapPopupInput(name);
        var rpc = new PopupToggle(Accent, SurfaceAlt)
        {
            Text = T("Enable Discord Rich Presence", "Discord Rich Presence’ı aç"),
            Checked = settings.Discord.Enabled, Dock = DockStyle.Fill, Cursor = Cursors.Hand
        };
        ApplyButtonPalette(accent);
        ApplyButtonPalette(appearance);
        layout.Controls.Add(heading, 0, 0);
        layout.Controls.Add(body, 0, 1);
        layout.Controls.Add(accent, 0, 2);
        layout.Controls.Add(appearance, 0, 3);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, WrapContents = false, FlowDirection = FlowDirection.RightToLeft };
        var materials = BuildMaterialColorChoices(Preview);
        layout.Controls.Add(materials, 0, 4);
        layout.Controls.Add(customization, 0, 5);
        layout.Controls.Add(nameBox, 0, 6);
        layout.Controls.Add(rpc, 0, 7);
        layout.Controls.Add(footer, 0, 9);
        int page = 0;
        var next = MakeButton("", () => { }, 126);
        var back = MakeButton(T("Back", "Geri"), () => { }, 105);
        var skip = MakeButton(T("Skip", "Atla"), () => popup.DialogResult = DialogResult.Cancel, 105);
        next.Tag = "primary";
        ApplyButtonPalette(next);
        back.Click += (_, _) => { page--; Render(); };
        next.Click += (_, _) =>
        {
            if (page < 4) { page++; Render(); }
            else popup.DialogResult = DialogResult.OK;
        };
        footer.Controls.AddRange([next, back, skip]);
        popup.AcceptButton = next;
        popup.CancelButton = skip;
        accent.SelectedIndexChanged += (_, _) => { Preview(); Render(); };
        appearance.SelectedIndexChanged += (_, _) => Preview();
        Render();
        if (popup.ShowDialog(this) != DialogResult.OK)
        {
            (settings.Theme, settings.MaterialYouColor, settings.FollowWindowsTheme, settings.LightMode, lightMode) = original;
            AnimatedRoundButton.AnimationsEnabled = settings.AnimationsEnabled;
            AnimatedRoundButton.AdvancedAnimationsEnabled = settings.AdvancedAnimationsEnabled;
            ApplyTheme();
            return;
        }
        applyCustomization();
        settings.LocalName = NormalizeLocalName(name.Text);
        settings.Discord = settings.Discord with { Enabled = rpc.Checked };
        settings.IntroSeen = true;
        SaveSettings();
        RefreshDiscordOptions();
        BuildShellRefresh();

        void Preview()
        {
            settings.Theme = accent.SelectedItem?.ToString() ?? settings.Theme;
            SaveAppearanceChoice(appearance.SelectedIndex);
            ApplyTheme();
            popup.BackColor = footer.BackColor = Surface;
            popup.BorderAccent = Accent;
            RecolorTree(popup);
            popup.Invalidate(true);
        }

        void Render()
        {
            heading.Text = page switch
            {
                0 => T("Welcome to BedrockLoader", "BedrockLoader’a hoş geldin"),
                1 => T("Your name", "Adın"),
                2 => T("Appearance", "Görünüm"),
                3 => T("Customization", "Özelleştirme"),
                _ => "Discord Rich Presence"
            };
            body.Text = page switch
            {
                0 => T("Start Minecraft or switch to Cinnabar from Home.", "Minecraft ve Cinnabar’a ana ekrandan geçebilirsin."),
                1 => T("Add a name for your Home greeting, or leave it blank.", "Ana ekran selamlaması için bir ad yazabilirsin. Bu alan isteğe bağlı."),
                2 => T("Choose an accent palette and a light or dark appearance.", "Renk paletini ve aydınlık ya da karanlık görünümü seç."),
                3 => T("Choose button effects, sound and window behavior.", "Düğme efektlerini, sesi ve pencere davranışını seç."),
                _ => T("Show Minecraft activity in Discord. If you entered a local name, it will appear there when Rich Presence is enabled.",
                    "Minecraft etkinliğini Discord’da göster. Bir yerel ad girdiysen Rich Presence açıkken adın da görünür.")
            };
            accent.Visible = appearance.Visible = page == 2;
            materials.Visible = page == 2 && accent.SelectedItem?.ToString() == "Material You";
            customization.Visible = page == 3;
            nameBox.Visible = page == 1;
            rpc.Visible = page == 4;
            for (int row = 2; row <= 7; row++)
                layout.RowStyles[row].Height = row switch
                {
                    2 or 3 when page == 2 => 48,
                    4 when page == 2 && accent.SelectedItem?.ToString() == "Material You" => 92,
                    5 when page == 3 => 132,
                    6 when page == 1 => 48,
                    7 when page == 4 => 44,
                    _ => 0
                };
            back.Enabled = page > 0;
            next.Text = page == 4 ? T("Finish", "Bitir") : T("Next", "İleri");
        }
    }
}
