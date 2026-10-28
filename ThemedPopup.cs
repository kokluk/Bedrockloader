namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    DialogResult ShowThemedPopup(string title, string message, bool confirmation = false)
    {
        using var popup = new RoundedPopupForm
        {
            Text = title, FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false,
            StartPosition = FormStartPosition.CenterParent, BackColor = Bg, ForeColor = TextColor,
            ClientSize = new Size(Math.Min(600, ClientSize.Width - 48), Math.Min(460, ClientSize.Height - 48)),
            Font = Font, Padding = Padding.Empty, AutoScaleMode = AutoScaleMode.Dpi
        };
        var card = new RoundedPanel { Dock = DockStyle.Fill, CornerRadius = 24, BackColor = Surface, Padding = new Padding(24) };
        popup.Controls.Add(card);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Surface, Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, Padding = new Padding(0, 0, 0, 16) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var heading = new Label { Text = title, Dock = DockStyle.Fill, ForeColor = TextColor, Font = new Font("Segoe UI", 16, FontStyle.Bold) };
        var close = new SymbolButton
        {
            Symbol = "close", AccessibleName = T("Close", "Kapat"), Size = new Size(36, 36),
            Anchor = AnchorStyles.Top | AnchorStyles.Right, Margin = Padding.Empty,
            BackColor = MixColors(Surface, Accent, 0.12), ForeColor = TextColor,
            FocusBorderColor = Accent, CornerRadius = 18, Cursor = Cursors.Hand
        };
        close.Click += (_, _) => popup.DialogResult = confirmation ? DialogResult.No : DialogResult.OK;
        header.Controls.Add(heading, 0, 0); header.Controls.Add(close, 1, 0);
        var body = new ScrollTextBox { Text = message.ReplaceLineEndings(Environment.NewLine), Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = Surface, ForeColor = TextColor, Font = new Font("Segoe UI", 10), WordWrap = true, TabStop = false };
        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = confirmation ? 2 : 1, RowCount = 1, Margin = Padding.Empty, Padding = new Padding(0, 14, 0, 6), BackColor = Surface };
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        for (int i = 0; i < actions.ColumnCount; i++) actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / actions.ColumnCount));
        var cancel = MakeButton(confirmation ? T("Cancel", "Vazgeç") : T("OK", "Tamam"), () => popup.DialogResult = confirmation ? DialogResult.No : DialogResult.OK);
        cancel.Dock = DockStyle.Fill; cancel.Margin = new Padding(0, 0, confirmation ? 6 : 0, 0);
        ApplyButtonPalette(cancel); actions.Controls.Add(cancel, 0, 0);
        popup.CancelButton = cancel;
        if (confirmation)
        {
            var accept = MakeButton(T("Allow installation", "Kuruluma izin ver"), () => popup.DialogResult = DialogResult.Yes);
            accept.Tag = "primary"; accept.Dock = DockStyle.Fill; accept.Margin = new Padding(6, 0, 0, 0);
            ApplyButtonPalette(accept); actions.Controls.Add(accept, 1, 0);
        }
        // Enter defaults to cancellation; Escape also dismisses without approval.
        popup.AcceptButton = cancel;
        layout.Controls.Add(header, 0, 0); layout.Controls.Add(body, 0, 1); layout.Controls.Add(actions, 0, 2);
        card.Controls.Add(layout);
        popup.Shown += (_, _) => cancel.Focus();
        return popup.ShowDialog(this);
    }

    sealed class RoundedPopupForm : Form
    {
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
            using var path = RoundedPath(ClientRectangle, (int)Math.Round(24 * DeviceDpi / 96d));
            var previous = Region;
            Region = new Region(path);
            previous?.Dispose();
        }
    }
}
