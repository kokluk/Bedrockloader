using System.Drawing.Drawing2D;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    sealed class SymbolButton : AnimatedRoundButton
    {
        public string Symbol { get; set; } = "settings";
        bool showUpdateBadge;
        public bool ShowUpdateBadge { get => showUpdateBadge; set { showUpdateBadge = value; Invalidate(); } }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics; var iconState = g.Save();
            ApplyContentMotion(g);
            float size = 22 + (float)HoverAmount;
            string key = Symbol == "maximize" && FindForm()?.WindowState == FormWindowState.Maximized ? "restore" : Symbol;
            SoftIcons.Draw(g, key, new RectangleF((Width - size) / 2, (Height - size) / 2, size, size), ForeColor, true);
            g.Restore(iconState);
            if (ShowUpdateBadge)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var badge = new SolidBrush(Color.FromArgb(220, 48, 60)); g.FillEllipse(badge, 0, 0, 16, 16);
                using var font = new Font("Segoe UI", 8, FontStyle.Bold);
                TextRenderer.DrawText(g, "1", font, new Rectangle(0, 0, 16, 16), Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }
    }

    sealed class NavIconButton : AnimatedRoundButton
    {
        public string IconKey { get; set; } = "Home";
        public bool IsActive { get; set; }
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
            SoftIcons.Draw(e.Graphics, IconKey, new RectangleF(9, (Height - size) / 2, size, size), paintedIcon, IsActive);
            e.Graphics.Restore(state);
        }
    }

    sealed class SoftStatusLabel : Label
    {
        Color iconColor = Color.LightGray;
        public Color IconColor { get => iconColor; set { iconColor = value; Invalidate(); } }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            SoftIcons.Draw(e.Graphics, "status", new RectangleF(0, (Height - 17) / 2f, 17, 17), IconColor, true);
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(24, 0, Math.Max(0, Width - 24), Height), ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }
}
