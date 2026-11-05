using KoklukBedrockModloader;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

int passed = 0;
void Assert(bool condition, string label) { if (!condition) throw new Exception(label); passed++; Console.WriteLine("PASS " + label); }
async Task Reject(Func<Task> action, string label) { try { await action(); } catch { passed++; Console.WriteLine("PASS " + label); return; } throw new Exception("Expected rejection: " + label); }
string Catalog(int category, int game = 78022, bool allowed = true, string website = "https://www.curseforge.com/minecraft-bedrock/texture-packs/example") => JsonSerializer.Serialize(new
{
    data = new[] { new { id = 123, gameId = game, classId = category, name = "Example", summary = "Test pack", authors = new[] { new { name = "Creator" } }, logo = new { thumbnailUrl = "https://media.forgecdn.net/avatars/example.png" }, links = new { websiteUrl = website }, allowModDistribution = allowed } },
    pagination = new { index = 0, resultCount = 2, totalCount = 8 }
});
CursePage Parse(string json, int category) { using var doc = JsonDocument.Parse(json); return CurseForgeApi.ParseSearch(doc.RootElement, category); }
Assert(Parse(Catalog(6929), 6929).Packs.Count == 1, "resource category parsed");
Assert(Parse(Catalog(4984), 4984).Packs.Count == 1, "behavior category parsed");
Assert(Parse(Catalog(6913), 6929).Packs.Count == 0, "world class excluded");
Assert(Parse(Catalog(6929, 432), 6929).Packs.Count == 0, "Java projects excluded");
Assert(Parse(Catalog(4984), 6929).Packs.Count == 0, "categories stay separated");
Assert(!Parse(Catalog(6929, allowed: false), 6929).Packs.Single().CanDistribute, "distribution opt-out respected");
Assert(Parse(Catalog(6929, website: "https://example.com/"), 6929).Packs.Count == 0, "untrusted project URL excluded");
Assert(Parse(Catalog(6929), 6929).HasNext, "pagination metadata used");
Assert(!CurseForgeApi.IsCdn(new Uri("https://forgecdn.net.evil.example/file")), "CDN lookalike rejected");
Assert(!CurseForgeApi.IsCdn(new Uri("http://edge.forgecdn.net/file")), "HTTP CDN rejected");
Assert(!CurseForgeApi.IsCdn(new Uri("https://name:secret@edge.forgecdn.net/file")), "userinfo rejected");
Assert(!CurseForgeApi.IsPackFile("example.mcworld"), "world extension excluded");
CurseFile? FileChoice(string name = "example.mcpack", string url = "https://edge.forgecdn.net/files/example.mcpack", long size = 1024, string? hash = null)
{
    using var json = JsonDocument.Parse(JsonSerializer.Serialize(new { data = new[] { new { fileName = name, isAvailable = true, fileLength = size, downloadUrl = url, fileDate = "2026-10-28T00:00:00Z", hashes = new[] { new { algo = 1, value = hash ?? new string('a', 40) } } } } }));
    return CurseForgeApi.PickFile(json.RootElement);
}
Assert(FileChoice() is not null, "available Bedrock file selected");
Assert(FileChoice("world.mcworld") is null, "world downloads never selected");
Assert(FileChoice("../file.mcpack") is null, "unsafe file name rejected");
Assert(FileChoice(url: "https://example.com/file.mcpack") is null, "untrusted download rejected");
Assert(FileChoice(size: CurseForgeApi.MaxDownload + 1) is null, "oversized download rejected");
Assert(FileChoice(hash: "bad") is null, "missing verification hash rejected");

using (var handler = new FakeHandler(request =>
{
    Assert(request.RequestUri!.Host == "api.curseforge.com", "key sent only to API host");
    Assert(request.Headers.GetValues("x-api-key").Single() == "fixture-key", "API key sent in header");
    Assert(!request.RequestUri.AbsoluteUri.Contains("fixture-key"), "key absent from URL");
    Assert(request.RequestUri.Query.Contains("classId=6929"), "request uses resource class");
    return Reply(Encoding.UTF8.GetBytes(Catalog(6929)), "application/json");
}))
using (var http = new HttpClient(handler))
    Assert((await CurseForgeApi.SearchAsync(http, "fixture-key", 6929, "test", 0, default)).Packs.Count == 1, "search client parses API response");

byte[] Archive(string type, string path = "manifest.json")
{
    using var memory = new MemoryStream();
    using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
    using (var writer = new StreamWriter(zip.CreateEntry(path).Open())) writer.Write(JsonSerializer.Serialize(new { modules = new[] { new { type } } }));
    return memory.ToArray();
}
string root = Path.Combine(Path.GetTempPath(), "bedrock-curse-tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
try
{
    byte[] valid = Archive("resources");
    var file = new CurseFile("example.mcpack", new Uri("https://edge.forgecdn.net/files/example.mcpack"), valid.Length, Convert.ToHexString(SHA1.HashData(valid)));
    using var handler = new FakeHandler(request =>
    {
        Assert(!request.Headers.Contains("x-api-key"), "key never forwarded to CDN");
        return Reply(valid, "application/octet-stream");
    });
    using var http = new HttpClient(handler);
    string output = Path.Combine(root, "pack.mcpack");
    await CurseForgeApi.DownloadAsync(http, file, output, default);
    Assert(File.ReadAllBytes(output).SequenceEqual(valid), "verified archive downloaded unchanged");
    await Reject(() => CurseForgeApi.DownloadAsync(http, file, output, default), "existing file is not overwritten");
    Assert(File.ReadAllBytes(output).SequenceEqual(valid), "existing file preserved");
    string bad = Path.Combine(root, "bad.mcpack");
    await Reject(() => CurseForgeApi.DownloadAsync(http, file with { Sha1 = new string('0', 40) }, bad, default), "hash mismatch rejected");
    Assert(!File.Exists(bad), "failed download cleaned up");
    await Reject(() => CurseForgeApi.DownloadAsync(http, file with { Size = valid.Length + 4 }, bad, default), "truncated download rejected");
    foreach (string type in new[] { "resources", "data", "script" })
    {
        string path = Path.Combine(root, type + ".zip"); File.WriteAllBytes(path, Archive(type)); CurseForgeApi.ValidateArchive(path); Assert(true, type + " manifest accepted");
    }
    foreach (string type in new[] { "world_template", "skin_pack", "unknown" })
    {
        string path = Path.Combine(root, type + ".zip"); File.WriteAllBytes(path, Archive(type));
        await Reject(() => { CurseForgeApi.ValidateArchive(path); return Task.CompletedTask; }, type + " manifest rejected");
    }
    string traversal = Path.Combine(root, "traversal.zip"); File.WriteAllBytes(traversal, Archive("resources", "../manifest.json"));
    await Reject(() => { CurseForgeApi.ValidateArchive(traversal); return Task.CompletedTask; }, "archive traversal rejected");
    foreach (string type in new[] { "resources", "world_template" })
    {
        string path = Path.Combine(root, type + ".mcaddon");
        using (var disk = File.Create(path))
        using (var bundle = new ZipArchive(disk, ZipArchiveMode.Create))
        using (var entry = bundle.CreateEntry("pack.mcpack").Open()) entry.Write(Archive(type));
        if (type == "resources") { CurseForgeApi.ValidateArchive(path); Assert(true, "nested resource pack accepted"); }
        else await Reject(() => { CurseForgeApi.ValidateArchive(path); return Task.CompletedTask; }, "world hidden inside addon rejected");
    }
    using var redirectHandler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.Redirect) { Headers = { Location = new Uri("https://example.com/private") } });
    using var redirectHttp = new HttpClient(redirectHandler);
    await Reject(() => CurseForgeApi.DownloadAsync(redirectHttp, file, bad, default), "redirect outside CDN rejected");
    using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
    await Reject(() => CurseForgeApi.DownloadAsync(http, file, bad, cancellation.Token), "cancellation honored");
}
finally { Directory.Delete(root, true); }
foreach (string fixture in args)
{
    if (Path.GetExtension(fixture) == ".json")
    {
        using var json = JsonDocument.Parse(File.ReadAllText(fixture));
        Assert(CurseForgeApi.PickFile(json.RootElement) != null, "live file metadata selects a verified Bedrock download");
    }
    else { CurseForgeApi.ValidateArchive(fixture); Assert(true, "live CurseForge download has valid pack manifests"); }
}
Console.WriteLine($"{passed} checks passed. No user packs were installed or changed.");

static HttpResponseMessage Reply(byte[] bytes, string type)
{
    var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
    response.Content.Headers.ContentType = new(type); return response;
}
sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel) { cancel.ThrowIfCancellationRequested(); return Task.FromResult(respond(request)); }
}
