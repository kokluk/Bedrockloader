using System.Buffers.Binary;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Text.Json;

namespace KoklukBedrockModloader;

internal sealed record SkinEntry(Guid Id, string Name, bool Slim, bool Favorite, string? Pack, byte[] Png, DateTime Created);
internal sealed class SkinLibrary(string root)
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal const int MaxPng = 1024 * 1024, Capacity = 128;
    string PathFor(Guid id) => Path.Combine(root, id.ToString("N") + ".json");

    internal List<SkinEntry> Read()
    {
        if (!Directory.Exists(root)) return [];
        var entries = new List<SkinEntry>();
        foreach (string file in Directory.EnumerateFiles(root, "*.json").Take(Capacity))
        {
            try
            {
                var entry = JsonSerializer.Deserialize<SkinEntry>(ReadLimited(file, 2 * MaxPng), Json);
                if (entry is null || !string.Equals(file, PathFor(entry.Id), StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(entry.Name)) continue;
                ValidateDimensions(entry.Png);
                entries.Add(entry);
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or JsonException or ArgumentException) { }
        }
        return entries.OrderByDescending(entry => entry.Created).ToList();
    }

    internal void Save(SkinEntry entry)
    {
        Directory.CreateDirectory(root);
        string temp = Path.Combine(root, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllBytes(temp, JsonSerializer.SerializeToUtf8Bytes(entry, Json));
            File.Move(temp, PathFor(entry.Id), overwrite: true);
        }
        finally { File.Delete(temp); }
    }

    internal void Remove(Guid id) => File.Delete(PathFor(id));
    internal static string CleanName(string name) => new(name.Trim().Where(c => !char.IsControl(c)).Take(80).ToArray());
    internal static byte[] ReadLimited(string file, int limit)
    {
        using var stream = File.OpenRead(file);
        if (stream.Length > limit) throw new InvalidDataException("File exceeds the size limit.");
        byte[] bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }

    internal static (int Width, int Height) ValidateDimensions(byte[] png)
    {
        if (png is null || png.Length < 33 || png.Length > MaxPng ||
            !png.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ||
            BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(8)) != 13 || !png.AsSpan(12, 4).SequenceEqual("IHDR"u8))
            throw new InvalidDataException("Invalid PNG.");
        int w = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)), h = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20));
        if ((w, h) is not ((64, 64) or (64, 32) or (128, 128)))
            throw new InvalidDataException("Expected a 64×64, 64×32 or 128×128 PNG.");
        return (w, h);
    }

    internal static byte[] Normalize(byte[] png)
    {
        ValidateDimensions(png);
        using var input = new MemoryStream(png);
        using var image = Image.FromStream(input, false, true);
        using var clean = new Bitmap(image);
        using var result = new MemoryStream();
        clean.Save(result, ImageFormat.Png);
        return result.ToArray();
    }

    internal static List<SkinEntry> ReadPack(string file, int remaining)
    {
        using var input = new MemoryStream(ReadLimited(file, 24 * MaxPng));
        using var zip = new ZipArchive(input);
        if (zip.Entries.Count > 256) throw new InvalidDataException("Too many pack entries.");
        byte[] ReadEntry(string name)
        {
            var matches = zip.Entries.Where(e => e.FullName.Equals(name, StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
            if (matches.Length != 1 || matches[0].Length > MaxPng) throw new InvalidDataException("Missing, duplicate or oversized pack entry.");
            using var source = matches[0].Open();
            var bytes = new byte[(int)matches[0].Length];
            source.ReadExactly(bytes);
            if (source.ReadByte() != -1) throw new InvalidDataException("Invalid archive size.");
            return bytes;
        }
        return ParsePack(ReadEntry, pack: CleanName(Path.GetFileNameWithoutExtension(file)), remaining);
    }

    internal static List<SkinEntry> ReadPackFolder(string folder, int remaining) =>
        ParsePack(name => ReadLimited(BedrockSkinSource.SafeChild(folder, name.Split('/')), MaxPng), CleanName(Path.GetFileName(folder)), remaining);

    static List<SkinEntry> ParsePack(Func<string, byte[]> ReadEntry, string pack, int remaining)
    {
        using var manifest = JsonDocument.Parse(ReadEntry("manifest.json"));
        if (manifest.RootElement.GetProperty("format_version").GetInt32() is not (1 or 2))
            throw new InvalidDataException("Unsupported skin pack version.");
        var modules = manifest.RootElement.GetProperty("modules");
        if (modules.GetArrayLength() != 1 || modules[0].GetProperty("type").GetString() != "skin_pack")
            throw new InvalidDataException("Expected a skin pack.");
        var header = manifest.RootElement.GetProperty("header");
        if (!Guid.TryParse(header.GetProperty("uuid").GetString(), out _) ||
            !Guid.TryParse(modules[0].GetProperty("uuid").GetString(), out _))
            throw new InvalidDataException("Invalid pack identifier.");
        using var data = JsonDocument.Parse(ReadEntry("skins.json"));
        string localization = data.RootElement.GetProperty("localization_name").GetString() ?? "";
        var translations = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            string language = System.Text.Encoding.UTF8.GetString(ReadEntry("texts/en_US.lang")).TrimStart((char)0xfeff);
            foreach (string line in language.Split((char)10))
            {
                int equals = line.IndexOf('=');
                if (equals > 0) translations.TryAdd(line[..equals], CleanName(line[(equals + 1)..]));
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException) { }
        if (translations.TryGetValue($"skinpack.{localization}", out string? title) && title.Length > 0) pack = title;
        var skins = data.RootElement.GetProperty("skins");
        var entries = new List<SkinEntry>();
        foreach (var skin in skins.EnumerateArray())
        {
            if (skin.GetProperty("type").GetString() != "free") continue;
            string? geometry = skin.GetProperty("geometry").GetString();
            if (geometry is not ("geometry.humanoid.custom" or "geometry.humanoid.customSlim")) continue;
            string texture = skin.GetProperty("texture").GetString() ?? "";
            if (texture.IndexOfAny(['/', (char)92, ':']) >= 0 || !texture.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Invalid texture name.");
            if (entries.Count >= remaining) throw new InvalidDataException("Skin library is full.");
            string key = skin.GetProperty("localization_name").GetString() ?? "";
            string name = translations.GetValueOrDefault($"skin.{localization}.{key}") ?? CleanName(key);
            entries.Add(new SkinEntry(Guid.NewGuid(), name.Length == 0 ? "Skin" : name, geometry.EndsWith("Slim"), false, pack, Normalize(ReadEntry(texture)), DateTime.UtcNow));
        }
        if (entries.Count == 0) throw new InvalidDataException("No free classic skins found.");
        return entries;
    }

    internal static void WritePack(string file, IReadOnlyList<SkinEntry> entries, string title)
    {
        string key = "bedrockloader_" + Guid.NewGuid().ToString("N");
        string temp = Path.Combine(Path.GetDirectoryName(file)!, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var zip = ZipFile.Open(temp, ZipArchiveMode.Create))
            {
                void Write(string name, byte[] bytes)
                {
                    using var output = zip.CreateEntry(name).Open();
                    output.Write(bytes);
                }
                void WriteJson(string name, object data) => Write(name, JsonSerializer.SerializeToUtf8Bytes(data));
                int[] version = [1, 0, 0];
                WriteJson("manifest.json", new { format_version = 1, header = new { name = "pack.name", uuid = Guid.NewGuid(), version }, modules = new[] { new { type = "skin_pack", uuid = Guid.NewGuid(), version } } });
                WriteJson("skins.json", new { serialize_name = key, localization_name = key, skins = entries.Select((skin, i) => new { localization_name = "skin" + i, geometry = skin.Slim ? "geometry.humanoid.customSlim" : "geometry.humanoid.custom", texture = $"skin{i}.png", type = "free" }) });
                var strings = new List<string> { $"skinpack.{key}={CleanName(title)}" };
                for (int i = 0; i < entries.Count; i++)
                {
                    Write($"skin{i}.png", Normalize(entries[i].Png));
                    strings.Add($"skin.{key}.skin{i}={CleanName(entries[i].Name)}");
                }
                WriteJson("texts/languages.json", new[] { "en_US", "tr_TR" });
                var language = System.Text.Encoding.UTF8.GetBytes(string.Join("\n", strings) + "\n");
                Write("texts/en_US.lang", language);
                Write("texts/tr_TR.lang", language);
            }
            File.Move(temp, file, overwrite: true);
        }
        finally { File.Delete(temp); }
    }
}
