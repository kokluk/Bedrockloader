namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    DialogResult ShowThemedPopup(string title, string message, bool confirmation = false, string? confirmLabel = null)
    {
        using var popup = new RoundedPopupForm(Accent)
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
        var cancel = MakeButton(confirmation ? T("Cancel", "Vazgeç") : (confirmLabel ?? T("OK", "Tamam")), () => popup.DialogResult = confirmation ? DialogResult.No : DialogResult.OK);
        cancel.Dock = DockStyle.Fill; cancel.Margin = new Padding(0, 0, confirmation ? 6 : 0, 0);
        ApplyButtonPalette(cancel); actions.Controls.Add(cancel, 0, 0);
        popup.CancelButton = cancel;
        if (confirmation)
        {
            var accept = MakeButton(confirmLabel ?? T("Allow installation", "Kuruluma izin ver"), () => popup.DialogResult = DialogResult.Yes);
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

    RoundedPanel WrapPopupInput(TextBox input)
    {
        var field = new RoundedPanel { BackColor = SurfaceAlt, CornerRadius = 13, Padding = new Padding(12, 7, 12, 0) };
        input.BorderStyle = BorderStyle.None;
        input.Dock = DockStyle.Fill;
        field.Controls.Add(input);
        field.Click += (_, _) => input.Focus();
        input.Enter += (_, _) => field.Invalidate();
        input.Leave += (_, _) => field.Invalidate();
        field.Paint += (_, e) =>
        {
            if (field.Width < 4 || field.Height < 4) return;
            using var path = RoundedPath(new Rectangle(1, 1, field.Width - 3, field.Height - 3), field.CornerRadius);
            using var pen = new Pen(input.Focused ? Accent : MixColors(SurfaceAlt, TextColor, .18), input.Focused ? 1.5f : 1f);
            e.Graphics.DrawPath(pen, path);
        };
        return field;
    }

    sealed class PopupToggle(Color accent, Color surface) : CheckBox
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            int top = (Height - 16) / 2;
            using var track = RoundedPath(new Rectangle(2, top, 30, 16), 8);
            using var fill = new SolidBrush(Checked ? accent : surface);
            using var pen = new Pen(Focused ? accent : MixColors(surface, ForeColor, .35));
            using var thumb = new SolidBrush(Checked ? ReadableText(accent) : ForeColor);
            e.Graphics.FillPath(fill, track);
            e.Graphics.DrawPath(pen, track);
            e.Graphics.FillEllipse(thumb, Checked ? 19 : 5, top + 3, 10, 10);
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(41, 0, Math.Max(0, Width - 41), Height), ForeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        }
    }

    sealed class RoundedPopupForm : Form
    {
        readonly System.Windows.Forms.Timer entranceTimer = new() { Interval = 15 };
        readonly System.Diagnostics.Stopwatch entranceClock = new();
        Point entranceDestination;
        int entranceDistance;
        readonly Color accent;

        public RoundedPopupForm(Color accent)
        {
            this.accent = accent;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            DoubleBuffered = true;
            entranceTimer.Tick += (_, _) =>
            {
                double elapsed = entranceClock.Elapsed.TotalMilliseconds;
                double progress = Math.Clamp(elapsed / 150d, 0, 1);
                int offset = (int)Math.Round(entranceDistance * Math.Pow(1 - progress, 3));
                var frame = new Point(entranceDestination.X, entranceDestination.Y + offset);
                if (Location != frame) Location = frame;
                if (progress >= 1)
                {
                    entranceTimer.Stop(); entranceClock.Stop();
                }
            };
        }

        protected override void OnLoad(EventArgs e)
        {
            int inset = (int)Math.Round(9 * DeviceDpi / 96d);
            Padding = new Padding(Math.Max(inset, Padding.Left), Math.Max(inset, Padding.Top),
                Math.Max(inset, Padding.Right), Math.Max(inset, Padding.Bottom));
            base.OnLoad(e);
        }

        protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(BackColor);

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (ClientSize.Width < 5 || ClientSize.Height < 5) return;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var outline = RoundedPath(new Rectangle(1, 1, ClientSize.Width - 3, ClientSize.Height - 3),
                (int)Math.Round(23 * DeviceDpi / 96d));
            using var pen = new Pen(MixColors(BackColor, accent, .65), Math.Max(1.5f, DeviceDpi / 64f));
            e.Graphics.DrawPath(pen, outline);
        }

        protected override void OnShown(EventArgs e)
        {
            if (!AnimatedRoundButton.ReducedMotion)
            {
                entranceDestination = Location;
                entranceDistance = (int)Math.Round(14 * DeviceDpi / 96d);
                Location = new Point(entranceDestination.X, entranceDestination.Y + entranceDistance);
                entranceClock.Restart();
                entranceTimer.Start();
            }
            base.OnShown(e);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (!e.Cancel) { entranceTimer.Stop(); entranceClock.Stop(); }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) entranceTimer.Dispose();
            base.Dispose(disposing);
        }

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
