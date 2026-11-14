using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Numerics;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    sealed class SkinPreview : Control
    {
        readonly System.Windows.Forms.Timer timer = new() { Interval = 33 };
        Bitmap? texture;
        bool slim, layers = true;
        int motion;
        float yaw = .38f, pitch = -.15f, zoom = 1;
        Point drag;

        public SkinPreview()
        {
            DoubleBuffered = true;
            timer.Tick += (_, _) =>
            {
                if (Visible && FindForm()?.WindowState != FormWindowState.Minimized) Invalidate();
            };
        }

        public void SetSkin(SkinEntry? skin)
        {
            texture?.Dispose();
            texture = null;
            if (skin != null)
            {
                using var stream = new MemoryStream(skin.Png);
                using var image = Image.FromStream(stream);
                texture = new Bitmap(image);
                slim = skin.Slim;
            }
            timer.Enabled = texture != null && motion != 0;
            Invalidate();
        }

        public bool ToggleLayers() { layers = !layers; Invalidate(); return layers; }
        public int NextMotion()
        {
            motion = (motion + 1) % 3;
            timer.Enabled = texture != null && motion != 0;
            Invalidate();
            return motion;
        }

        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); drag = e.Location; Focus(); }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (e.Button != MouseButtons.Left) return;
            yaw -= (e.X - drag.X) * .015f;
            pitch = Math.Clamp(pitch + (e.Y - drag.Y) * .012f, -1.35f, 1.35f);
            drag = e.Location;
            Invalidate();
        }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            zoom = Math.Clamp(zoom * MathF.Pow(1.12f, e.Delta / 120f), .5f, 2.5f);
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { timer.Dispose(); texture?.Dispose(); }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (texture is not { } bitmap)
            {
                SoftIcons.Draw(e.Graphics, "question", new RectangleF((Width - 64) / 2f, (Height - 64) / 2f, 64, 64), ForeColor);
                return;
            }
            var camera = Matrix4x4.CreateRotationY(yaw) * Matrix4x4.CreateRotationX(pitch);
            float scale = Math.Min(Width / 25f, Height / 38f) * zoom;
            float swing = motion == 0 ? 0 : MathF.Sin((float)(Environment.TickCount64 % 100000) * (motion == 1 ? .007f : .012f)) * (motion == 1 ? .48f : .82f);
            int arm = slim && bitmap.Height != 32 ? 3 : 4;
            bool legacy = bitmap.Height == 32;
            int density = bitmap.Width / 64;
            var faces = new List<(float Depth, PointF[] Points, Rectangle Uv)>();
            Part(-4, 0, 8, 8, 8, 0, 0, 32, 0, 0, .5f);
            Part(-4, 8, 8, 12, 4, 16, 16, 16, 32, 0);
            Part(-4-arm, 8, arm, 12, 4, 40, 16, 40, 32, swing);
            Part(4, 8, arm, 12, 4, legacy ? 40 : 32, legacy ? 16 : 48, 48, 48, -swing, mirror: legacy);
            Part(-4, 20, 4, 12, 4, 0, 16, 0, 32, -swing);
            Part(0, 20, 4, 12, 4, legacy ? 0 : 16, legacy ? 16 : 48, 0, 48, swing, mirror: legacy);
            e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
            foreach (var face in faces.OrderByDescending(face => face.Depth))
                e.Graphics.DrawImage(bitmap, face.Points, face.Uv, GraphicsUnit.Pixel);

            void Part(float x, float y, int w, int h, int d, int u, int v, int outerU, int outerV, float angle, float inflate = .25f, bool mirror = false)
            {
                var joint = Matrix4x4.CreateRotationX(angle);
                Box(u, v, 0);
                if (layers && (!legacy || y == 0)) Box(outerU, outerV, inflate);

                void Box(int tx, int ty, float padding)
                {
                    float left = x - padding, right = x+w+padding, top = -padding, bottom = h+padding;
                    float front = -d/2f-padding, back = d/2f+padding;
                    var a = new Vector3(left,top,front); var b = new Vector3(right,top,front);
                    var c = new Vector3(left,bottom,front); var f = new Vector3(right,bottom,front);
                    var g = new Vector3(left,top,back); var k = new Vector3(right,top,back);
                    var m = new Vector3(left,bottom,back); var n = new Vector3(right,bottom,back);
                    Face(a,b,c,tx+d,ty+d,w,h);
                    Face(k,g,n,tx+d+w+d,ty+d,w,h);
                    Face(g,a,m,tx,ty+d,d,h);
                    Face(b,k,f,tx+d+w,ty+d,d,h);
                    Face(g,k,a,tx+d,ty,w,d);
                    Face(c,f,m,tx+d+w,ty,w,d);
                }

                void Face(Vector3 a, Vector3 b, Vector3 c, int tx, int ty, int tw, int th)
                {
                    Vector3 Transform(Vector3 point)
                    {
                        point.X -= x+w/2f;
                        point = Vector3.Transform(point, joint);
                        point += new Vector3(x+w/2f, y-16, 0);
                        return Vector3.Transform(point, camera);
                    }
                    a = Transform(a); b = Transform(b); c = Transform(c);
                    if (Vector3.Cross(b-a, c-a).Z <= 0) return;
                    if (mirror)
                    {
                        c = b + c - a;
                        (a, b) = (b, a);
                        if (tw == d && th == h && w != d) tx = tx == u ? u+d+w : u;
                    }
                    PointF Project(Vector3 p) => new(Width/2f+p.X*scale, Height/2f+p.Y*scale);
                    faces.Add(((a.Z+b.Z+c.Z)/3, [Project(a),Project(b),Project(c)], new Rectangle(tx*density,ty*density,tw*density,th*density)));
                }
            }
        }
    }

    SkinEntry? EditNativeSkin(SkinEntry skin)
    {
        using var stream = new MemoryStream(skin.Png);
        using var image = Image.FromStream(stream);
        using var bitmap = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap)) graphics.DrawImageUnscaled(image, 0, 0);
        using var dialog = new RoundedPopupForm(Accent) { FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false, Text = T("Edit skin", "Skini düzenle"), StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(Math.Min(750, ClientSize.Width - 32), Math.Min(590, ClientSize.Height - 32)),
            BackColor = Surface, ForeColor = TextColor, Padding = new Padding(14), MinimizeBox = false, MaximizeBox = false };
        var name = MakeTextBox(skin.Name); name.BorderStyle = BorderStyle.None; name.Dock = DockStyle.Fill;
        var nameCard = new RoundedPanel { Width = 138, Height = 36, CornerRadius = 16, BackColor = SurfaceAlt, Padding = new Padding(10, 8, 10, 0) };
        nameCard.Controls.Add(name);
        bool slim = skin.Slim;
        var model = MakeButton(slim ? "Alex" : "Steve", () => { }, 84, "Skins");
        model.Click += (_, _) => { slim = !slim; model.Text = slim ? "Alex" : "Steve"; };
        ApplyButtonPalette(model);
        bool erasing = false;
        var erase = MakeButton(T("Eraser", "Silgi"), () => { }, 91, "eraser");
        void SetEraser(bool value)
        {
            erasing = value;
            erase.Tag = value ? "primary" : null;
            ApplyButtonPalette(erase);
        }
        erase.Click += (_, _) => SetEraser(!erasing);
        SetEraser(false);
        Color ink = Color.White;
        var palette = new RoundedPanel { Dock = DockStyle.Left, Width = 150, CornerRadius = 18,
            BackColor = SurfaceAlt, Padding = new Padding(9) };
        var colorPreview = new RoundedPanel { Dock = DockStyle.Top, Height = 26, CornerRadius = 9, BackColor = ink };
        var hex = MakeTextBox("#FFFFFF");
        hex.Dock = DockStyle.Top;
        hex.MaxLength = 7;
        hex.AccessibleName = T("Hex color", "Hex renk kodu");
        hex.TextChanged += (_, _) =>
        {
            if (hex.Text.Length != 7 || hex.Text[0] != '#' ||
                !int.TryParse(hex.Text.AsSpan(1), System.Globalization.NumberStyles.HexNumber, null, out int rgb)) return;
            ink = Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
            colorPreview.BackColor = ink;
            SetEraser(false);
        };
        var wheel = new SkinColorWheel { Dock = DockStyle.Top, Height = 105,
            AccessibleName = T("Color wheel", "Renk çarkı") };
        wheel.ColorPicked += color => hex.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        var swatches = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(0, 5, 0, 0) };
        foreach (string code in new[] { "#FFFFFF", "#CCCCCC", "#777777", "#181818", "#E05263", "#FA98AB",
                     "#F79455", "#FFD66B", "#8BCC7E", "#38AC76", "#4EBAC0", "#368FDA",
                     "#6575D5", "#9E75DE", "#E886CF", "#825642", "#DCAD90", "#F8D2B6",
                     "#000000", "#922134", "#A54217", "#876308", "#315427", "#095E64",
                     "#153878", "#41296E", "#802658", "#FFEEE2", "#AAF0D7", "#BCCEFF" })
        {
            Color color = ColorTranslator.FromHtml(code);
            var swatch = new MaterialColorButton { SwatchColor = color, BackColor = color, Size = new Size(23, 23),
                Margin = new Padding(2), CornerRadius = 11, AccessibleName = code };
            swatch.Click += (_, _) => { hex.Text = code; SetEraser(false); };
            swatches.Controls.Add(swatch);
        }
        palette.Controls.Add(swatches);
        palette.Controls.Add(wheel);
        palette.Controls.Add(hex);
        palette.Controls.Add(colorPreview);
        var save = MakeButton(T("Save", "Kaydet"), () => dialog.DialogResult = DialogResult.OK, 96, "check-mark");
        save.Tag = "primary";
        ApplyButtonPalette(save);
        var header = new Panel { Dock = DockStyle.Top, Height = 49, BackColor = Surface };
        nameCard.Location = new Point(0, 1);
        nameCard.Width = 220;
        var close = new SymbolButton { Symbol = "close", AccessibleName = T("Close", "Kapat"), Dock = DockStyle.Right,
            Width = 36, ForeColor = TextColor, BackColor = SurfaceAlt, CornerRadius = 18 };
        var closeHost = new Panel { Dock = DockStyle.Right, Width = 36, Padding = new Padding(0, 1, 0, 12) };
        closeHost.Controls.Add(close);
        header.Controls.Add(nameCard);
        header.Controls.Add(closeHost);
        var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0), WrapContents = true };
        close.Click += (_, _) => dialog.DialogResult = DialogResult.Cancel;
        var canvas = new PictureBox { Dock = DockStyle.Fill, BackColor = SurfaceAlt, Margin = Padding.Empty };
        var overlay = MakeButton(T("Overlay PNG", "Bindir"), OverlayPng, 136, "image-overlay");
        ApplyButtonPalette(overlay);
        bool showLayers = true;
        using var layerStatus = new System.Windows.Forms.Timer { Interval = 2000 };
        var layerButton = MakeButton(T("Layers", "Katman"), () => { }, 166, "layers");
        ApplyButtonPalette(layerButton);
        layerButton.Click += (_, _) =>
        {
            showLayers = !showLayers;
            layerButton.Text = showLayers ? T("Layers: on", "Katman: açık") : T("Layers: off", "Katman: kapalı");
            layerStatus.Stop(); layerStatus.Start();
            canvas.Invalidate();
        };
        layerStatus.Tick += (_, _) => { layerStatus.Stop(); layerButton.Text = T("Layers", "Katman"); };
        bar.Controls.AddRange([model, erase, overlay, layerButton]);
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 53 };
        save.Dock = DockStyle.Right;
        var saveHost = new Panel { Dock = DockStyle.Right, Width = 104, Padding = new Padding(0, 8, 0, 8) };
        saveHost.Controls.Add(save);
        bar.Dock = DockStyle.Fill;
        footer.Controls.Add(bar);
        footer.Controls.Add(saveHost);
        void FitFooter() => footer.Height = bar.Controls.Cast<Control>().Sum(control => control.Width + control.Margin.Horizontal)
            <= footer.ClientSize.Width - saveHost.Width ? 53 : 99;
        footer.Resize += (_, _) => FitFooter();
        FitFooter();
        dialog.CancelButton = close;
        Rectangle Area()
        {
            int side = Math.Max(1, Math.Min(canvas.Width, canvas.Height) - 12);
            if (side >= bitmap.Width) side -= side % bitmap.Width;
            return new Rectangle((canvas.Width - side) / 2, (canvas.Height - side) / 2, side, side);
        }
        Rectangle[] layerRects = [new(32, 0, 32, 16), new(0, 32, 56, 16), new(0, 48, 16, 16), new(48, 48, 16, 16)];
        canvas.Paint += (_, e) =>
        {
            e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
            var area = Area();
            if (!showLayers)
                foreach (var rect in layerRects)
                    e.Graphics.ExcludeClip(new Rectangle(area.X + rect.X * area.Width / 64, area.Y + rect.Y * area.Height / 64,
                        rect.Width * area.Width / 64, rect.Height * area.Height / 64));
            e.Graphics.DrawImage(bitmap, area);
        };
        void PaintPixel(object? sender, MouseEventArgs e)
        {
            var area = Area();
            if (e.Button != MouseButtons.Left || !area.Contains(e.Location)) return;
            int x = (e.X - area.X) * bitmap.Width / area.Width, y = (e.Y - area.Y) * bitmap.Height / area.Height;
            if (!showLayers && layerRects.Any(rect => rect.Contains(x * 64 / bitmap.Width, y * 64 / bitmap.Height))) return;
            bitmap.SetPixel(x, y, erasing ? Color.Transparent : ink);
            canvas.Invalidate();
        }
        canvas.MouseDown += PaintPixel;
        canvas.MouseMove += PaintPixel;
        var editor = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 13));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var separator = new Panel { Dock = DockStyle.Fill, Margin = new Padding(6, 7, 6, 7), BackColor = MixColors(Surface, TextColor, .20) };
        editor.Controls.Add(palette, 0, 0);
        palette.Dock = DockStyle.Fill;
        editor.Controls.Add(separator, 1, 0);
        editor.Controls.Add(canvas, 2, 0);
        dialog.Controls.Add(editor);
        dialog.Controls.Add(footer);
        dialog.Controls.Add(header);
        void OverlayPng()
        {
            using var file = new OpenFileDialog { Filter = "PNG (*.png)|*.png" };
            if (file.ShowDialog(dialog) != DialogResult.OK) return;
            try
            {
                if (new FileInfo(file.FileName).Length > 2 * 1024 * 1024) throw new InvalidDataException();
                using var input = File.OpenRead(file.FileName);
                using var layer = Image.FromStream(input);
                if (layer.RawFormat.Guid != ImageFormat.Png.Guid || layer.Width != layer.Height || layer.Width is not (64 or 128))
                    throw new InvalidDataException();
                using var graphics = Graphics.FromImage(bitmap);
                graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;
                graphics.DrawImage(layer, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                canvas.Invalidate();
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or OutOfMemoryException)
            {
                MessageBox.Show(dialog, T("Choose a valid 64×64 or 128×128 PNG.", "Geçerli bir 64×64 veya 128×128 PNG seç."),
                    T("Overlay PNG", "PNG bindir"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        if (dialog.ShowDialog(this) != DialogResult.OK) return null;
        string title = SkinLibrary.CleanName(name.Text);
        if (title.Length == 0) return null;
        using var output = new MemoryStream();
        bitmap.Save(output, ImageFormat.Png);
        return skin with { Name = title, Slim = slim, Png = SkinLibrary.Normalize(output.ToArray()) };
    }
}
