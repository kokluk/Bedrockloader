using KoklukBedrockModloader;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

int passed = 0;
void Assert(bool condition, string name) { if (!condition) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
async Task Reject(Func<Task> action, string name)
{
    try { await action(); } catch { passed++; Console.WriteLine("PASS " + name); return; }
    throw new Exception("Expected rejection: " + name);
}
string Json(string tag = "v0.17.11", string? digest = null, string? url = null, string? page = null, bool draft = false, bool prerelease = false, long size = 4) => JsonSerializer.Serialize(new
{
    tag_name = tag, draft, prerelease, body = "Changes", html_url = page ?? "https://github.com/kokluk/Bedrockloader/releases/tag/" + tag,
    assets = new[] { new { name = $"BedrockLoader-Setup-v{tag.TrimStart('v')}-x64.exe", state = "uploaded", size,
        browser_download_url = url ?? "https://github.com/kokluk/Bedrockloader/releases/download/" + tag + "/setup.exe", digest } }
});
string fakeDigest = "sha256:" + new string('a', 64);
const string bilingualNotes = "## Türkçe\r\n\r\nTürkçe notlar\r\n\r\n## English\r\n\r\nEnglish notes\r\n\r\n<!-- end-release-notes -->\r\n\r\nChecksum";
Assert(ReleaseUpdates.LocalizeNotes(bilingualNotes, false) == "Türkçe notlar", "Turkish release notes selected");
Assert(ReleaseUpdates.LocalizeNotes(bilingualNotes, true) == "English notes", "English release notes selected without checksum");
Assert(ReleaseUpdates.LocalizeNotes("Legacy notes", true) == "Legacy notes", "older release notes remain readable");
Assert(ReleaseUpdates.LocalizeNotes("## Türkçe\n\nNotlar\n\n## English\n\n**Interface**\n- First change\n\n**Updates**\n- Second change", true)
    == "Interface\n• First change\n\nUpdates\n• Second change", "localized sections retain paragraph breaks");
Assert(ReleaseUpdates.ParseVersion("v0.17.10") == ReleaseUpdates.CurrentVersion, "same version is not an update");
Assert(ReleaseUpdates.ParseVersion("v0.17.11") > ReleaseUpdates.CurrentVersion, "newer version comparison");
Assert(!ReleaseUpdates.ParseRelease(Json()).CanInstall, "missing hash blocks install");
Assert(!ReleaseUpdates.ParseRelease(Json(digest: fakeDigest, url: "https://evil.example/setup.exe")).CanInstall, "external installer URL rejected");
Assert(!ReleaseUpdates.ParseRelease(Json(digest: fakeDigest, url: "http://github.com/kokluk/Bedrockloader/releases/download/v0.14/setup.exe")).CanInstall, "HTTP installer URL rejected");
Assert(!ReleaseUpdates.ParseRelease(Json(digest: fakeDigest, url: "https://github.com/other/repo/releases/download/v0.14/setup.exe")).CanInstall, "wrong repository rejected");
Assert(!ReleaseUpdates.ParseRelease(Json(digest: fakeDigest, size: ReleaseUpdates.MaxDownload + 1)).CanInstall, "oversized release rejected");
await Reject(() => Task.FromResult(ReleaseUpdates.ParseRelease(Json(draft: true))), "draft rejected");
await Reject(() => Task.FromResult(ReleaseUpdates.ParseRelease(Json(prerelease: true))), "prerelease rejected");
await Reject(() => Task.FromResult(ReleaseUpdates.ParseRelease(Json(tag: "broken"))), "malformed version rejected");
await Reject(() => Task.FromResult(ReleaseUpdates.ParseRelease(Json(page: "https://evil.example/release"))), "external release page rejected");

using (var missing = new HttpClient(new Reply(() => new HttpResponseMessage(HttpStatusCode.NotFound))))
    Assert(await ReleaseUpdates.CheckAsync(missing, default) == null, "private or missing release is not reported as current");
using (var forbidden = new HttpClient(new Reply(() => new HttpResponseMessage(HttpStatusCode.Forbidden))))
    await Reject(async () => await ReleaseUpdates.CheckAsync(forbidden, default), "rate limit/access failure is reported");
using (var metadata = new HttpClient(new Reply(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Json(digest: fakeDigest)) })))
    Assert((await ReleaseUpdates.CheckAsync(metadata, default))?.CanInstall == true, "published verified asset discovered");

if (args.Length != 1) throw new ArgumentException("Provide the path to a Windows EXE fixture (never executed).");
byte[] fixture = await File.ReadAllBytesAsync(args[0]);
string hash = "sha256:" + Convert.ToHexString(SHA256.HashData(fixture));
var valid = ReleaseUpdates.ParseRelease(Json(digest: hash, size: fixture.Length));
Assert(!ReleaseUpdates.ShouldNotify(null), "missing release does not display a badge");
Assert(!ReleaseUpdates.ShouldNotify(valid with { Version = ReleaseUpdates.ParseVersion("v0.13") }), "older release does not display a badge");
Assert(!ReleaseUpdates.ShouldNotify(valid with { Version = ReleaseUpdates.CurrentVersion }), "current version does not display a badge");
Assert(ReleaseUpdates.ShouldNotify(valid), "new version displays an update badge");
Assert(ReleaseUpdates.ShouldNotify(valid with { Download = null, Sha256 = null }), "new release is notified even before an installer is published");
string directory = Path.Combine(Path.GetTempPath(), "bedrock-update-tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
using var download = new HttpClient(new Reply(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(fixture) }));
string target = Path.Combine(directory, "valid.download"); long progress = 0;
await ReleaseUpdates.DownloadAsync(download, valid, target, bytes => progress = bytes, default);
Assert(File.Exists(target) && progress == fixture.Length, "verified executable downloaded completely without executing");
string corrupt = Path.Combine(directory, "corrupt.download");
await Reject(() => ReleaseUpdates.DownloadAsync(download, valid with { Sha256 = new string('0', 64) }, corrupt, null, default), "tampered hash rejected");
Assert(!File.Exists(corrupt), "failed download cleaned up");
string existing = Path.Combine(directory, "existing.download"); await File.WriteAllTextAsync(existing, "keep");
await Reject(() => ReleaseUpdates.DownloadAsync(download, valid, existing, null, default), "existing target not overwritten");
Assert(await File.ReadAllTextAsync(existing) == "keep", "existing file preserved");
string truncated = Path.Combine(directory, "truncated.download");
await Reject(() => ReleaseUpdates.DownloadAsync(download, valid with { Size = fixture.Length + 1 }, truncated, null, default), "truncated response rejected");
Assert(!File.Exists(truncated), "truncated installer not installed");
using var canceled = new CancellationTokenSource(); canceled.Cancel();
await Reject(() => ReleaseUpdates.DownloadAsync(download, valid, Path.Combine(directory, "canceled"), null, canceled.Token), "cancellation honored");
File.Delete(target); File.Delete(existing); Directory.Delete(directory);
Console.WriteLine($"{passed} checks passed. No installer was executed.");

sealed class Reply(Func<HttpResponseMessage> response) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); return Task.FromResult(response());
    }
}
