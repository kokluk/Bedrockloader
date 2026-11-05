using System.Diagnostics;
using System.IO.Compression;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    void BuildWorldsPage()
    {
        var grid = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Tag = "page" };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Tag = "page" };
        contentHost.Controls.Add(grid);
        contentHost.Controls.Add(actions);

        if (!cinnabarMode) actions.Controls.Add(MakeButton(T("Import world", "Dünya içe aktar"), ImportWorld, 155));
        actions.Controls.Add(MakeButton(T("Refresh", "Yenile"), LoadWorlds, 95));
        AddPageHeading(T("Worlds", "Dünyalar"), cinnabarMode ? T("Browse Cinnabar local worlds.", "Cinnabar’ın yerel dünyalarını görüntüle.") : T("Browse your worlds and import downloaded .mcworld files.", "Dünyalarını görüntüle ve indirdiğin .mcworld dosyalarını içe aktar."));
        if (!cinnabarMode) AddCurseForgeHeadingButton("CurseWorlds");
        LoadWorlds();

        void LoadWorlds()
        {
            while (grid.Controls.Count > 0) grid.Controls[0].Dispose();
            foreach (string root in cinnabarMode ? new[] { Path.Combine(CinnabarDataRoot(), "worlds") } : WorldRoots())
            {
                try
                {
                    if (!Directory.Exists(root)) continue;
                    foreach (string folder in Directory.EnumerateDirectories(root))
                    {
                        try
                        {
                            var safe = BedrockSkinSource.SafeChild(root, Path.GetFileName(folder));
                            if (!File.Exists(BedrockSkinSource.SafeChild(safe, cinnabarMode ? "world.json" : "level.dat"))) continue;
                            AddWorld(safe);
                        }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { }
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
            if (grid.Controls.Count == 0)
                grid.Controls.Add(new Label { AutoSize = true, ForeColor = TextColor, Text = T("No local worlds found.", "Yerel dünya bulunamadı.") });
        }

        void AddWorld(string folder)
        {
            var nameFile = BedrockSkinSource.SafeChild(folder, "levelname.txt");
            string title = File.Exists(nameFile) && new FileInfo(nameFile).Length <= 4096 ? File.ReadAllText(nameFile).Trim() : "";
            if (cinnabarMode)
            {
                var metadata = BedrockSkinSource.SafeChild(folder, "world.json");
                try
                {
                    if (new FileInfo(metadata).Length <= 1024 * 1024)
                    {
                        using var data = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(metadata));
                        if (data.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object &&
                            data.RootElement.TryGetProperty("name", out var name) && name.ValueKind == System.Text.Json.JsonValueKind.String)
                            title = name.GetString() ?? "";
                    }
                }
                catch (System.Text.Json.JsonException) { }
            }
            if (title.Length == 0) title = Path.GetFileName(folder);
            var card = new RoundedPanel { Width = 158, Height = 158, CornerRadius = 19, BackColor = SurfaceAlt, Tag = "card", Margin = new Padding(4, 4, 9, 9) };
            var image = new PictureBox { Size = new Size(126, 85), Location = new Point(16, 10), SizeMode = PictureBoxSizeMode.Zoom };
            try
            {
                foreach (string file in new[] { "world_icon.jpeg", "world_icon.jpg", "world_icon.png", "icon.png" })
                {
                    var icon = BedrockSkinSource.SafeChild(folder, file);
                    if (!File.Exists(icon) || new FileInfo(icon).Length > 12 * 1024 * 1024) continue;
                    using var original = Image.FromFile(icon);
                    if (original.Width > 8192 || original.Height > 8192) continue;
                    double scale = Math.Min(126d / original.Width, 85d / original.Height);
                    var thumbnail = new Bitmap(Math.Max(1, (int)(original.Width * scale)), Math.Max(1, (int)(original.Height * scale)));
                    using (var graphics = Graphics.FromImage(thumbnail))
                    {
                        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        graphics.DrawImage(original, new Rectangle(0, 0, thumbnail.Width, thumbnail.Height));
                    }
                    image.Image = thumbnail;
                    break;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException or OutOfMemoryException) { }
            image.Paint += (_, e) =>
            {
                if (image.Image == null)
                    SoftIcons.Draw(e.Graphics, "Worlds", new RectangleF(40, 19, 46, 46), Accent);
            };
            image.Disposed += (_, _) => image.Image?.Dispose();
            var label = new Label { Text = title, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleCenter, ForeColor = TextColor, Bounds = new Rectangle(8, 97, 142, 21) };
            var open = MakeButton(T("Open folder", "Klasörü aç"), () => OpenUrl(folder), 126);
            open.Bounds = new Rectangle(16, 121, 126, 29);
            card.Controls.AddRange([image, label, open]);
            grid.Controls.Add(card);
        }
    }

    static IEnumerable<string> WorldRoots()
    {
        var users = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Minecraft Bedrock", "Users");
        string[] profiles = [];
        try { if (Directory.Exists(users)) profiles = Directory.GetDirectories(users); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        foreach (var profile in profiles)
        {
            string root;
            try { root = BedrockSkinSource.SafeChild(users, Path.GetFileName(profile), "games", "com.mojang", "minecraftWorlds"); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { continue; }
            yield return root;
        }
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", "Microsoft.MinecraftUWP_8wekyb3d8bbwe", "LocalState", "games", "com.mojang", "minecraftWorlds");
    }

    void ImportWorld()
    {
        using var picker = new OpenFileDialog { Filter = "Minecraft World (*.mcworld)|*.mcworld" };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            OpenWorldArchive(picker.FileName);
        }
        catch (Exception ex) { ShowError(ex, T("Could not open the world", "Dünya açılamadı")); }
    }
    void OpenWorldArchive(string path)
    {
        if (!Path.GetExtension(path).Equals(".mcworld", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Expected an .mcworld file.");
        using (var archive = ZipFile.OpenRead(path))
        {
            if (archive.GetEntry("level.dat") is not { Length: > 8 } ||
                !archive.Entries.Any(entry => entry.FullName.StartsWith("db/", StringComparison.Ordinal) && entry.Length > 0) ||
                archive.Entries.Any(entry => entry.FullName.Split('/', '\\').Contains("..") || Path.IsPathRooted(entry.FullName) || entry.FullName.Contains(':')))
                throw new InvalidDataException("Invalid Minecraft world archive.");
        }
        using var process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        UpdateStatus(T("Complete the import in Minecraft, then refresh this list.", "İçe aktarmayı Minecraft’ta tamamla, ardından listeyi yenile."));
    }
}
