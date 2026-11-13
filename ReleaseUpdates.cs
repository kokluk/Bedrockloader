using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace KoklukBedrockModloader;

internal sealed record UpdateRelease(Version Version, string Tag, string Notes, Uri Page, Uri? Download, string? Sha256, long Size)
{
    public bool CanInstall => Download != null && Sha256 != null && Size > 0;
}

internal static class ReleaseUpdates
{
    public static readonly Version CurrentVersion = new(0, 17, 10, 0);
    public static bool ShouldNotify(UpdateRelease? release) => release != null && release.Version > CurrentVersion;
    public const string ReleasesPage = "https://github.com/kokluk/Bedrockloader/releases";
    public const string LatestApi = "https://api.github.com/repos/kokluk/Bedrockloader/releases/latest";
    public const long MaxDownload = 300L * 1024 * 1024;

    public static string LocalizeNotes(string notes, bool english)
    {
        const string turkishHeader = "## Türkçe\n";
        const string englishHeader = "\n## English\n";
        const string endMarker = "\n<!-- end-release-notes -->";
        string text = notes.ReplaceLineEndings("\n").Trim();
        if (!text.StartsWith(turkishHeader, StringComparison.Ordinal)) return notes;
        int englishStart = text.IndexOf(englishHeader, turkishHeader.Length - 1, StringComparison.Ordinal);
        if (englishStart < 0) return notes;

        string turkishNotes = text[turkishHeader.Length..Math.Max(turkishHeader.Length, englishStart)].Trim();
        string englishNotes = text[(englishStart + englishHeader.Length)..];
        int end = englishNotes.IndexOf(endMarker, StringComparison.Ordinal);
        if (end >= 0) englishNotes = englishNotes[..end];
        englishNotes = englishNotes.Trim();
        string preferred = english ? englishNotes : turkishNotes;
        string localized = preferred.Length > 0 ? preferred : english ? turkishNotes : englishNotes;
        return string.Join("\n", localized.Split('\n').Select(line =>
            line.StartsWith("**", StringComparison.Ordinal) && line.EndsWith("**", StringComparison.Ordinal) && line.Length > 4
                ? line[2..^2]
                : line.StartsWith("- ", StringComparison.Ordinal) ? "• " + line[2..] : line));
    }

    public static Version ParseVersion(string tag)
    {
        if (!Version.TryParse(tag.Trim().TrimStart('v', 'V'), out var version) || version.Major < 0)
            throw new InvalidDataException("Release version is invalid / Sürüm numarası geçersiz.");
        return new Version(version.Major, version.Minor, Math.Max(0, version.Build), Math.Max(0, version.Revision));
    }

    static bool TrustedUrl(string url, string prefix, out Uri? uri)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out uri) && uri.Scheme == "https" && uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            && uri.IsDefaultPort && uri.UserInfo.Length == 0 && uri.Fragment.Length == 0 && uri.Query.Length == 0
            && uri.AbsolutePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    public static UpdateRelease ParseRelease(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean())
            throw new InvalidDataException("Release is not stable / Kararlı sürüm değil.");
        string tag = root.GetProperty("tag_name").GetString() ?? "";
        Version version = ParseVersion(tag);
        string pageUrl = root.GetProperty("html_url").GetString() ?? "";
        if (!TrustedUrl(pageUrl, "/kokluk/Bedrockloader/releases/tag/", out var page))
            throw new InvalidDataException("Unexpected release source / Beklenmeyen sürüm kaynağı.");
        string notes = root.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "";
        Uri? download = null; string? digest = null; long size = 0;
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            string name = asset.GetProperty("name").GetString() ?? "";
            if (!name.Equals($"BedrockLoader-Setup-v{tag.TrimStart('v', 'V')}-x64.exe", StringComparison.OrdinalIgnoreCase)) continue;
            if (!asset.TryGetProperty("state", out var state) || state.GetString() != "uploaded") continue;
            long candidateSize = asset.GetProperty("size").GetInt64();
            if (candidateSize <= 0 || candidateSize > MaxDownload) continue;
            if (!TrustedUrl(asset.GetProperty("browser_download_url").GetString() ?? "", "/kokluk/Bedrockloader/releases/download/", out var uri)) continue;
            string hash = asset.TryGetProperty("digest", out var hashValue) ? hashValue.GetString() ?? "" : "";
            if (!hash.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) || hash.Length != 71 || !hash[7..].All(Uri.IsHexDigit)) continue;
            download = uri; digest = hash[7..]; size = candidateSize; break;
        }
        return new UpdateRelease(version, tag, notes, page!, download, digest, size);
    }

    public static async Task<UpdateRelease?> CheckAsync(HttpClient http, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestApi);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > 1024 * 1024) throw new InvalidDataException("Release response is too large.");
        await using var input = await response.Content.ReadAsStreamAsync(token);
        using var memory = new MemoryStream();
        var buffer = new byte[16384]; int count;
        while ((count = await input.ReadAsync(buffer, token)) > 0)
        {
            if (memory.Length + count > 1024 * 1024) throw new InvalidDataException("Release response is too large.");
            memory.Write(buffer, 0, count);
        }
        return ParseRelease(System.Text.Encoding.UTF8.GetString(memory.ToArray()));
    }

    public static Task DownloadAsync(HttpClient http, UpdateRelease release, string path, Action<long>? progress, CancellationToken token) =>
        DownloadVerifiedAsync(http, release, path, progress, token, false);

    internal static async Task DownloadVerifiedAsync(HttpClient http, UpdateRelease release, string path, Action<long>? progress, CancellationToken token, bool? dll)
    {
        if (!release.CanInstall) throw new InvalidDataException("No verified installer / Doğrulanabilir kurulum dosyası yok.");
        using var response = await http.GetAsync(release.Download, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is long length && length != release.Size) throw new InvalidDataException("Installer size mismatch / Kurulum boyutu eşleşmiyor.");
        bool created = false;
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                created = true;
                await using var input = await response.Content.ReadAsStreamAsync(token);
                var buffer = new byte[81920]; long total = 0; int count;
                while ((count = await input.ReadAsync(buffer, token)) > 0)
                {
                    total += count;
                    if (total > release.Size || total > MaxDownload) throw new InvalidDataException("Installer size mismatch / Kurulum boyutu eşleşmiyor.");
                    hash.AppendData(buffer, 0, count);
                    await output.WriteAsync(buffer.AsMemory(0, count), token);
                    progress?.Invoke(total);
                }
                if (total != release.Size) throw new InvalidDataException("Incomplete download / Eksik indirme.");
                if (!Convert.ToHexString(hash.GetHashAndReset()).Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("SHA-256 verification failed / SHA-256 doğrulaması başarısız.");
            }
            if (dll == null) return;
            using var executable = File.OpenRead(path);
            using var pe = new System.Reflection.PortableExecutable.PEReader(executable);
            if (pe.PEHeaders.PEHeader == null || pe.PEHeaders.IsDll != dll)
                throw new InvalidDataException(dll == true ? "Expected a Windows DLL." : "Installer is not an executable / Kurulum dosyası yürütülebilir bir dosya değil.");
        }
        catch
        {
            if (created) File.Delete(path);
            throw;
        }
    }
}
