using System.Drawing.Drawing2D;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    sealed class SkinColorWheel : Control
    {
        const int Resolution = 100;
        readonly Bitmap wheel = new(Resolution, Resolution);
        Point? picked;
        public event Action<Color>? ColorPicked;

        public SkinColorWheel()
        {
            DoubleBuffered = true;
            Cursor = Cursors.Cross;
            float radius = Resolution / 2f;
            for (int y = 0; y < Resolution; y++)
            for (int x = 0; x < Resolution; x++)
            {
                float dx = (x + .5f - radius) / radius, dy = (y + .5f - radius) / radius;
                float saturation = MathF.Sqrt(dx * dx + dy * dy);
                if (saturation > 1) continue;
                float hue = (MathF.Atan2(dy, dx) * (3 / MathF.PI) + 6) % 6;
                wheel.SetPixel(x, y, Color.FromArgb(Channel(5), Channel(3), Channel(1)));
                int Channel(int n)
                {
                    float k = (n + hue) % 6;
                    return (int)MathF.Round(255 * (1 - saturation * Math.Clamp(Math.Min(k, 4 - k), 0, 1)));
                }
            }
        }

        Rectangle WheelArea
        {
            get
            {
                int side = Math.Max(1, Math.Min(Width, Height) - 6);
                return new Rectangle((Width - side) / 2, (Height - side) / 2, side, side);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var area = WheelArea;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawImage(wheel, area);
            if (picked is not { } point) return;
            int x = area.Left + point.X * area.Width / Resolution, y = area.Top + point.Y * area.Height / Resolution;
            e.Graphics.DrawEllipse(Pens.Black, x - 5, y - 5, 10, 10);
            e.Graphics.DrawEllipse(Pens.White, x - 4, y - 4, 8, 8);
        }

        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); Pick(e); }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); Pick(e); }

        void Pick(MouseEventArgs e)
        {
            var area = WheelArea;
            if (e.Button != MouseButtons.Left || !area.Contains(e.Location)) return;
            var point = new Point((e.X - area.X) * Resolution / area.Width, (e.Y - area.Y) * Resolution / area.Height);
            var color = wheel.GetPixel(point.X, point.Y);
            if (color.A == 0) return;
            picked = point;
            ColorPicked?.Invoke(color);
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) wheel.Dispose();
            base.Dispose(disposing);
        }
    }
}
