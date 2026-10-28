using System.Drawing.Drawing2D;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    sealed class ToggleContextMenu : ContextMenuStrip
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

    // A painted selector avoids the system ComboBox's unthemed arrow surface.
    sealed class SoftSelector : AnimatedRoundButton
    {
        readonly MainForm owner;
        readonly ToggleContextMenu menu = new() { ShowImageMargin = false, ShowCheckMargin = false };
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
        public SoftSelector(MainForm owner)
        {
            this.owner = owner;
            AccessibleRole = AccessibleRole.ComboBox;
            menu.ToggleOwner = this;
            menu.Opened += (_, _) => Invalidate();
            menu.Closed += (_, _) => Invalidate();
            menu.Renderer = new SoftMenuRenderer(() => owner.SurfaceAlt, () => owner.TextColor, () => owner.Accent);
        }
        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (menu.Visible) { menu.Close(); return; }
            menu.Items.Clear(); menu.BackColor = owner.SurfaceAlt; menu.ForeColor = owner.TextColor; menu.Font = Font;
            menu.MinimumSize = new Size(Width, 0);
            for (int i = 0; i < Items.Count; i++)
            {
                int index = i;
                var item = new ToolStripMenuItem(Items[i].ToString()) { AutoSize = false, Size = new Size(Math.Max(Width - 4, 120), 36) };
                item.Click += (_, _) => { SelectedIndex = index; Focus(); };
                menu.Items.Add(item);
            }
            menu.Show(this, new Point(0, Height + 4));
            if (selectedIndex >= 0) menu.Items[selectedIndex].Select();
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
            e.Graphics.DrawLines(pen, new[] { new PointF(x - 4, y - 2 * direction), new PointF(x, y + 2 * direction), new PointF(x + 4, y - 2 * direction) });
        }
        protected override void Dispose(bool disposing) { if (disposing) menu.Dispose(); base.Dispose(disposing); }
    }
}
