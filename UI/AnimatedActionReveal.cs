using System.Diagnostics;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    // Animate the reserved space as well as the button so the surrounding cards
    // resize smoothly instead of jumping when selection actions appear.
    sealed class AnimatedActionReveal : Panel
    {
        readonly AnimatedRoundButton button;
        readonly System.Windows.Forms.Timer timer = new() { Interval = 15 };
        readonly Action<int>? reserveHeight;
        readonly int buttonHeight, topGap;
        long started;
        double amount, from, extent, fromExtent;
        bool shown;

        public AnimatedActionReveal(AnimatedRoundButton button, int buttonHeight, int topGap, Action<int>? reserveHeight = null)
        {
            this.button = button; this.buttonHeight = buttonHeight; this.topGap = topGap; this.reserveHeight = reserveHeight;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Margin = Padding.Empty; Height = 0; TabStop = false;
            button.Dock = DockStyle.None; button.Margin = Padding.Empty;
            button.HasRevealAnimation = true; button.RevealTextAmount = 0;
            button.RevealAmount = 0; button.Visible = false; button.Enabled = false; button.TabStop = false;
            Controls.Add(button);
            timer.Tick += (_, _) => Advance();
        }

        public void SetShown(bool value, bool animate = true)
        {
            if (IsDisposed) return;
            if (shown == value && animate) return;
            shown = value;
            if (!value) { button.Enabled = false; button.TabStop = false; }
            if (!animate || AnimatedRoundButton.ReducedMotion)
            {
                timer.Stop(); amount = extent = value ? 1 : 0; ApplyAmount(); return;
            }
            from = amount; fromExtent = extent; started = Stopwatch.GetTimestamp(); timer.Start();
        }

        void Advance()
        {
            if (IsDisposed || button.IsDisposed) { timer.Stop(); return; }
            double t = Math.Clamp(Stopwatch.GetElapsedTime(started).TotalMilliseconds / (shown ? 340 : 200), 0, 1);
            double easing = shown ? 1 + 2.3 * Math.Pow(t - 1, 3) + 1.3 * Math.Pow(t - 1, 2) : 1 - Math.Pow(1 - t, 3);
            amount = from + ((shown ? 1 : 0) - from) * easing;
            // The layout moves monotonically; only the painted shape rebounds.
            // Native lists must not resize back and forth with the spring.
            extent = fromExtent + ((shown ? 1 : 0) - fromExtent) * (1 - Math.Pow(1 - t, 3));
            if (t >= 1) { amount = extent = shown ? 1 : 0; timer.Stop(); }
            ApplyAmount();
        }

        void ApplyAmount()
        {
            int height = (int)Math.Round((buttonHeight + topGap) * Math.Clamp(extent, 0, 1));
            if (reserveHeight != null) reserveHeight(height);
            else if (Height != height) Height = height;
            button.RevealAmount = Math.Clamp(amount, 0, 1);
            button.RevealExpansion = amount;
            // Do not reveal cropped glyphs while the parent row is still short.
            double textFits = Math.Clamp((buttonHeight + button.Font.Height + 2d) / (2 * (buttonHeight + topGap)), 0, 0.95);
            button.RevealTextAmount = Math.Clamp((extent - textFits) / (1 - textFits), 0, 1);
            button.Visible = amount > 0.005;
            button.Enabled = shown && amount >= 0.95;
            button.TabStop = button.Enabled;
            PositionButton();
        }

        void PositionButton()
        {
            // Keep the button's native height and screen baseline unchanged.
            // As its parent row grows upward, this bottom anchor compensates.
            button.SetBounds(0, ClientSize.Height - buttonHeight, Math.Max(1, ClientSize.Width), buttonHeight);
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); if (button != null) PositionButton(); }
        protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }
    }

    sealed class BufferedActionLayout : TableLayoutPanel
    {
        public BufferedActionLayout() { DoubleBuffered = true; }
        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                // Composite native list and label descendants with their parent.
                parameters.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
                return parameters;
            }
        }
    }
}
