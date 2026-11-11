using System.Drawing.Drawing2D;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    sealed class ThemedScrollPanel : Panel
    {
        const int Gutter = 16;
        readonly Control content;
        readonly ScrollThumb scrollbar;
        int offset, wheelRemainder;
        int MaximumOffset => Math.Max(0, content.Height - ClientSize.Height);

        public ThemedScrollPanel(MainForm owner, Control content)
        {
            this.content = content;
            scrollbar = new ScrollThumb(owner, this);
            Controls.Add(content);
            Controls.Add(scrollbar);
            content.SizeChanged += (_, _) => Arrange();
            TrackFocus(content);
            Arrange();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (scrollbar != null) Arrange();
        }

        void Arrange()
        {
            bool overflow = MaximumOffset > 0;
            scrollbar.Visible = overflow;
            scrollbar.SetBounds(Math.Max(0, ClientSize.Width - Gutter), 0, Gutter, ClientSize.Height);
            content.Width = Math.Max(0, ClientSize.Width - (overflow ? Gutter : 0));
            ScrollTo(offset);
        }

        void ScrollTo(int value)
        {
            offset = Math.Clamp(value, 0, MaximumOffset);
            content.Top = -offset;
            scrollbar.Invalidate();
        }

        void TrackFocus(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                if (child.TabStop)
                    child.Enter += (_, _) =>
                    {
                        int top = PointToClient(child.PointToScreen(Point.Empty)).Y;
                        if (top < 0) ScrollTo(offset + top);
                        else if (top + child.Height > ClientSize.Height)
                            ScrollTo(offset + top + child.Height - ClientSize.Height);
                    };
                TrackFocus(child);
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (MaximumOffset == 0) { base.OnMouseWheel(e); return; }
            wheelRemainder += e.Delta;
            int steps = wheelRemainder / 120;
            wheelRemainder %= 120;
            int lines = SystemInformation.MouseWheelScrollLines;
            ScrollTo(offset - steps * (lines < 0 ? ClientSize.Height : lines * 16));
            if (e is HandledMouseEventArgs handled) handled.Handled = true;
        }

        sealed class ScrollThumb : Control
        {
            readonly MainForm owner;
            readonly ThemedScrollPanel panel;
            int dragOffset;
            bool hover;
            int TrackLength => Math.Max(1, Height - 12);
            int ThumbLength => Math.Clamp((int)(TrackLength * (double)Height / Math.Max(1, panel.content.Height)),
                Math.Min(32, TrackLength), TrackLength);
            int Travel => TrackLength - ThumbLength;
            int ThumbTop => 6 + (int)Math.Round(Travel * panel.offset / (double)Math.Max(1, panel.MaximumOffset));

            public ScrollThumb(MainForm owner, ThemedScrollPanel panel)
            {
                this.owner = owner;
                this.panel = panel;
                AccessibleName = owner.T("Vertical scroll bar", "Dikey kaydırma çubuğu");
                AccessibleRole = AccessibleRole.ScrollBar;
                Cursor = Cursors.Hand;
                TabStop = true;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(panel.BackColor);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var track = SoftIcons.Round(5, 6, 6, TrackLength, 3);
                using var thumb = SoftIcons.Round(5, ThumbTop, 6, ThumbLength, 3);
                using var trackColor = new SolidBrush(MixColors(owner.Surface, owner.TextColor, .10));
                using var thumbColor = new SolidBrush(MixColors(owner.Surface, owner.Accent, hover || Capture || Focused ? .88 : .62));
                e.Graphics.FillPath(trackColor, track);
                e.Graphics.FillPath(thumbColor, thumb);
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (e.Button != MouseButtons.Left) return;
                Focus();
                int relative = e.Y - ThumbTop;
                dragOffset = relative >= 0 && relative < ThumbLength ? relative : ThumbLength / 2;
                Capture = true;
                DragTo(e.Y);
            }

            protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (Capture) DragTo(e.Y); }
            protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); if (e.Button == MouseButtons.Left) Capture = false; }
            protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); Invalidate(); }
            protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
            protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Invalidate(); }
            protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
            protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

            protected override bool IsInputKey(Keys keyData) =>
                keyData is Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End || base.IsInputKey(keyData);

            protected override void OnKeyDown(KeyEventArgs e)
            {
                int? target = e.KeyCode switch
                {
                    Keys.Up => panel.offset - 24,
                    Keys.Down => panel.offset + 24,
                    Keys.PageUp => panel.offset - Height,
                    Keys.PageDown => panel.offset + Height,
                    Keys.Home => 0,
                    Keys.End => panel.MaximumOffset,
                    _ => null
                };
                if (target.HasValue) { panel.ScrollTo(target.Value); e.Handled = true; }
                base.OnKeyDown(e);
            }

            void DragTo(int y) => panel.ScrollTo((int)Math.Round((y - dragOffset - 6) *
                (double)panel.MaximumOffset / Math.Max(1, Travel)));
        }
    }
}
