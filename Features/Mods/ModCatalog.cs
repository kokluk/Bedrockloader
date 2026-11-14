using System.Net.Http;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

namespace KoklukBedrockModloader;

internal sealed record CatalogVariant(string Version, Uri Download, string Sha256, long Size);

internal sealed record CatalogMod(string Name, string Description, string MinimumVersion, string Developer, string Credit, string FileName, Uri Download, Uri? Discord, Uri? Source, string Sha256, long Size)
{
    public IReadOnlyList<CatalogVariant> Variants { get; init; } = Array.Empty<CatalogVariant>();
    public string DefaultVariant { get; init; } = "";
}

internal static class ModCatalog
{
    public const string Repository = "https://github.com/kokluk/bedrockmodsrepo";
    public const string Manifest = "https://api.github.com/repos/kokluk/bedrockmodsrepo/contents/catalog.json";
    public const long MaxDllBytes = 64L * 1024 * 1024;

    internal static IReadOnlyList<CatalogMod> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1) throw new InvalidDataException("Unsupported mod catalog version.");
        var entries = root.GetProperty("mods");
        if (entries.GetArrayLength() > 100) throw new InvalidDataException("The mod catalog contains too many entries.");
        var result = new List<CatalogMod>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in entries.EnumerateArray())
        {
            string Read(string key) => item.TryGetProperty(key, out var value) ? value.GetString() ?? "" : "";
            string name = Read("name"), description = Read("description"), minimumVersion = Read("minimumVersion"), developer = Read("developer"), credit = Read("credit"), fileName = Read("fileName"), hash = Read("sha256");
            long size = item.GetProperty("size").GetInt64();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 100 || minimumVersion.Length > 40 || developer.Length > 100 || description.Length > 2000 || credit.Length > 200 ||
                fileName.Length > 120 || !fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || fileName.Contains('/') || fileName.Contains('\\') ||
                !names.Add(fileName) || size <= 0 || size > MaxDllBytes || hash.Length != 64 || !hash.All(Uri.IsHexDigit))
                throw new InvalidDataException("Invalid mod entry / Geçersiz mod kaydı.");
            if (!Uri.TryCreate(Read("downloadUrl"), UriKind.Absolute, out var download) || !TrustedDownload(download))
                throw new InvalidDataException("Unexpected mod download source / Beklenmeyen mod kaynağı.");
            Uri? discord = null;
            if (Read("discordUrl") is { Length: > 0 } invite &&
                (!Uri.TryCreate(invite, UriKind.Absolute, out discord) || !TrustedDiscord(discord)))
                throw new InvalidDataException("Invalid Discord invitation / Geçersiz Discord daveti.");
            Uri? source = null;
            if (Read("sourceUrl") is { Length: > 0 } sourceUrl &&
                (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out source) || !BasicHttps(source) || !source.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Invalid GitHub source / Geçersiz GitHub kaynağı.");
            var variants = new List<CatalogVariant>();
            string defaultVariant = Read("defaultVariant");
            if (item.TryGetProperty("variants", out var variantEntries))
            {
                if (variantEntries.GetArrayLength() > 20) throw new InvalidDataException("Too many mod versions.");
                var versions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var variant in variantEntries.EnumerateArray())
                {
                    string version = variant.GetProperty("version").GetString() ?? "";
                    string variantHash = variant.GetProperty("sha256").GetString() ?? "";
                    long variantSize = variant.GetProperty("size").GetInt64();
                    if (string.IsNullOrWhiteSpace(version) || version.Length > 40 || !versions.Add(version) ||
                        variantHash.Length != 64 || !variantHash.All(Uri.IsHexDigit) || variantSize <= 0 || variantSize > MaxDllBytes ||
                        !Uri.TryCreate(variant.GetProperty("downloadUrl").GetString(), UriKind.Absolute, out var variantDownload) || !TrustedDownload(variantDownload))
                        throw new InvalidDataException("Invalid mod version / Geçersiz mod sürümü.");
                    variants.Add(new CatalogVariant(version, variantDownload, variantHash, variantSize));
                }
                if (variants.Count > 0)
                {
                    var selected = variants.SingleOrDefault(v => v.Version == defaultVariant);
                    if (selected == null || selected.Download != download || selected.Size != size || !selected.Sha256.Equals(hash, StringComparison.OrdinalIgnoreCase) || selected.Version != minimumVersion)
                        throw new InvalidDataException("Invalid default mod version / Geçersiz varsayılan mod sürümü.");
                }
            }
            result.Add(new CatalogMod(name, description, minimumVersion, developer, credit, fileName, download, discord, source, hash, size) { Variants = variants, DefaultVariant = defaultVariant });
        }
        return result;
    }

    internal static CatalogMod SelectVariant(CatalogMod mod, string version)
    {
        var selected = mod.Variants.SingleOrDefault(v => v.Version == version) ?? throw new InvalidDataException("Unknown mod version / Bilinmeyen mod sürümü.");
        return mod with { Download = selected.Download, Sha256 = selected.Sha256, Size = selected.Size, MinimumVersion = selected.Version };
    }

    static bool BasicHttps(Uri uri) => uri.Scheme == "https" && uri.IsDefaultPort && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0;
    static bool TrustedDownload(Uri uri) => BasicHttps(uri) &&
        ((uri.Host.Equals("raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase) && uri.AbsolutePath.StartsWith("/kokluk/bedrockmodsrepo/main/", StringComparison.OrdinalIgnoreCase)) ||
         (uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) && uri.AbsolutePath.StartsWith("/kokluk/bedrockmodsrepo/releases/download/", StringComparison.OrdinalIgnoreCase)));
    static bool TrustedDiscord(Uri uri) => BasicHttps(uri) &&
        ((uri.Host.Equals("discord.gg", StringComparison.OrdinalIgnoreCase) && uri.AbsolutePath.Length > 1) ||
         (uri.Host.Equals("discord.com", StringComparison.OrdinalIgnoreCase) && uri.AbsolutePath.StartsWith("/invite/", StringComparison.OrdinalIgnoreCase) && uri.AbsolutePath.Length > 8));

    internal static async Task<IReadOnlyList<CatalogMod>> FetchAsync(HttpClient http, CancellationToken token)
    {
        // Read the current file through GitHub's contents API. The raw CDN can
        // serve stale catalog metadata after a repository edit.
        using var request = new HttpRequestMessage(HttpMethod.Get, Manifest + "?t=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        request.Headers.Accept.ParseAdd("application/vnd.github.raw+json");
        request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        using var memory = new MemoryStream();
        await using var input = await response.Content.ReadAsStreamAsync(token);
        var buffer = new byte[8192]; int count;
        while ((count = await input.ReadAsync(buffer, token)) > 0)
        {
            if (memory.Length + count > 1024 * 1024) throw new InvalidDataException("The mod catalog is too large.");
            memory.Write(buffer, 0, count);
        }
        return Parse(System.Text.Encoding.UTF8.GetString(memory.ToArray()).TrimStart('\uFEFF'));
    }

    internal static async Task DownloadAsync(HttpClient http, CatalogMod mod, string path, CancellationToken token)
    {
        if (!TrustedDownload(mod.Download) || mod.Size <= 0 || mod.Size > MaxDllBytes) throw new InvalidDataException("Invalid mod download.");
        bool created = false;
        try
        {
            using var response = await http.GetAsync(mod.Download, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long length && length != mod.Size) throw new InvalidDataException("Mod size mismatch.");
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                created = true;
                await using var input = await response.Content.ReadAsStreamAsync(token);
                var buffer = new byte[81920]; long total = 0; int count;
                while ((count = await input.ReadAsync(buffer, token)) > 0)
                {
                    total += count;
                    if (total > mod.Size) throw new InvalidDataException("Mod size mismatch.");
                    hash.AppendData(buffer, 0, count);
                    await output.WriteAsync(buffer.AsMemory(0, count), token);
                }
                if (total != mod.Size || !Convert.ToHexString(hash.GetHashAndReset()).Equals(mod.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Mod verification failed / Mod doğrulanamadı.");
            }
            using var stream = File.OpenRead(path);
            using var pe = new PEReader(stream);
            if (pe.PEHeaders.PEHeader == null || (pe.PEHeaders.CoffHeader.Characteristics & Characteristics.Dll) == 0)
                throw new InvalidDataException("The downloaded file is not a Windows DLL.");
        }
        catch { if (created) File.Delete(path); throw; }
    }
}
