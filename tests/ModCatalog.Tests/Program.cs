using System.Net;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using KoklukBedrockModloader;

if (args.Length is < 2 or > 3) throw new ArgumentException("Pass catalog.json and its ForceCloseOreUI.dll fixture, optionally --live.");
string json = File.ReadAllText(args[0]);
byte[] fixture = File.ReadAllBytes(args[1]);
var mods = ModCatalog.Parse(json);
int checks = 0;
void Check(bool success, string name) { if (!success) throw new Exception(name); Console.WriteLine("PASS " + name); checks++; }
void Reject(Action<JsonNode> mutate, string name)
{
    var root = JsonNode.Parse(json)!; mutate(root);
    try { ModCatalog.Parse(root.ToJsonString()); } catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException or ArgumentException) { Check(true, name); return; }
    throw new Exception("Expected rejection: " + name);
}
Check(mods.Count == 2, "two published mods parsed");
Check(mods[1].Variants.Count == 3 && mods[1].DefaultVariant == "26.20+", "MinecraftForFree exposes three versions with 26.20+ as default");
var older = ModCatalog.SelectVariant(mods[1], "1.21.130+");
Check(older.FileName == "MinecraftForFree.dll" && older.Download.AbsolutePath.Contains("1.21.130+") && older.Size == 55296, "older selection uses its own DLL while retaining one installation filename");
try { ModCatalog.SelectVariant(mods[1], "unknown"); throw new Exception("Expected unknown version rejection"); }
catch (InvalidDataException) { Check(true, "unknown version cannot be installed"); }
var reordered = JsonNode.Parse(json)!;
var versionList = reordered["mods"]![1]!["variants"]!.AsArray();
var reverse = versionList.Reverse().Select(v => v!.DeepClone()).ToArray(); versionList.Clear(); foreach (var variant in reverse) versionList.Add(variant);
var reorderedMod = ModCatalog.Parse(reordered.ToJsonString())[1];
Check(reorderedMod.DefaultVariant == "26.20+" && ModCatalog.SelectVariant(reorderedMod, reorderedMod.DefaultVariant).Download == mods[1].Download, "default remains 26.20+ when version list order changes");
Check(mods[0].MinimumVersion == "26.60+" && mods[1].MinimumVersion == "26.20+", "minimum versions remain separate from descriptions");
Check(mods[0].Developer == "QYCottage" && mods[1].Developer == "Kokluk", "developer names are separate from optional source links");
Check(mods[0].Discord?.AbsoluteUri == "https://discord.gg/gBaFaMQ", "ForceCloseOreUI source credit preserved");
Check(mods[0].Source?.AbsoluteUri == "https://github.com/QYCottage/ForceCloseOreUI", "ForceCloseOreUI original GitHub source preserved");
Check(mods[1].Discord == null && mods[1].Credit.Contains("Kokluk"), "Kokluk mod has its own author and no unrelated Discord link");
Reject(r => r["schemaVersion"] = 2, "unsupported catalog version rejected");
Reject(r => r["mods"]![0]!["fileName"] = "../evil.dll", "path traversal rejected");
Reject(r => r["mods"]![0]!["fileName"] = "program.exe", "non-DLL file name rejected");
Reject(r => r["mods"]![0]!["downloadUrl"] = "https://example.com/mod.dll", "outside repository download rejected");
Reject(r => r["mods"]![0]!["downloadUrl"] = "http://raw.githubusercontent.com/kokluk/bedrockmodsrepo/main/mod.dll", "HTTP download rejected");
Reject(r => r["mods"]![0]!["discordUrl"] = "https://discord.gg.example.com/gBaFaMQ", "fake Discord domain rejected");
Reject(r => r["mods"]![0]!["sourceUrl"] = "javascript:alert(1)", "non-HTTPS source link rejected");
Reject(r => r["mods"]![0]!["sha256"] = "bad", "missing valid digest rejected");
Reject(r => r["mods"]![0]!["size"] = ModCatalog.MaxDllBytes + 1, "oversized DLL rejected");
Reject(r => r["mods"]!.AsArray().Add(r["mods"]![0]!.DeepClone()), "duplicate destination rejected");
Reject(r => r["mods"]![1]!["defaultVariant"] = "missing", "missing default variant rejected");
Reject(r => r["mods"]![1]!["variants"]![1]!["downloadUrl"] = "https://example.com/mod.dll", "untrusted version download rejected");
Reject(r => r["mods"]![1]!["variants"]!.AsArray().Add(r["mods"]![1]!["variants"]![0]!.DeepClone()), "duplicate version labels rejected");
var path = Path.Combine(Path.GetTempPath(), "bedrockloader-catalog-test-" + Guid.NewGuid().ToString("N") + ".download");
try
{
    using var http = new HttpClient(new FixtureHandler(fixture));
    await ModCatalog.DownloadAsync(http, mods[0], path, CancellationToken.None);
    Check(File.ReadAllBytes(path).SequenceEqual(fixture), "original DLL downloaded byte-for-byte without execution");
    try { await ModCatalog.DownloadAsync(http, mods[0], path, CancellationToken.None); throw new Exception("Expected existing destination rejection"); }
    catch (IOException) { Check(File.ReadAllBytes(path).SequenceEqual(fixture), "existing destination is never overwritten or deleted"); }
    File.Delete(path);
    try { await ModCatalog.DownloadAsync(http, mods[0] with { Sha256 = new string('0', 64) }, path, CancellationToken.None); throw new Exception("Expected hash rejection"); }
    catch (InvalidDataException) { Check(!File.Exists(path), "tampered hash rejected and incomplete file removed"); }
    using var truncated = new HttpClient(new FixtureHandler(fixture[..^1]));
    try { await ModCatalog.DownloadAsync(truncated, mods[0], path, CancellationToken.None); throw new Exception("Expected truncated data rejection"); }
    catch (InvalidDataException) { Check(!File.Exists(path), "truncated DLL rejected"); }
    byte[] text = "this is not a DLL"u8.ToArray();
    using var invalid = new HttpClient(new FixtureHandler(text));
    try { await ModCatalog.DownloadAsync(invalid, mods[0] with { Size = text.Length, Sha256 = Convert.ToHexString(SHA256.HashData(text)) }, path, CancellationToken.None); throw new Exception("Expected DLL format rejection"); }
    catch (BadImageFormatException) { Check(!File.Exists(path), "matching hash does not bypass Windows DLL format verification"); }
    using var canceled = new CancellationTokenSource(); canceled.Cancel();
    try { await ModCatalog.DownloadAsync(http, mods[0], path, canceled.Token); throw new Exception("Expected cancellation"); }
    catch (OperationCanceledException) { Check(!File.Exists(path), "cancellation leaves no download"); }
}
finally { if (File.Exists(path)) File.Delete(path); }
if (args.Length == 3 && args[2] == "--live")
{
    using var http = new HttpClient();
    http.DefaultRequestHeaders.UserAgent.ParseAdd("BedrockLoader-Catalog-Verification/1.0");
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
    var live = await ModCatalog.FetchAsync(http, timeout.Token);
    Check(live.Count == 2 && live[0].Source == mods[0].Source && live[1].Credit == mods[1].Credit && live[0].Developer == mods[0].Developer && live[1].Developer == mods[1].Developer && live[0].Download == mods[0].Download && live[1].Download == mods[1].Download && live[1].Variants.Count == 3 && live[1].DefaultVariant == "26.20+", "live public catalog includes correct sources, folders and default version");
    foreach (var mod in live)
    {
        var versionsToTest = mod.Variants.Count == 0 ? new[] { mod } : mod.Variants.Select(v => ModCatalog.SelectVariant(mod, v.Version)).ToArray();
        foreach (var version in versionsToTest)
        {
            try
            {
                await ModCatalog.DownloadAsync(http, version, path, timeout.Token);
                Check(new FileInfo(path).Length == version.Size, "live download verified without execution: " + version.Name + " " + version.MinimumVersion);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }
    }
}
Console.WriteLine($"{checks} catalog checks passed. No DLL was executed; user mod folders were not changed.");

sealed class FixtureHandler(byte[] bytes) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
    }
}
