using System.Drawing.Drawing2D;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    class ToggleContextMenu : ContextMenuStrip
    {
        public Control? ToggleOwner { get; set; }
        protected override void OnClosing(ToolStripDropDownClosingEventArgs e)
        {
            // WinForms dismisses popups on mouse-down, before the owner's Click.
            // Keep it open for that one mouse-down so Click can close it once.
            if (e.CloseReason == ToolStripDropDownCloseReason.AppClicked
                && (Control.MouseButtons & MouseButtons.Left) != 0
                && ToggleOwner is { IsDisposed: false, Visible: true } trigger
                && trigger.RectangleToScreen(trigger.ClientRectangle).Contains(Cursor.Position))
                e.Cancel = true;
            base.OnClosing(e);
        }
    }

    sealed class SelectorMenu : ToggleContextMenu
    {
        internal const int Radius = 22;
        public SelectorMenu()
        {
            DropShadowEnabled = false;
            Padding = new Padding(1, 0, 1, 7);
            reveal.Tick += (_, _) =>
            {
                double t = Math.Clamp(System.Diagnostics.Stopwatch.GetElapsedTime(openedAt).TotalMilliseconds / 190, 0, 1);
                UpdateRegion(closing ? 1 - t : 1 - Math.Pow(1 - t, 3));
                if (t >= 1)
                {
                    reveal.Stop();
                    if (closing) Close();
                }
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) reveal.Dispose();
            base.Dispose(disposing);
        }
        readonly System.Windows.Forms.Timer reveal = new() { Interval = 15 };
        long openedAt;
        bool closing;
        Action? afterClose;
        public bool RevealFromBottom { get; init; }

        public void SelectAndClose(Action action)
        {
            if (Visible) afterClose = action;
            else action();
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            if (AnimatedRoundButton.ReducedMotion) return;
            openedAt = System.Diagnostics.Stopwatch.GetTimestamp();
            UpdateRegion(0);
            reveal.Start();
        }

        protected override void OnClosed(ToolStripDropDownClosedEventArgs e)
        {
            closing = false;
            reveal.Stop();
            UpdateRegion(1);
            base.OnClosed(e);
            var action = afterClose;
            afterClose = null;
            action?.Invoke();
        }

        protected override void OnClosing(ToolStripDropDownClosingEventArgs e)
        {
            base.OnClosing(e);
            if (e.Cancel || closing || AnimatedRoundButton.ReducedMotion ||
                e.CloseReason is not (ToolStripDropDownCloseReason.ItemClicked or ToolStripDropDownCloseReason.CloseCalled or ToolStripDropDownCloseReason.Keyboard or ToolStripDropDownCloseReason.AppClicked)) return;
            e.Cancel = true;
            closing = true;
            openedAt = System.Diagnostics.Stopwatch.GetTimestamp();
            reveal.Start();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            UpdateRegion(1);
        }

        void UpdateRegion(double amount)
        {
            if (Width < 2 || Height < 2) return;
            int initialHeight = Math.Min(closing ? 2 : 42, Height);
            int height = initialHeight + (int)Math.Round((Height - initialHeight) * amount);
            int width = Math.Max(2, (int)Math.Round(Width * (.92 + .08 * amount)));
            using var path = RoundedPath(new Rectangle((Width - width) / 2, RevealFromBottom ? Height - height : 0, width, height),
                Math.Min(Radius, height / 2));
            var previous = Region;
            Region = new Region(path);
            previous?.Dispose();
        }
    }

    sealed class SelectorMenuRenderer(MainForm owner) : ToolStripProfessionalRenderer
    {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) => e.Graphics.Clear(owner.SurfaceAlt);
        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }
        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e) { }
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = RoundedPath(new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1), SelectorMenu.Radius);
            using var pen = new Pen(MixColors(owner.SurfaceAlt, owner.Accent, 0.6), 1);
            e.Graphics.DrawPath(pen, path);
        }
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            bool header = e.Item.Tag is string;
            if (!header && (e.Item.Selected || e.Item is ToolStripMenuItem { Checked: true }))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundedPath(new Rectangle(6, 2, Math.Max(1, e.Item.Width - 12), e.Item.Height - 4), 12);
                using var fill = new SolidBrush(MixColors(owner.SurfaceAlt, owner.Accent, e.Item.Selected ? 0.22 : 0.13));
                e.Graphics.FillPath(fill, path);
            }
            if (header)
            {
                using var pen = new Pen(MixColors(owner.SurfaceAlt, owner.TextColor, 0.12));
                e.Graphics.DrawLine(pen, 14, e.Item.Height - 1, e.Item.Width - 14, e.Item.Height - 1);
            }
        }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            string? icon = (e.Item.Tag as SelectorItemIcon)?.Key;
            int left = icon == null ? 14 : 38;
            if (icon != null) SoftIcons.Draw(e.Graphics, icon, new RectangleF(14, (e.Item.Height - 17) / 2f, 17, 17), owner.Accent);
            TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont,
                new Rectangle(left, 0, Math.Max(0, e.Item.Width - left - 40), e.Item.Height), owner.TextColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (e.Item.Tag is string)
            {
                using var pen = new Pen(owner.TextColor, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                float x = e.Item.Width - 24, y = e.Item.Height / 2f;
                e.Graphics.DrawLines(pen, new[] { new PointF(x - 4, y + 2), new PointF(x, y - 2), new PointF(x + 4, y + 2) });
            }
            else if (e.Item is ToolStripMenuItem { Checked: true })
                SoftIcons.Draw(e.Graphics, "check-mark", new RectangleF(e.Item.Width - 34, (e.Item.Height - 18) / 2f, 18, 18), owner.Accent);
        }
    }

    sealed record SelectorItemIcon(string Key);

    // A painted selector avoids the system ComboBox's unthemed arrow surface.
    sealed class SoftSelector : AnimatedRoundButton
    {
        readonly MainForm owner;
        readonly SelectorMenu menu = new() { ShowImageMargin = false, ShowCheckMargin = false };
        int selectedIndex = -1;
        public List<object> Items { get; } = new();
        public event EventHandler? SelectedIndexChanged;
        public int SelectedIndex
        {
            get => selectedIndex;
            set
            {
                int next = value >= 0 && value < Items.Count ? value : -1;
                if (next == selectedIndex) return;
                selectedIndex = next; Text = SelectedItem?.ToString() ?? "";
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
        }
        public object? SelectedItem { get => selectedIndex < 0 ? null : Items[selectedIndex]; set => SelectedIndex = Items.IndexOf(value!); }
        public string[]? ItemIcons { get; set; }
        public SoftSelector(MainForm owner)
        {
            this.owner = owner;
            AccessibleRole = AccessibleRole.ComboBox;
            menu.ToggleOwner = this;
            menu.Opened += (_, _) => Invalidate();
            menu.Closed += (_, _) => Invalidate();
            menu.Renderer = new SelectorMenuRenderer(owner);
        }
        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (menu.Visible) { menu.Close(); return; }
            menu.Items.Clear(); menu.BackColor = owner.SurfaceAlt; menu.ForeColor = owner.TextColor; menu.Font = Font;
            menu.MinimumSize = new Size(Width, 0);
            menu.MaximumSize = new Size(Width, 0);
            var header = new ToolStripMenuItem(Text) { Tag = "selector-header", AutoSize = false, Size = new Size(Math.Max(Width - 2, 120), Height) };
            header.Click += (_, _) => { menu.Close(); Focus(); };
            menu.Items.Add(header);
            for (int i = 0; i < Items.Count; i++)
            {
                int index = i;
                var item = new ToolStripMenuItem(Items[i].ToString()) { AutoSize = false, Size = new Size(Math.Max(Width - 2, 120), 38), Checked = i == selectedIndex };
                if (ItemIcons is { } icons && i < icons.Length) item.Tag = new SelectorItemIcon(icons[i]);
                item.Click += (_, _) => menu.SelectAndClose(() => { if (!IsDisposed) { Focus(); SelectedIndex = index; } });
                menu.Items.Add(item);
            }
            // The selected header and options share one surface over the trigger.
            menu.Show(this, Point.Empty);
            if (selectedIndex >= 0) menu.Items[selectedIndex + 1].Select();
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)
            {
                if (e.Alt) OnClick(EventArgs.Empty);
                else SelectedIndex = Math.Clamp(selectedIndex + (e.KeyCode == Keys.Down ? 1 : -1), 0, Math.Max(0, Items.Count - 1));
                e.Handled = true; e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.F4) { OnClick(EventArgs.Empty); e.Handled = true; }
            else base.OnKeyDown(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(ForeColor, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            float x = Width - 24, y = Height / 2f;
            float direction = menu.Visible ? -1 : 1;
            var state = e.Graphics.Save();
            ApplyIconMotion(e.Graphics, new RectangleF(x - 8, y - 8, 16, 16));
            e.Graphics.DrawLines(pen, new[] { new PointF(x - 4, y - 2 * direction), new PointF(x, y + 2 * direction), new PointF(x + 4, y - 2 * direction) });
            e.Graphics.Restore(state);
        }
        protected override void Dispose(bool disposing) { if (disposing) menu.Dispose(); base.Dispose(disposing); }
    }
}
