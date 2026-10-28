using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using KoklukBedrockModloader;

if (args.Length is < 1 or > 3) throw new ArgumentException("Provide an output directory, optional original logo PNG and --transparent.");
string folder = Path.GetFullPath(args[0]); Directory.CreateDirectory(folder);
using var source = args.Length >= 2 ? Image.FromFile(Path.GetFullPath(args[1])) : null;
using var original = source == null ? null : new Bitmap(source);
if (original != null && args.Length == 3 && args[2] == "--transparent")
{
    // Convert the supplied monochrome artwork to an alpha mask without redrawing it.
    // Unmatte antialiased edge pixels so a black fringe cannot remain on light themes.
    for (int y = 0; y < original.Height; y++)
        for (int x = 0; x < original.Width; x++)
        {
            Color pixel = original.GetPixel(x, y);
            int luminance = (pixel.R * 299 + pixel.G * 587 + pixel.B * 114) / 1000;
            int alpha = Math.Clamp((luminance - 16) * 255 / 229, 0, 255);
            original.SetPixel(x, y, Color.FromArgb(alpha * pixel.A / 255, 255, 255, 255));
        }
    original.Save(Path.Combine(folder, "WolfTransparent.png"), ImageFormat.Png);
}
int[] sizes = [16, 32, 48, 64, 256];
var frames = new List<byte[]>();
foreach (int size in sizes)
{
    using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using (var g = Graphics.FromImage(bitmap))
    {
        g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Color.Transparent);
        if (original != null)
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(original, new Rectangle(0, 0, size, size));
        }
        else
        {
        using var background = new SolidBrush(Color.FromArgb(37, 29, 55));
        using var border = new Pen(Color.FromArgb(84, 65, 115), Math.Max(1, size / 128f));
        using var path = SoftIcons.Round(0.5f, 0.5f, size - 1, size - 1, size * 0.25f);
        g.FillPath(background, path); g.DrawPath(border, path);
        SoftIcons.Draw(g, "game", new RectangleF(size * 0.14f, size * 0.1f, size * 0.72f, size * 0.72f), Color.FromArgb(207, 188, 255), true);
        using var accent = new SolidBrush(Color.FromArgb(207, 188, 255));
        using var dash = SoftIcons.Round(size * 0.37f, size * 0.78f, size * 0.26f, size * 0.045f, size * 0.022f); g.FillPath(accent, dash);
        }
    }
    using var memory = new MemoryStream(); bitmap.Save(memory, ImageFormat.Png); frames.Add(memory.ToArray());
    if (size == 256) bitmap.Save(Path.Combine(folder, "BedrockLoader.png"), ImageFormat.Png);
}
using (var output = new BinaryWriter(File.Create(Path.Combine(folder, "BedrockLoader.ico"))))
{
    output.Write((ushort)0); output.Write((ushort)1); output.Write((ushort)sizes.Length);
    int offset = 6 + sizes.Length * 16;
    for (int i = 0; i < sizes.Length; i++)
    {
        output.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); output.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
        output.Write((byte)0); output.Write((byte)0); output.Write((ushort)1); output.Write((ushort)32);
        output.Write(frames[i].Length); output.Write(offset); offset += frames[i].Length;
    }
    foreach (byte[] frame in frames) output.Write(frame);
}
Console.WriteLine("Created five-resolution Windows icon and PNG preview.");

string[] keys = ["Home", "Mods", "Addons", "Logs", "About", "game", "language", "moon", "sun", "settings", "play", "external", "minimize", "maximize", "restore", "close", "status", "check-mark"];
using var atlas = new Bitmap(768, 336, PixelFormat.Format32bppArgb);
using (var g = Graphics.FromImage(atlas))
{
    g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Color.FromArgb(23, 19, 32));
    using var font = new Font("Segoe UI", 9);
    using var ink = new SolidBrush(Color.FromArgb(226, 217, 240));
    using var tile = new SolidBrush(Color.FromArgb(48, 40, 64));
    using var centered = new StringFormat { Alignment = StringAlignment.Center };
    for (int i = 0; i < keys.Length; i++)
    {
        int x = i % 6 * 128, y = i / 6 * 112;
        using var tileShape = SoftIcons.Round(x + 36, y + 10, 56, 56, 22); g.FillPath(tile, tileShape);
        SoftIcons.Draw(g, keys[i], new RectangleF(x + 48, y + 22, 32, 32), keys[i] == "close" ? Color.FromArgb(255, 165, 178) : Color.FromArgb(207, 188, 255), true);
        g.DrawString(keys[i], font, ink, new RectangleF(x, y + 79, 128, 25), centered);
    }
}
atlas.Save(Path.Combine(folder, "IconFamily.png"), ImageFormat.Png);
