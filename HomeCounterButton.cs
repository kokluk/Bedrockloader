using System.Drawing.Drawing2D;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    static int CountCinnabarResourcePacks(string directory)
    {
        try
        {
            if (!Directory.Exists(directory)) return 0;
            return Directory.EnumerateFileSystemEntries(directory).Count(path =>
                Directory.Exists(path) ? File.Exists(Path.Combine(path, "manifest.json")) :
                File.Exists(path) && (Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase) ||
                Path.GetExtension(path).Equals(".mcpack", StringComparison.OrdinalIgnoreCase)));
        }
        catch (IOException) { return 0; }
        catch (UnauthorizedAccessException) { return 0; }
    }

    sealed class HomeCounterButton : AnimatedRoundButton
    {
        readonly MainForm owner;
        readonly string count, title, symbol;
        readonly Action? activate;
        readonly System.Windows.Forms.Timer navigation = new() { Interval = 150 };
        readonly Font countFont = new("Segoe UI", 26, FontStyle.Bold);
        readonly Font titleFont = new("Segoe UI", 9);

        public HomeCounterButton(MainForm owner, int value, string title, string symbol, Action? activate)
        {
            this.owner = owner;
            count = value.ToString();
            this.title = title;
            this.symbol = symbol;
            this.activate = activate;
            AccessibleName = $"{title}: {count}";
            TabStop = activate != null;
            if (activate == null) Cursor = Cursors.Default;
            navigation.Tick += (_, _) =>
            {
                navigation.Stop();
                if (Visible && FindForm() == owner && owner.currentPage == "Home") activate?.Invoke();
            };
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (activate == null) return;
            if (ReducedMotion) activate();
            else navigation.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            var state = g.Save();
            ApplyContentMotion(g);
            var disk = new RectangleF(Width - 58, (Height - 40) / 2f, 40, 40);
            Color accent = owner.Accent;
            Color tint = MixColors(BackColor, accent, .16);
            using (var fill = new SolidBrush(tint)) g.FillEllipse(fill, disk);
            var icon = new RectangleF(disk.X + 8, disk.Y + 8, 24, 24);
            ApplyIconMotion(g, icon);
            SoftIcons.Draw(g, symbol, icon, accent, true);
            g.Restore(state);

            int width = Math.Max(0, Width - 75);
            var flags = TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;
            TextRenderer.DrawText(g, count, countFont, new Rectangle(16, Math.Max(2, Height / 2 - 35), width, 43), ForeColor, flags);
            TextRenderer.DrawText(g, title, titleFont, new Rectangle(16, Height / 2 + 10, width, 23), owner.Muted, flags);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                navigation.Dispose();
                countFont.Dispose();
                titleFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
