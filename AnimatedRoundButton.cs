using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    class AnimatedRoundButton : Button
    {
        readonly System.Windows.Forms.Timer motionTimer = new() { Interval = 15 };
        long lastFrame;
        bool hovered, pressed;
        double hoverAmount, pressAmount, focusAmount, pulse = 1;
        Point pulseOrigin;
        Color paintedBackground;
        static readonly bool reducedMotion = ReadReducedMotion();
        public int CornerRadius { get; set; } = 18;
        public Color FocusBorderColor { get; set; } = Color.Empty;

        [DllImport("user32.dll")]
        static extern bool SystemParametersInfo(uint action, uint parameter, ref int value, uint flags);

        static bool ReadReducedMotion()
        {
            int enabled = 1;
            return SystemParametersInfo(0x1042, 0, ref enabled, 0) && enabled == 0;
        }

        public AnimatedRoundButton()
        {
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            paintedBackground = BackColor;
            motionTimer.Tick += (_, _) => AdvanceFrame();
        }

        protected void Animate()
        {
            if (!IsHandleCreated || Parent == null || IsDisposed) return;
            if (!motionTimer.Enabled) { lastFrame = Stopwatch.GetTimestamp(); motionTimer.Start(); }
            Invalidate();
        }

        protected static double Approach(double current, double target, double factor)
        {
            double next = current + (target - current) * factor;
            return Math.Abs(next - target) < 0.002 ? target : next;
        }

        protected static Color Blend(Color current, Color target, double factor) => Color.FromArgb(
            Channel(current.A, target.A, factor), Channel(current.R, target.R, factor),
            Channel(current.G, target.G, factor), Channel(current.B, target.B, factor));

        static int Channel(int current, int target, double factor)
        {
            double next = current + (target - current) * factor;
            return Math.Abs(next - target) < 1 ? target : (int)Math.Round(next);
        }

        protected virtual bool AdvanceExtraMotion(double factor) => false;

        void AdvanceFrame()
        {
            if (IsDisposed || !Visible || Parent == null) { motionTimer.Stop(); return; }
            long now = Stopwatch.GetTimestamp();
            double dt = Math.Clamp((now - lastFrame) / (double)Stopwatch.Frequency, 0, 0.05);
            lastFrame = now;
            double smooth = reducedMotion ? 1 : 1 - Math.Exp(-dt * 22);
            hoverAmount = Approach(hoverAmount, hovered && Enabled ? 1 : 0, smooth);
            pressAmount = Approach(pressAmount, pressed && Enabled ? 1 : 0, reducedMotion ? 1 : 1 - Math.Exp(-dt * 32));
            focusAmount = Approach(focusAmount, Focused && Enabled ? 1 : 0, smooth);
            paintedBackground = Blend(paintedBackground, BackColor, smooth);
            pulse = reducedMotion ? 1 : Math.Min(1, pulse + dt / 0.32);
            bool extra = AdvanceExtraMotion(smooth);
            Invalidate();
            if (!extra && hoverAmount == (hovered && Enabled ? 1 : 0) && pressAmount == (pressed && Enabled ? 1 : 0)
                && focusAmount == (Focused && Enabled ? 1 : 0) && paintedBackground.ToArgb() == BackColor.ToArgb() && pulse == 1)
                motionTimer.Stop();
        }

        void BeginPulse(Point point) { pulseOrigin = point; pulse = 0; Animate(); }
        protected override void OnMouseEnter(EventArgs e) { hovered = true; Animate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; pressed = false; Animate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { pressed = true; BeginPulse(e.Location); }
            base.OnMouseDown(e);
        }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Animate(); base.OnMouseUp(e); }
        protected override void OnMouseCaptureChanged(EventArgs e) { if (!Capture) pressed = false; Animate(); base.OnMouseCaptureChanged(e); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space && !pressed) { pressed = true; BeginPulse(new Point(Width / 2, Height / 2)); }
            base.OnKeyDown(e);
        }
        protected override void OnKeyUp(KeyEventArgs e) { if (e.KeyCode == Keys.Space) pressed = false; Animate(); base.OnKeyUp(e); }
        protected override void OnGotFocus(EventArgs e) { Animate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { pressed = false; Animate(); base.OnLostFocus(e); }
        protected override void OnEnabledChanged(EventArgs e) { if (!Enabled) { pressed = false; hovered = false; } Animate(); base.OnEnabledChanged(e); }
        protected override void OnBackColorChanged(EventArgs e)
        {
            base.OnBackColorChanged(e);
            if (!IsHandleCreated) paintedBackground = BackColor;
            else Animate();
        }
        protected override void OnParentChanged(EventArgs e) { base.OnParentChanged(e); if (Parent == null) motionTimer?.Stop(); }
        protected override void Dispose(bool disposing) { if (disposing) motionTimer.Dispose(); base.Dispose(disposing); }

        protected void ApplyContentMotion(Graphics graphics)
        {
            float scale = (float)(1 - pressAmount * 0.035);
            graphics.TranslateTransform(Width / 2f, Height / 2f + (float)pressAmount);
            graphics.ScaleTransform(scale, scale); graphics.TranslateTransform(-Width / 2f, -Height / 2f);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Parent?.BackColor ?? SystemColors.Control);
            int inset = 1 + (int)Math.Round(pressAmount * 2);
            var bounds = ClientRectangle; bounds.Inflate(-inset, -inset);
            using var path = RoundedPath(bounds, Math.Max(4, CornerRadius - inset + 1));
            var color = Blend(paintedBackground, ControlPaint.Light(paintedBackground, 0.14f), hoverAmount);
            color = Blend(color, ControlPaint.Dark(paintedBackground, 0.16f), pressAmount);
            using (var brush = new SolidBrush(color)) g.FillPath(brush, path);
            if (pulse < 1 && Enabled)
            {
                var state = g.Save(); g.SetClip(path);
                float radius = (float)(Math.Sqrt(Width * (double)Width + Height * (double)Height) * (1 - Math.Pow(1 - pulse, 3)));
                using var pulseBrush = new SolidBrush(Color.FromArgb((int)(32 * (1 - pulse)), ForeColor));
                g.FillEllipse(pulseBrush, pulseOrigin.X - radius, pulseOrigin.Y - radius, radius * 2, radius * 2); g.Restore(state);
            }
            if (focusAmount > 0)
            {
                using var pen = new Pen(Color.FromArgb((int)(200 * focusAmount), FocusBorderColor.IsEmpty ? ForeColor : FocusBorderColor), 1.2f);
                g.DrawPath(pen, path);
            }
            var textRect = ClientRectangle;
            if (this is NavIconButton) { textRect.X += 34; textRect.Width -= 38; }
            textRect.Offset(0, (int)Math.Round(pressAmount));
            TextRenderer.DrawText(g, Text, Font, textRect, Enabled ? ForeColor : SystemColors.GrayText,
                (this is NavIconButton ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter) | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        }
    }
}
