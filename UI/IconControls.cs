using System.Drawing.Drawing2D;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    sealed class SymbolButton : AnimatedRoundButton
    {
        public string Symbol { get; set; } = "settings";
        double spin;
        public void Spin()
        {
            if (ReducedMotion) return;
            spin = 0.001;
            Animate();
        }
        protected override bool AdvanceExtraMotion(double factor)
        {
            if (spin == 0) return false;
            spin = Approach(spin, 1, factor * 0.55);
            if (spin == 1) spin = 0;
            return spin != 0;
        }
        bool showUpdateBadge;
        public bool ShowUpdateBadge { get => showUpdateBadge; set { showUpdateBadge = value; Invalidate(); } }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics; var iconState = g.Save();
            ApplyContentMotion(g);
            float size = 22 + (float)HoverAmount;
            float center = Text.Length == 0 ? Width / 2f : Width - 21;
            if (spin != 0)
            {
                g.TranslateTransform(center, Height / 2f);
                g.RotateTransform((float)(spin * 360));
                g.TranslateTransform(-center, -Height / 2f);
            }
            string key = Symbol == "maximize" && FindForm()?.WindowState == FormWindowState.Maximized ? "restore" : Symbol;
            var bounds = new RectangleF(center - size / 2, (Height - size) / 2, size, size);
            ApplyIconMotion(g, bounds);
            SoftIcons.Draw(g, key, bounds, ForeColor, true);
            g.Restore(iconState);
            if (ShowUpdateBadge)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var badge = new SolidBrush(Color.FromArgb(220, 48, 60)); g.FillEllipse(badge, 0, 0, 16, 16);
                using var font = LauncherFont.Create(8, FontStyle.Bold);
                TextRenderer.DrawText(g, "1", font, new Rectangle(0, 0, 16, 16), Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }
    }

    sealed class NavIconButton : AnimatedRoundButton
    {
        public string IconKey { get; set; } = "Home";
        public bool IsActive { get; set; }
        protected override void OnClick(EventArgs e)
        {
            BeginPulse(new Point(Width / 2, Height / 2));
            base.OnClick(e);
        }
        Color iconColor = Color.LightGray, paintedIcon = Color.LightGray;
        public Color IconColor
        {
            get => iconColor;
            set { iconColor = value; if (!IsHandleCreated) paintedIcon = value; else Animate(); }
        }
        protected override bool AdvanceExtraMotion(double factor)
        {
            paintedIcon = Blend(paintedIcon, iconColor, factor);
            return paintedIcon.ToArgb() != iconColor.ToArgb();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var state = e.Graphics.Save(); ApplyContentMotion(e.Graphics);
            float size = 23 + (float)HoverAmount;
            var bounds = new RectangleF(9, (Height - size) / 2, size, size);
            ApplyIconMotion(e.Graphics, bounds);
            SoftIcons.Draw(e.Graphics, IconKey, bounds, paintedIcon, IsActive);
            e.Graphics.Restore(state);
        }
    }

    sealed class HeadingGlyph : Control
    {
        readonly MainForm owner;
        readonly string symbol;
        readonly System.Windows.Forms.Timer timer = new() { Interval = 15 };
        readonly System.Diagnostics.Stopwatch clock = new();

        public HeadingGlyph(MainForm owner, string symbol)
        {
            this.owner = owner;
            this.symbol = symbol;
            Location = new Point(1, 1);
            Size = new Size(26, 26);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            timer.Tick += (_, _) =>
            {
                if (clock.ElapsedMilliseconds >= 220 || !Visible) timer.Stop();
                Invalidate();
            };
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible && !AnimatedRoundButton.ReducedMotion && AnimatedRoundButton.AdvancedAnimationsEnabled)
            { clock.Restart(); timer.Start(); }
            else timer.Stop();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? owner.Surface);
            float amount = timer.Enabled ? 1 - Math.Clamp(clock.ElapsedMilliseconds / 220f, 0, 1) : 0;
            float size = 23 - 5 * amount;
            SoftIcons.Draw(e.Graphics, symbol, new RectangleF((Width - size) / 2, (Height - size) / 2 + 2 * amount, size, size), owner.Accent, true);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Stop();
            base.Dispose(disposing);
            if (disposing) timer.Dispose();
        }
    }

    sealed class SoftStatusLabel : Label
    {
        Color iconColor = Color.LightGray;
        public Color IconColor { get => iconColor; set { iconColor = value; Invalidate(); } }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            int width = Math.Min(Math.Max(0, Width - Padding.Right), TextRenderer.MeasureText(Text, Font).Width + 44);
            if (width < 42 || Height < 10) return;
            int height = Math.Min(32, Height);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pill = SoftIcons.Round(0, (Height - height) / 2f, width, height, height / 2f);
            using var fill = new SolidBrush(MixColors(BackColor, IconColor, .12));
            e.Graphics.FillPath(fill, pill);
            SoftIcons.Draw(e.Graphics, "status", new RectangleF(9, (Height - 17) / 2f, 17, 17), IconColor, true);
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(33, 0, width - 41, Height), ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }
}
