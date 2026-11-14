using System.Text.Json;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace KoklukBedrockModloader;

internal static class CinnabarNightlyCatalog
{
    // k1 public verification key embedded in the upstream Windows updater.
    const string Key = "paEwHeKOdH83Z1aTvcmJfcrjwPKzk1il5yDlko41ENU=";
    const string DownloadBase = "https://github.com/cinnabar-mc/cinnabar/releases/download/nightly/";
    internal sealed record Build(UpdateRelease Package, long Number, DateTimeOffset Date);

    public static async Task<Build> ReadAsync(HttpClient http, CancellationToken token)
    {
        using var release = JsonDocument.Parse(await http.GetStringAsync(
            "https://api.github.com/repos/cinnabar-mc/cinnabar/releases/tags/nightly", token));
        var root = release.RootElement;
        if (root.GetProperty("draft").GetBoolean() || !root.GetProperty("prerelease").GetBoolean() ||
            root.GetProperty("tag_name").GetString() != "nightly") throw new InvalidDataException("Invalid Cinnabar Nightly release.");
        using var response = await http.GetAsync(DownloadBase + "update-nightly.json", HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        byte[] buffer = new byte[65536];
        int count = await stream.ReadAtLeastAsync(buffer, buffer.Length, false, token);
        if (count == buffer.Length) throw new InvalidDataException("Nightly manifest exceeds limit.");
        using var envelope = JsonDocument.Parse(buffer.AsMemory(0, count));
        var signed = envelope.RootElement;
        if (signed.GetProperty("key_id").GetString() != "k1") throw new InvalidDataException("Unknown Nightly signing key.");
        byte[] payload = signed.GetProperty("manifest").GetBytesFromBase64();
        var verifier = new Ed25519Signer();
        verifier.Init(false, new Ed25519PublicKeyParameters(Convert.FromBase64String(Key), 0));
        verifier.BlockUpdate(payload, 0, payload.Length);
        if (!verifier.VerifySignature(signed.GetProperty("signature").GetBytesFromBase64()))
            throw new InvalidDataException("Nightly signature could not be verified.");

        using var manifest = JsonDocument.Parse(payload);
        var data = manifest.RootElement;
        if (data.GetProperty("schema").GetInt32() != 1 || data.GetProperty("channel").GetString() != "nightly" ||
            data.GetProperty("expires").GetDateTimeOffset() <= DateTimeOffset.UtcNow ||
            !Version.TryParse(data.GetProperty("version").GetString(), out var version))
            throw new InvalidDataException("Invalid or expired Nightly manifest.");
        long number = data.GetProperty("build_number").GetInt64();
        if (number <= 0) throw new InvalidDataException("Invalid Nightly build number.");
        var package = data.GetProperty("artifacts").GetProperty("windows-x86_64");
        string url = package.GetProperty("url").GetString() ?? "";
        string hash = package.GetProperty("sha256").GetString() ?? "";
        long size = package.GetProperty("size").GetInt64();
        if (hash.Length != 64 || !hash.All(Uri.IsHexDigit) || size is <= 0 or > ReleaseUpdates.MaxDownload ||
            url != DownloadBase + $"update-payload-{hash[..16]}-Cinnabar-x64.msi")
            throw new InvalidDataException("Invalid Nightly MSI metadata.");
        var asset = root.GetProperty("assets").EnumerateArray().FirstOrDefault(item => item.GetProperty("browser_download_url").GetString() == url);
        if (asset.ValueKind == JsonValueKind.Undefined || asset.GetProperty("state").GetString() != "uploaded" ||
            asset.GetProperty("size").GetInt64() != size || asset.GetProperty("digest").GetString() != "sha256:" + hash)
            throw new InvalidDataException("Nightly MSI does not match the release.");
        return new(new(version, "nightly", "", new Uri(DownloadBase.Replace("/download/", "/tag/").TrimEnd('/')), new Uri(url), hash, size),
            number, asset.GetProperty("updated_at").GetDateTimeOffset());
    }
}
