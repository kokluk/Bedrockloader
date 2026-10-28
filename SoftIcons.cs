using System.Drawing;
using System.Drawing.Drawing2D;

namespace KoklukBedrockModloader;

// A single 24-unit vector family: rounded caps, soft silhouettes and tonal fills.
// Original artwork inspired by expressive Material shapes; no icon font required.
internal static class SoftIcons
{
    public static void Draw(Graphics graphics, string key, RectangleF bounds, Color color, bool emphasized = false)
    {
        var state = graphics.Save();
        try
        {
            graphics.TranslateTransform(bounds.X, bounds.Y);
            graphics.ScaleTransform(bounds.Width / 24f, bounds.Height / 24f);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            using var pen = new Pen(color, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            using var fill = new SolidBrush(Color.FromArgb(emphasized ? 68 : 28, color));
            using var solid = new SolidBrush(color);
            void Shape(GraphicsPath path)
            {
                using (path) { graphics.FillPath(fill, path); graphics.DrawPath(pen, path); }
            }
            void Line(float x, float y, float x2, float y2) => graphics.DrawLine(pen, x, y, x2, y2);
            switch (key)
            {
                case "update":
                    graphics.FillEllipse(fill, 3, 3, 18, 18);
                    graphics.DrawArc(pen, 4, 4, 16, 16, 35, 290);
                    Line(19, 3.5f, 19, 8.5f); Line(14, 8.5f, 19, 8.5f);
                    Line(12, 8, 12, 16); Line(9, 13, 12, 16); Line(12, 16, 15, 13);
                    break;
                case "Home":
                    using (var p = new GraphicsPath())
                    {
                        p.AddLine(4, 10, 10, 5); p.AddBezier(10, 5, 11, 4, 13, 4, 14, 5); p.AddLine(14, 5, 20, 10);
                        p.AddBezier(20, 10, 21, 11, 21, 12, 21, 14); p.AddLine(21, 14, 21, 18);
                        p.AddBezier(21, 18, 21, 20, 20, 21, 18, 21); p.AddLine(18, 21, 6, 21);
                        p.AddBezier(6, 21, 4, 21, 3, 20, 3, 18); p.AddLine(3, 18, 3, 14); p.AddBezier(3, 14, 3, 12, 3, 11, 4, 10); p.CloseFigure();
                        graphics.FillPath(fill, p); graphics.DrawPath(pen, p);
                        using var door = new GraphicsPath(); door.AddLine(9, 21, 9, 16); door.AddBezier(9, 16, 9, 12.5f, 15, 12.5f, 15, 16); door.AddLine(15, 16, 15, 21); graphics.DrawPath(pen, door);
                    }
                    break;
                case "Mods":
                    using (var p = new GraphicsPath())
                    {
                        p.AddLine(6, 7, 9, 7); p.AddBezier(9, 7, 9, 2, 15, 2, 15, 7); p.AddLine(15, 7, 18, 7);
                        p.AddBezier(18, 7, 20, 7, 21, 8, 21, 10); p.AddLine(21, 10, 21, 12);
                        p.AddBezier(21, 12, 16, 11, 16, 18, 21, 17); p.AddLine(21, 17, 21, 18);
                        p.AddBezier(21, 18, 21, 20, 20, 21, 18, 21); p.AddLine(18, 21, 6, 21);
                        p.AddBezier(6, 21, 4, 21, 3, 20, 3, 18); p.AddLine(3, 18, 3, 10); p.AddBezier(3, 10, 3, 8, 4, 7, 6, 7); p.CloseFigure();
                        graphics.FillPath(fill, p); graphics.DrawPath(pen, p);
                    }
                    break;
                case "Addons":
                    using (var p = new GraphicsPath())
                    {
                        p.AddLine(3, 8, 10, 4); p.AddBezier(10, 4, 11, 3.4f, 13, 3.4f, 14, 4); p.AddLine(14, 4, 21, 8);
                        p.AddBezier(21, 8, 22, 8.6f, 22, 9.4f, 21, 10); p.AddLine(21, 10, 14, 14);
                        p.AddBezier(14, 14, 13, 14.6f, 11, 14.6f, 10, 14); p.AddLine(10, 14, 3, 10); p.AddBezier(3, 10, 2, 9.4f, 2, 8.6f, 3, 8); p.CloseFigure();
                        graphics.FillPath(fill, p); graphics.DrawPath(pen, p);
                        for (int y = 13; y <= 17; y += 4)
                        {
                            using var layer = new GraphicsPath(); layer.AddLine(3, y, 10, y + 4); layer.AddBezier(10, y + 4, 11, y + 4.6f, 13, y + 4.6f, 14, y + 4); layer.AddLine(14, y + 4, 21, y); graphics.DrawPath(pen, layer);
                        }
                    }
                    break;
                case "Logs":
                    Shape(Round(4, 3, 16, 18, 4)); Line(8, 8, 16, 8); Line(8, 12, 16, 12); Line(8, 16, 13, 16); break;
                case "About":
                    graphics.FillEllipse(fill, 3, 3, 18, 18); graphics.DrawEllipse(pen, 3, 3, 18, 18);
                    Line(12, 11, 12, 16); graphics.FillEllipse(solid, 11, 7, 2, 2); break;
                case "game":
                    using (var p = new GraphicsPath())
                    {
                        p.AddLine(7, 7, 17, 7); p.AddBezier(17, 7, 20, 7, 21, 10, 22, 16);
                        p.AddBezier(22, 16, 22.5f, 19.5f, 19, 21, 16, 17); p.AddLine(16, 17, 8, 17);
                        p.AddBezier(8, 17, 5, 21, 1.5f, 19.5f, 2, 16); p.AddBezier(2, 16, 3, 10, 4, 7, 7, 7); p.CloseFigure();
                        graphics.FillPath(fill, p); graphics.DrawPath(pen, p);
                    }
                    Line(7, 10, 7, 14); Line(5, 12, 9, 12);
                    graphics.FillEllipse(solid, 14.8f, 10.3f, 2.2f, 2.2f); graphics.FillEllipse(solid, 17.4f, 13, 2.2f, 2.2f); break;
                case "language":
                    graphics.FillEllipse(fill, 3, 3, 18, 18); graphics.DrawEllipse(pen, 3, 3, 18, 18); graphics.DrawEllipse(pen, 8, 3, 8, 18); Line(3, 12, 21, 12); break;
                case "moon":
                    using (var p = new GraphicsPath())
                    {
                        p.AddBezier(19.8f, 15.5f, 17, 22, 9, 23, 4.8f, 17.5f); p.AddBezier(4.8f, 17.5f, 0.5f, 12, 4, 4, 10, 3);
                        p.AddBezier(10, 3, 7, 8, 8, 13, 12, 15); p.AddBezier(12, 15, 14.5f, 16.5f, 17, 16.5f, 19.8f, 15.5f); p.CloseFigure();
                        graphics.FillPath(fill, p); graphics.DrawPath(pen, p);
                    }
                    Line(18, 3, 18, 7); Line(16, 5, 20, 5); break;
                case "sun":
                    graphics.FillEllipse(fill, 7, 7, 10, 10); graphics.DrawEllipse(pen, 7, 7, 10, 10);
                    for (int i = 0; i < 8; i++) { double a = i * Math.PI / 4; Line(12 + (float)Math.Cos(a) * 8, 12 + (float)Math.Sin(a) * 8, 12 + (float)Math.Cos(a) * 10, 12 + (float)Math.Sin(a) * 10); } break;
                case "settings":
                    var points = Enumerable.Range(0, 16).Select(i => { double a = i * Math.PI / 8 - Math.PI / 2; float r = i % 2 == 0 ? 9.6f : 8.1f; return new PointF(12 + (float)Math.Cos(a) * r, 12 + (float)Math.Sin(a) * r); }).ToArray();
                    using (var p = new GraphicsPath()) { p.AddClosedCurve(points, 0.6f); graphics.FillPath(fill, p); graphics.DrawPath(pen, p); }
                    graphics.DrawEllipse(pen, 8.5f, 8.5f, 7, 7); break;
                case "play":
                    using (var p = new GraphicsPath())
                    {
                        p.AddBezier(9, 5.5f, 8, 5, 7, 5.5f, 7, 6.8f); p.AddLine(7, 6.8f, 7, 17.2f); p.AddBezier(7, 17.2f, 7, 18.5f, 8, 19, 9, 18.5f);
                        p.AddLine(9, 18.5f, 18, 13.2f); p.AddBezier(18, 13.2f, 19.3f, 12.5f, 19.3f, 11.5f, 18, 10.8f); p.AddLine(18, 10.8f, 9, 5.5f); p.CloseFigure(); graphics.FillPath(solid, p);
                    }
                    break;
                case "external":
                    using (var p = new GraphicsPath()) { p.AddBezier(10, 4, 4, 3, 3, 5, 3, 8); p.AddLine(3, 8, 3, 17); p.AddBezier(3, 17, 3, 20, 4, 21, 7, 21); p.AddLine(7, 21, 16, 21); p.AddBezier(16, 21, 20, 21, 21, 20, 20, 14); graphics.DrawPath(pen, p); }
                    Line(12, 12, 21, 3); Line(15, 3, 21, 3); Line(21, 3, 21, 9); break;
                case "minimize": Line(6, 12, 18, 12); break;
                case "maximize": Shape(Round(5, 5, 14, 14, 4)); break;
                case "restore":
                    using (var p = new GraphicsPath()) { p.AddLine(9, 4, 17, 4); p.AddBezier(17, 4, 20, 4, 20, 5, 20, 8); p.AddLine(20, 8, 20, 15); graphics.DrawPath(pen, p); }
                    Shape(Round(4, 8, 12, 12, 3)); break;
                case "close": Line(7, 7, 17, 17); Line(17, 7, 7, 17); break;
                case "status":
                    graphics.FillEllipse(fill, 3, 3, 18, 18); graphics.DrawEllipse(pen, 3, 3, 18, 18); Line(7.5f, 12, 10.5f, 15); Line(10.5f, 15, 16.5f, 9); break;
                case "check-mark": Line(5, 12, 10, 17); Line(10, 17, 19, 7); break;
            }
        }
        finally { graphics.Restore(state); }
    }

    public static GraphicsPath Round(float x, float y, float width, float height, float radius)
    {
        var path = new GraphicsPath(); float d = Math.Min(radius * 2, Math.Min(width, height));
        path.AddArc(x, y, d, d, 180, 90); path.AddArc(x + width - d, y, d, d, 270, 90);
        path.AddArc(x + width - d, y + height - d, d, d, 0, 90); path.AddArc(x, y + height - d, d, d, 90, 90); path.CloseFigure(); return path;
    }
}
