using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace KoklukBedrockModloader;

internal sealed record CursePack(int Id, string Name, string Summary, string Authors, Uri? Logo, Uri Website, bool CanDistribute);
internal sealed record CursePage(IReadOnlyList<CursePack> Packs, bool HasNext);
internal sealed record CurseFile(string Name, Uri Download, long Size, string Sha1);

internal static class CurseForgeApi
{
    public const int Game = 78022, Resources = 6929, Behaviors = 4984, PageSize = 2, Java = 432, Shaders = 6552, Skins = 6925, SkinPacks = 6928;
    public const long MaxDownload = 250L * 1024 * 1024;
    static bool Category(int value) => value is Resources or Behaviors;
    static string Text(JsonElement value, string property) => value.TryGetProperty(property, out var text) && text.ValueKind == JsonValueKind.String ? text.GetString() ?? "" : "";
    static int Number(JsonElement value, string property) => value.TryGetProperty(property, out var number) && number.TryGetInt32(out int result) ? result : 0;
    public static bool IsCdn(Uri uri) => uri.Scheme == "https" && uri.IsDefaultPort && uri.UserInfo.Length == 0 && (uri.Host.Equals("forgecdn.net", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".forgecdn.net", StringComparison.OrdinalIgnoreCase));
    static Uri? Cdn(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && IsCdn(uri) ? uri : null;
    public static bool IsPackFile(string file) => Path.GetExtension(file).ToLowerInvariant() is ".mcpack" or ".mcaddon" or ".zip";

    static async Task<JsonDocument> Request(HttpClient http, string key, string path, CancellationToken cancel)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.curseforge.com/v1/" + path);
        request.Headers.Add("x-api-key", key);
        request.Headers.Accept.ParseAdd("application/json");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel);
        response.EnsureSuccessStatusCode();
        var bytes = await ReadBounded(response, 8 * 1024 * 1024, cancel);
        return JsonDocument.Parse(bytes);
    }

    public static async Task ProbeAsync(HttpClient http, string key, CancellationToken cancel)
    {
        using var json = await Request(http, key, $"games/{Game}", cancel);
        if (Number(json.RootElement.GetProperty("data"), "id") != Game) throw new InvalidDataException("Bedrock catalog is unavailable.");
    }

    public static async Task<CursePage> SearchAsync(HttpClient http, string key, int category, string search, int offset, CancellationToken cancel, int filter = 0, bool shaders = false)
    {
        if (category <= 0 || offset < 0 || offset > 9998) throw new ArgumentException("Invalid catalog page.");
        using var json = await Request(http, key, $"mods/search?gameId={(shaders ? Java : Game)}&classId={category}{(filter > 0 ? $"&categoryId={filter}" : "")}&pageSize={PageSize}&index={offset}&searchFilter={Uri.EscapeDataString(search.Trim()[..Math.Min(search.Trim().Length, 120)])}&sortField=2&sortOrder=desc", cancel);
        return ParseSearch(json.RootElement, category, !Category(category), shaders);
    }

    public static CursePage ParseSearch(JsonElement root, int category, bool worlds = false, bool shaders = false)
    {
        if (!worlds && !Category(category)) throw new ArgumentException("Unsupported pack category.");
        var result = new List<CursePack>();
        foreach (var mod in root.GetProperty("data").EnumerateArray())
        {
            if (Number(mod, "gameId") != (shaders ? Java : Game) || Number(mod, "classId") != category) continue;
            int id = Number(mod, "id"); string name = Text(mod, "name");
            if (id <= 0 || string.IsNullOrWhiteSpace(name)) continue;
            string url = mod.TryGetProperty("links", out var links) ? Text(links, "websiteUrl") : "";
            if (!Uri.TryCreate(url, UriKind.Absolute, out var website) || website.Scheme != "https" || !website.IsDefaultPort || website.UserInfo.Length != 0 || website.Host != "www.curseforge.com" || !website.AbsolutePath.StartsWith(shaders ? "/minecraft/shaders/" : "/minecraft-bedrock/", StringComparison.Ordinal)) continue;
            Uri? logo = mod.TryGetProperty("logo", out var image) && image.ValueKind == JsonValueKind.Object ? Cdn(Text(image, "thumbnailUrl")) : null;
            string authors = mod.TryGetProperty("authors", out var list) ? string.Join(", ", list.EnumerateArray().Take(4).Select(a => Text(a, "name"))) : "";
            bool allowed = !mod.TryGetProperty("allowModDistribution", out var distribution) || distribution.ValueKind != JsonValueKind.False;
            string summary = Text(mod, "summary");
            result.Add(new CursePack(id, name[..Math.Min(name.Length, 180)], summary[..Math.Min(summary.Length, 1000)], authors, logo, website, allowed));
        }
        bool next = root.TryGetProperty("pagination", out var page) && Number(page, "index") + Number(page, "resultCount") < Number(page, "totalCount");
        return new CursePage(result, next);
    }

    public static async Task<CurseFile?> LatestFileAsync(HttpClient http, string key, CursePack pack, CancellationToken cancel, bool world = false, bool shaders = false, bool skins = false)
    {
        if (!pack.CanDistribute) return null;
        using var json = await Request(http, key, $"mods/{pack.Id}/files?pageSize=50", cancel);
        return PickFile(json.RootElement, world, shaders, skins);
    }

    public static CurseFile? PickFile(JsonElement root, bool world = false, bool shaders = false, bool skins = false)
    {
        foreach (var file in root.GetProperty("data").EnumerateArray().OrderByDescending(f => Text(f, "fileDate")))
        {
            string name = Text(file, "fileName");
            if (!file.TryGetProperty("isAvailable", out var available) || available.ValueKind != JsonValueKind.True || !(world ? Path.GetExtension(name).Equals(".mcworld", StringComparison.OrdinalIgnoreCase) : shaders ? Path.GetExtension(name).Equals(".zip", StringComparison.OrdinalIgnoreCase) : skins ? Path.GetExtension(name).ToLowerInvariant() is ".mcpack" or ".zip" : IsPackFile(name)) || Path.GetFileName(name) != name || name.IndexOfAny(new[] { '/', '\\', ':' }) >= 0) continue;
            if (!file.TryGetProperty("fileLength", out var length) || !length.TryGetInt64(out long size) || size <= 0 || size > MaxDownload) continue;
            Uri? download = Cdn(Text(file, "downloadUrl"));
            string hash = file.TryGetProperty("hashes", out var hashes) ? hashes.EnumerateArray().Where(h => Number(h, "algo") == 1).Select(h => Text(h, "value")).FirstOrDefault() ?? "" : "";
            if (download == null || hash.Length != 40 || !hash.All(Uri.IsHexDigit)) continue;
            return new CurseFile(name, download, size, hash);
        }
        return null;
    }

    public static async Task<(int ClassId, (int Id, string Name)[] Categories)> WorldCategoriesAsync(HttpClient http, string key, CancellationToken cancel)
    {
        using var all = await Request(http, key, $"categories?gameId={Game}", cancel);
        var categories = all.RootElement.GetProperty("data");
        int classId = categories.EnumerateArray().Where(item => Text(item, "slug") == "maps")
            .Select(item => Number(item, "id")).FirstOrDefault();
        if (classId <= 0) throw new InvalidDataException("Bedrock maps category is unavailable.");
        using var maps = await Request(http, key, $"categories?gameId={Game}&classId={classId}", cancel);
        return (classId, maps.RootElement.GetProperty("data").EnumerateArray()
            .Where(item => Number(item, "id") > 0 && Number(item, "id") != classId && Text(item, "name").Length > 0)
            .Select(item => (Number(item, "id"), Text(item, "name"))).ToArray());
    }

    internal static void ValidateSkinPack(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (archive.Entries.Count > 10000 ||
            archive.Entries.Sum(entry => (decimal)entry.Length) > MaxDownload * 2 ||
            archive.Entries.Any(entry => !names.Add(entry.FullName.TrimEnd('/')) ||
                entry.FullName.StartsWith('/') || entry.FullName.Contains((char)92) ||
                entry.FullName.Contains(':') || entry.FullName.Split('/').Any(part => part is "." or "..")))
            throw new InvalidDataException("Invalid skin pack archive.");

        using var manifest = ReadMetadata("manifest.json");
        using var skins = ReadMetadata("skins.json");
        if (!manifest.RootElement.GetProperty("modules").EnumerateArray().Any(module => Text(module, "type") == "skin_pack") ||
            skins.RootElement.GetProperty("skins").GetArrayLength() == 0)
            throw new InvalidDataException("Expected a Bedrock skin pack.");

        JsonDocument ReadMetadata(string name)
        {
            var entry = archive.GetEntry(name);
            if (entry is null || entry.Length is <= 0 or > 4 * 1024 * 1024)
                throw new InvalidDataException("Missing or oversized skin pack metadata.");
            using var stream = entry.Open();
            return JsonDocument.Parse(stream);
        }
    }

    static async Task<HttpResponseMessage> CdnResponse(HttpClient http, Uri url, CancellationToken cancel)
    {
        for (int redirect = 0; redirect < 5; redirect++)
        {
            if (!IsCdn(url)) throw new InvalidDataException("Untrusted download address.");
            var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancel);
            if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is { } location)
            {
                url = new Uri(url, location); response.Dispose(); continue;
            }
            try { response.EnsureSuccessStatusCode(); return response; } catch { response.Dispose(); throw; }
        }
        throw new InvalidDataException("Too many download redirects.");
    }

    static async Task<byte[]> ReadBounded(HttpResponseMessage response, int limit, CancellationToken cancel)
    {
        if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException("Response exceeds the size limit.");
        using var output = new MemoryStream();
        await using var input = await response.Content.ReadAsStreamAsync(cancel);
        var buffer = new byte[81920]; int read;
        while ((read = await input.ReadAsync(buffer, cancel)) > 0)
        {
            if (output.Length + read > limit) throw new InvalidDataException("Response exceeds the size limit.");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    public static async Task<byte[]> LogoAsync(HttpClient http, Uri logo, CancellationToken cancel)
    {
        using var response = await CdnResponse(http, logo, cancel);
        return await ReadBounded(response, 2 * 1024 * 1024, cancel);
    }

    public static async Task DownloadAsync(HttpClient http, CurseFile file, string destination, CancellationToken cancel)
    {
        bool created = false;
        try
        {
            using var response = await CdnResponse(http, file.Download, cancel);
            await using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                created = true;
                await using var input = await response.Content.ReadAsStreamAsync(cancel);
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
                var buffer = new byte[81920]; long total = 0; int read;
                while ((read = await input.ReadAsync(buffer, cancel)) > 0)
                {
                    total += read;
                    if (total > file.Size || total > MaxDownload) throw new InvalidDataException("Unexpected pack size.");
                    hash.AppendData(buffer, 0, read); await output.WriteAsync(buffer.AsMemory(0, read), cancel);
                }
                if (total != file.Size || !CryptographicOperations.FixedTimeEquals(hash.GetHashAndReset(), Convert.FromHexString(file.Sha1))) throw new InvalidDataException("Pack verification failed.");
            }
            ValidateArchive(destination);
        }
        catch { if (created) { try { File.Delete(destination); } catch { } } throw; }
    }

    public static void ValidateArchive(string path)
    {
        long expanded = 0; int entries = 0;
        using var input = File.OpenRead(path);
        Inspect(input, 0);
        void Inspect(Stream stream, int depth)
        {
            if (depth > 4) throw new InvalidDataException("Nested packs are too deep.");
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, true);
            bool pack = false;
            foreach (var entry in zip.Entries)
            {
                if (++entries > 25000 || (expanded += entry.Length) > MaxDownload) throw new InvalidDataException("Expanded pack exceeds the limit.");
                if (entry.FullName.StartsWith('/') || entry.FullName.Contains('\\') || entry.FullName.Contains(':') || entry.FullName.Split('/').Any(p => p == "..")) throw new InvalidDataException("Invalid archive path.");
                if (entry.Name.EndsWith(".mcworld", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Worlds are not supported.");
                if (entry.Name.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
                {
                    if (entry.Length > 1024 * 1024) throw new InvalidDataException("Pack manifest exceeds the limit.");
                    using var manifest = entry.Open(); using var json = JsonDocument.Parse(manifest);
                    var modules = json.RootElement.GetProperty("modules");
                    if (modules.GetArrayLength() == 0 || modules.EnumerateArray().Any(m => Text(m, "type") is not ("resources" or "data" or "script"))) throw new InvalidDataException("Only resource and behavior packs are supported.");
                    pack = true;
                }
                else if (entry.Name.EndsWith(".mcpack", StringComparison.OrdinalIgnoreCase) || entry.Name.EndsWith(".mcaddon", StringComparison.OrdinalIgnoreCase))
                {
                    using var nested = new MemoryStream(); using var source = entry.Open();
                    var buffer = new byte[81920]; int read;
                    while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (nested.Length + read > MaxDownload || nested.Length + read > entry.Length) throw new InvalidDataException("Nested archive exceeds the limit.");
                        nested.Write(buffer, 0, read);
                    }
                    nested.Position = 0;
                    Inspect(nested, depth + 1); pack = true;
                }
            }
            if (!pack) throw new InvalidDataException("No valid Bedrock pack was found.");
        }
    }
}
