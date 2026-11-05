namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    CatalogMod? SelectCatalogVersion(CatalogMod mod)
    {
        using var popup = new RoundedPopupForm(Accent)
        {
            Text = T("Choose Minecraft version", "Minecraft sürümünü seç"),
            FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false,
            StartPosition = FormStartPosition.CenterParent, BackColor = Bg, ForeColor = TextColor,
            ClientSize = new Size(Math.Min(540, ClientSize.Width - 48), 370),
            Font = Font, AutoScaleMode = AutoScaleMode.Dpi
        };
        var card = new RoundedPanel { Dock = DockStyle.Fill, CornerRadius = 24, BackColor = Surface, Padding = new Padding(22) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = Surface, Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        var header = new Panel { Dock = DockStyle.Fill, BackColor = Surface, Margin = Padding.Empty };
        var title = WrappedText(mod.Name, 16, true); title.Dock = DockStyle.Fill;
        var close = new SymbolButton { Symbol = "close", AccessibleName = T("Close", "Kapat"), Dock = DockStyle.Right, Width = 36, Height = 36, BackColor = MixColors(Surface, Accent, 0.12), ForeColor = TextColor, FocusBorderColor = Accent, CornerRadius = 18, Cursor = Cursors.Hand };
        close.Click += (_, _) => popup.DialogResult = DialogResult.Cancel;
        header.Controls.Add(title); header.Controls.Add(close);
        var explanation = WrappedText(T("Choose your Minecraft version. Download starts only when you select Download & install.", "Minecraft sürümünü seç. İndirme yalnızca İndir ve kur seçildiğinde başlar."), 10); explanation.Dock = DockStyle.Fill;
        var selector = new SoftSelector(this) { Dock = DockStyle.Fill, Margin = new Padding(0, 2, 0, 4), Font = new Font("Segoe UI", 10), AccessibleName = T("Minecraft version", "Minecraft sürümü") };
        foreach (var variant in mod.Variants)
            selector.Items.Add(variant.Version == mod.DefaultVariant ? T($"{variant.Version} (Latest — default)", $"{variant.Version} (Son sürüm — varsayılan)") : variant.Version);
        selector.SelectedIndex = mod.Variants.ToList().FindIndex(v => v.Version == mod.DefaultVariant);
        ApplyButtonPalette(selector);
        var details = new ScrollTextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9), BackColor = Surface, ForeColor = Muted, TabStop = false };
        void UpdateDetails()
        {
            var variant = mod.Variants[selector.SelectedIndex];
            details.Text = T($"Source: {variant.Download}\n\nDestination: {Path.Combine(modsFolder, mod.FileName)}", $"Kaynak: {variant.Download}\n\nHedef: {Path.Combine(modsFolder, mod.FileName)}").ReplaceLineEndings(Environment.NewLine);
        }
        selector.SelectedIndexChanged += (_, _) => UpdateDetails();
        UpdateDetails();
        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, BackColor = Surface, Padding = new Padding(0, 10, 0, 0) };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var cancel = MakeButton(T("Cancel", "Vazgeç"), () => popup.DialogResult = DialogResult.Cancel);
        cancel.Dock = DockStyle.Fill; cancel.Margin = new Padding(0, 0, 5, 0); ApplyButtonPalette(cancel);
        var install = MakeButton(T("Download & install", "İndir ve kur"), () => popup.DialogResult = DialogResult.OK, icon: "download");
        install.Tag = "primary"; install.Dock = DockStyle.Fill; install.Margin = new Padding(5, 0, 0, 0); ApplyButtonPalette(install);
        actions.Controls.Add(cancel, 0, 0); actions.Controls.Add(install, 1, 0);
        popup.AcceptButton = cancel; popup.CancelButton = cancel;
        layout.Controls.Add(header, 0, 0); layout.Controls.Add(explanation, 0, 1); layout.Controls.Add(selector, 0, 2); layout.Controls.Add(details, 0, 3); layout.Controls.Add(actions, 0, 4);
        card.Controls.Add(layout); popup.Controls.Add(card);
        popup.Shown += (_, _) => selector.Focus();
        return popup.ShowDialog(this) == DialogResult.OK ? ModCatalog.SelectVariant(mod, mod.Variants[selector.SelectedIndex].Version) : null;
    }
}
