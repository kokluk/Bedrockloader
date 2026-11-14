namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    async void ShowBackupActions()
    {
        if (backupBusy) return;
        int selected = ShowShortcutChoices(T("Backups", "Yedekler"),
            T("Choose what to export or restore.", "Dışa aktarmak veya geri yüklemek istediğin işlemi seç."),
            [(T("Export settings", "Ayarları dışa aktar"), true),
             (T("Import settings", "Ayarları içe aktar"), true),
             (T("Export full backup", "Tam yedeği dışa aktar"), true),
             (T("Restore full backup", "Tam yedeği içe aktar"), true)]);
        // Finish closing the selector before opening the existing backup dialogs.
        if (selected < 0) return;
        if (selected is 0 or 2) await ExportBackupAsync(full: selected == 2, back: ShowBackupActions);
        else await ImportBackupAsync(full: selected == 3, back: ShowBackupActions);
    }

    void ShowInstallationFolders()
    {
        string?[] folders =
        [
            MinecraftInstallation.FindGameDirectories(settings.ContentPath).FirstOrDefault(),
            Path.GetDirectoryName(FindCinnabar(nightly: false)),
            Path.GetDirectoryName(FindCinnabar(nightly: true))
        ];
        string[] names = ["Minecraft", T("Cinnabar Stable", "Cinnabar Kararlı"), T("Cinnabar Nightly", "Cinnabar Gecelik")];
        int selected = ShowShortcutChoices(T("Installation folders", "Kurulum klasörleri"),
            T("Which installation folder would you like to open?", "Hangi kurulum klasörünü açmak istersin?"),
            names.Select((name, index) => (name, Directory.Exists(folders[index]))).ToArray(), folderActions: true);
        if (selected == folders.Length)
        {
            using var picker = new FolderBrowserDialog
            {
                Description = T("Choose the Minecraft Content folder", "Minecraft Content klasörünü seç"),
                SelectedPath = Directory.Exists(settings.ContentPath) ? settings.ContentPath : ""
            };
            if (picker.ShowDialog(this) == DialogResult.OK && Directory.Exists(picker.SelectedPath))
            {
                settings.ContentPath = picker.SelectedPath;
                SaveSettings();
                RefreshDiscordOptions();
                UpdateStatus(T("Content folder saved.", "Content klasörü kaydedildi."));
            }
            return;
        }
        if (selected >= 0 && folders[selected] is { } folder && Directory.Exists(folder)) OpenPath(folder);
    }

    int ShowShortcutChoices(string title, string description, (string Text, bool Enabled)[] choices, bool folderActions = false)
    {
        using var popup = new RoundedPopupForm(Accent)
        {
            Text = title, FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.CenterParent,
            ShowInTaskbar = false, BackColor = Surface, ForeColor = TextColor, ClientSize = new Size(480, 342),
            AutoScaleMode = AutoScaleMode.Dpi
        };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(22), BackColor = Surface };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
        popup.Controls.Add(layout);
        void Span(Control control, int row, int height)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            layout.Controls.Add(control, 0, row);
            layout.SetColumnSpan(control, 2);
        }
        var heading = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        var x = MakeShortcutClose(popup);
        var headingText = WrappedText(title, 16, true);
        headingText.Dock = DockStyle.Fill;
        headingText.Padding = new Padding(0, 0, 44, 0);
        heading.Controls.Add(headingText);
        x.Dock = DockStyle.None;
        x.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        void PlaceClose() => x.Location = new Point(Math.Max(0, heading.ClientSize.Width - x.Width), 2);
        heading.Resize += (_, _) => PlaceClose();
        heading.Controls.Add(x);
        x.BringToFront();
        PlaceClose();
        Span(heading, 0, 44);
        Span(WrappedText(description), 1, 42);
        int result = -1;
        for (int i = 0; i < choices.Length; i++)
        {
            int choice = i;
            var button = MakeButton(folderActions ? T("Open folder", "Klasörü aç") : choices[i].Text,
                () => { result = choice; popup.DialogResult = DialogResult.OK; }, icon: folderActions ? "Addons" : "backup");
            button.Dock = DockStyle.Fill;
            button.Enabled = choices[i].Enabled;
            if (folderActions)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
                var label = MakeLabel(choices[i].Text + (choices[i].Enabled ? "" : T(" · Not installed", " · Kurulu değil")));
                label.Anchor = AnchorStyles.Left;
                layout.Controls.Add(label, 0, i + 2);
                layout.Controls.Add(button, 1, i + 2);
            }
            else
            {
                if (i % 2 == 0) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
                layout.Controls.Add(button, i % 2, 2 + i / 2);
            }
        }
        if (folderActions)
        {
            var change = MakeButton(T("Change Minecraft folder", "Minecraft klasörünü değiştir"),
                () => { result = choices.Length; popup.DialogResult = DialogResult.OK; });
            change.Dock = DockStyle.Fill;
            Span(change, choices.Length + 2, 46);
        }
        popup.CancelButton = x;
        if (!folderActions) popup.ClientSize = new Size(565, 248);
        return popup.ShowDialog(this) == DialogResult.OK ? result : -1;
    }

    SymbolButton MakeShortcutClose(Form popup)
    {
        var close = new SymbolButton { Symbol = "close", Dock = DockStyle.Right, Size = new Size(36, 36),
            MinimumSize = new Size(36, 36),
            CornerRadius = 18, AccessibleName = T("Close", "Kapat"),
            ForeColor = lightMode ? Color.FromArgb(153, 28, 42) : Color.FromArgb(255, 129, 144),
            BackColor = MixColors(Surface, Color.FromArgb(218, 55, 72), lightMode ? .16 : .25), Cursor = Cursors.Hand };
        close.Click += (_, _) => popup.DialogResult = DialogResult.Abort;
        return close;
    }
}
