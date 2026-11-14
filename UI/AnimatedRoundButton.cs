using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    class AnimatedRoundButton : Button
    {
        readonly System.Windows.Forms.Timer motionTimer = new() { Interval = 15 };
        System.Windows.Forms.Timer? mouseOutlineTimer;
        bool pointerFocus, pointerOutline, pointerDown;
        long lastFrame;
        bool hovered, pressed;
        double hoverAmount, pressAmount, focusAmount, pulse = 1;
        double compression, compressionVelocity;
        Point pulseOrigin;
        Color paintedBackground;
        static readonly bool reducedMotion = ReadReducedMotion();
        internal static bool AnimationsEnabled { get; set; } = true;
        internal static bool AdvancedAnimationsEnabled { get; set; } = true;
        internal static bool ReducedMotion => reducedMotion || !AnimationsEnabled;
        PointF shinePoint;
        float shineStrength;

        internal void SetProximityShine(Point point, bool active)
        {
            float dx = point.X - Math.Clamp(point.X, 0, Width);
            float dy = point.Y - Math.Clamp(point.Y, 0, Height);
            float strength = active && Visible && Enabled ? Math.Max(0, 1 - MathF.Sqrt(dx * dx + dy * dy) / 48) : 0;
            if (strength == 0 && shineStrength == 0 || strength == shineStrength && shinePoint == point) return;
            shineStrength = strength;
            shinePoint = point;
            Invalidate();
        }
        double revealAmount = 1;
        public double RevealAmount { get => revealAmount; set { revealAmount = Math.Clamp(value, 0, 1); Invalidate(); } }
        double revealExpansion = 1;
        public double RevealExpansion { get => revealExpansion; set { revealExpansion = Math.Clamp(value, 0, 1.08); Invalidate(); } }
        internal bool HasRevealAnimation { get; set; }
        double revealTextAmount = 1;
        public double RevealTextAmount { get => revealTextAmount; set { revealTextAmount = Math.Clamp(value, 0, 1); Invalidate(); } }
        public int CornerRadius { get; set; } = 18;
        public Color FocusBorderColor { get; set; } = Color.Empty;
        public string? LeadingIcon { get; set; }
        public bool MonochromeIcon { get; set; }
        public bool Outline { get; set; }
        protected double HoverAmount => hoverAmount;

        [DllImport("user32.dll")]
        static extern bool SystemParametersInfo(uint action, uint parameter, ref int value, uint flags);

        static bool ReadReducedMotion()
        {
            int enabled = 1;
            return SystemParametersInfo(0x1042, 0, ref enabled, 0) && enabled == 0;
        }

        public AnimatedRoundButton()
        {
            Cursor = Cursors.Hand;
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
            bool instant = ReducedMotion;
            double smooth = instant ? 1 : 1 - Math.Exp(-dt * 22);
            hoverAmount = Approach(hoverAmount, hovered && Enabled ? 1 : 0, smooth);
            pressAmount = Approach(pressAmount, pressed && Enabled ? 1 : 0, instant ? 1 : 1 - Math.Exp(-dt * 32));
            double focusTarget = Focused && Enabled && (!pointerFocus || pointerOutline) ? 1 : 0;
            focusAmount = Approach(focusAmount, focusTarget, smooth);
            paintedBackground = Blend(paintedBackground, BackColor, smooth);
            pulse = instant ? 1 : Math.Min(1, pulse + dt / 0.28);
            // A damped spring gives a small rebound without changing layout bounds.
            double target = pressed && Enabled && !instant ? 1 : 0;
            if (instant) { compression = 0; compressionVelocity = 0; }
            else
            {
                int steps = Math.Max(1, (int)Math.Ceiling(dt / 0.008));
                double step = dt / steps;
                for (int i = 0; i < steps; i++)
                {
                    compressionVelocity += ((target - compression) * 620 - compressionVelocity * 29) * step;
                    compression += compressionVelocity * step;
                }
                if (Math.Abs(compression - target) < 0.002 && Math.Abs(compressionVelocity) < 0.025)
                { compression = target; compressionVelocity = 0; }
            }
            bool extra = AdvanceExtraMotion(smooth);
            Invalidate();
            if (!extra && hoverAmount == (hovered && Enabled ? 1 : 0) && pressAmount == (pressed && Enabled ? 1 : 0)
                && focusAmount == focusTarget && compression == target && compressionVelocity == 0
                && paintedBackground.ToArgb() == BackColor.ToArgb() && pulse == 1)
                motionTimer.Stop();
        }

        protected void BeginPulse(Point point) { pulseOrigin = point; pulse = ReducedMotion ? 1 : 0; Animate(); }
        protected override void OnClick(EventArgs e) { PlayClick(this); base.OnClick(e); }
        protected override void OnMouseEnter(EventArgs e) { hovered = true; Animate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; pressed = false; Animate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                pointerDown = pointerFocus = pointerOutline = pressed = true;
                mouseOutlineTimer?.Stop();
                if (!ReducedMotion) compressionVelocity = Math.Min(18, compressionVelocity + 9);
                BeginPulse(e.Location);
            }
            base.OnMouseDown(e);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (pressed && !ReducedMotion) { compression = Math.Max(compression, 0.55); compressionVelocity = Math.Min(compressionVelocity, 0); }
            if (e.Button == MouseButtons.Left) ReleasePointerOutline();
            pressed = false; Animate(); base.OnMouseUp(e);
        }
        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            if (!Capture) { pressed = false; ReleasePointerOutline(); }
            Animate();
            base.OnMouseCaptureChanged(e);
        }

        void ReleasePointerOutline()
        {
            if (!pointerDown) return;
            pointerDown = false;
            if (mouseOutlineTimer == null)
            {
                mouseOutlineTimer = new System.Windows.Forms.Timer { Interval = 2000 };
                mouseOutlineTimer.Tick += (_, _) =>
                {
                    mouseOutlineTimer.Stop();
                    pointerOutline = false;
                    Animate();
                };
            }
            mouseOutlineTimer.Start();
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            pointerFocus = false;
            Animate();
            if (e.KeyCode == Keys.Space && !pressed) { pressed = true; BeginPulse(new Point(Width / 2, Height / 2)); }
            base.OnKeyDown(e);
        }
        protected override void OnKeyUp(KeyEventArgs e) { if (e.KeyCode == Keys.Space) pressed = false; Animate(); base.OnKeyUp(e); }
        protected override void OnGotFocus(EventArgs e) { Animate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e)
        {
            pointerDown = pointerFocus = pointerOutline = pressed = false;
            mouseOutlineTimer?.Stop();
            Animate();
            base.OnLostFocus(e);
        }
        protected override void OnEnabledChanged(EventArgs e) { if (!Enabled) { pressed = false; hovered = false; } Animate(); base.OnEnabledChanged(e); }
        protected override void OnBackColorChanged(EventArgs e)
        {
            base.OnBackColorChanged(e);
            if (!IsHandleCreated) paintedBackground = BackColor;
            else Animate();
        }
        protected override void OnParentChanged(EventArgs e) { base.OnParentChanged(e); if (Parent == null) motionTimer?.Stop(); }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { motionTimer.Dispose(); mouseOutlineTimer?.Dispose(); }
            base.Dispose(disposing);
        }

        protected void ApplyContentMotion(Graphics graphics)
        {
            float amount = (float)Math.Clamp(compression, -0.25, 1.2);
            graphics.TranslateTransform(Width / 2f, Height / 2f + amount * 1.3f);
            graphics.ScaleTransform(1 - amount * 0.035f, 1 - amount * 0.075f);
            graphics.TranslateTransform(-Width / 2f, -Height / 2f);
        }

        protected void ApplyIconMotion(Graphics graphics, RectangleF bounds)
        {
            if (ReducedMotion || !AdvancedAnimationsEnabled) return;
            float x = bounds.X + bounds.Width / 2, y = bounds.Y + bounds.Height / 2;
            float bounce = pulse < 1 ? (float)(Math.Sin(pulse * Math.PI * 2) * (1 - pulse)) : 0;
            graphics.TranslateTransform(x, y - (float)hoverAmount * 1.2f);
            graphics.RotateTransform(bounce * 15);
            float scale = 1 + (float)hoverAmount * .07f + bounce * .13f;
            graphics.ScaleTransform(scale, scale);
            graphics.TranslateTransform(-x, -y);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Parent?.BackColor ?? SystemColors.Control);
            if (revealAmount <= 0.005) return;
            float amount = (float)Math.Clamp(compression, -0.25, 1.2);
            float insetX = Math.Max(0.5f, 1 + amount * 2.5f), insetY = Math.Max(0.5f, 1 + amount * 1.2f);
            float shapeWidth = Math.Min(Width - 1, (Width - insetX * 2) * (float)(0.90 + 0.10 * revealExpansion));
            float shapeHeight = Math.Min(Height - 1, (Height - insetY * 2) * (float)(0.60 + 0.40 * revealExpansion));
            using var path = SoftIcons.Round((Width - shapeWidth) / 2, (Height - shapeHeight) / 2,
                Math.Max(1, shapeWidth), Math.Max(1, shapeHeight),
                Math.Max(4, CornerRadius - amount * 6));
            var color = Blend(paintedBackground, ControlPaint.Light(paintedBackground, 0.14f), hoverAmount);
            color = Blend(color, Blend(paintedBackground, ForeColor, .12), pressAmount);
            color = Blend(Parent?.BackColor ?? BackColor, color, revealAmount);
            using (var brush = new SolidBrush(color)) g.FillPath(brush, path);
            if (shineStrength > 0)
            {
                const float radius = 105;
                using var glow = new GraphicsPath();
                glow.AddEllipse(shinePoint.X - radius, shinePoint.Y - radius, radius * 2, radius * 2);
                using var highlight = new PathGradientBrush(glow)
                {
                    CenterColor = Color.FromArgb((int)(48 * shineStrength * (1 - .8 * pressAmount)), Color.White),
                    SurroundColors = [Color.FromArgb(0, Color.White)]
                };
                var clip = g.Save();
                g.SetClip(glow);
                g.FillPath(highlight, path);
                g.Restore(clip);
            }
            if (pulse < 1 && Enabled)
            {
                var state = g.Save(); g.SetClip(path);
                float radius = (float)(Math.Sqrt(Width * (double)Width + Height * (double)Height) * (1 - Math.Pow(1 - pulse, 3)));
                using var pulseBrush = new SolidBrush(Color.FromArgb((int)(24 * Math.Pow(1 - pulse, 1.5)), ForeColor));
                g.FillEllipse(pulseBrush, pulseOrigin.X - radius, pulseOrigin.Y - radius, radius * 2, radius * 2); g.Restore(state);
            }
            if (Outline || focusAmount > 0)
            {
                int opacity = Math.Max(Outline ? 100 : 0, (int)(200 * focusAmount));
                using var pen = new Pen(Color.FromArgb(opacity, FocusBorderColor.IsEmpty ? ForeColor : FocusBorderColor), 1.2f);
                g.DrawPath(pen, path);
            }
            var textRect = ClientRectangle;
            if (this is SymbolButton && Text.Length > 0) textRect.Width = Math.Max(0, Width - 34);
            if (this is SoftSelector) { textRect.X += 14; textRect.Width -= 52; }
            if (this is NavIconButton) { textRect.X += 42; textRect.Width -= 46; }
            else if (LeadingIcon != null)
            {
                int size = Height >= 48 ? 24 : 18;
                int textWidth = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;
                int x = Math.Max(10, (Width - textWidth - size - 8) / 2);
                var iconState = g.Save(); ApplyContentMotion(g);
                var iconColor = MonochromeIcon && FindForm() is MainForm owner
                    ? owner.lightMode ? Color.Black : Color.White : ForeColor;
                var bounds = new RectangleF(x, (Height - size) / 2f, size, size);
                ApplyIconMotion(g, bounds);
                SoftIcons.Draw(g, LeadingIcon, bounds, iconColor, !MonochromeIcon);
                g.Restore(iconState);
                textRect.X = x + size + 8; textRect.Width = Math.Max(0, Width - textRect.X - 10);
            }
            // Rasterize glyphs on a fixed pixel grid: spring-scale only the shape
            // and icons, never the text, so ClearType does not shimmer each frame.
            double textVisibility = HasRevealAnimation ? revealTextAmount : revealAmount;
            var textColor = Blend(color,
                Enabled || HasRevealAnimation ? ForeColor : Blend(BackColor, ForeColor, 0.45),
                textVisibility);
            if (textVisibility > 0.005)
                TextRenderer.DrawText(g, Text, Font, textRect, textColor,
                    (this is NavIconButton || this is SoftSelector || LeadingIcon != null ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter)
                    | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
    }
}
