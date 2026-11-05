using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using KoklukBedrockModloader;

static void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException(name);
    Console.WriteLine("PASS " + name);
}

Check(BedrockServers.NormalizeHost(" PLAY.Example.NET. ") == "play.example.net", "DNS normalization");
Check(BedrockServers.NormalizeHost("BÜCHER.example") == "xn--bcher-kva.example", "international DNS normalization");
Check(BedrockServers.NormalizeHost("[2001:0DB8:0:0::1]") == "2001:db8::1", "bracketed IPv6");
Check(BedrockServers.NormalizeHost("::ffff:192.0.2.1") == "192.0.2.1", "mapped IPv4 normalization");
Check(BedrockServers.NormalizeHost("192.000.002.001") == "192.0.2.1", "IPv4 normalization");
foreach (string value in new[] { "", "256.1.1.1", "1.2.3", "bad..example", "-bad.example", "https://example.org", "[127.0.0.1]", "[::1]:19132" })
    Check(BedrockServers.NormalizeHost(value) == null, "reject " + value);
Check(BedrockServers.TryPort("1", out _) && BedrockServers.TryPort("65535", out _) &&
    !BedrockServers.TryPort("0", out _) && !BedrockServers.TryPort("65536", out _), "port boundaries");

var directory = Directory.CreateTempSubdirectory();
try
{
    string file = Path.Combine(directory.FullName, "external_servers.txt");
    const string other = "7:Other:[2001:db8::1]:19133:456\n";
    File.WriteAllText(file, "1:First:example.org:19132:123\r\n" + other, new UTF8Encoding(true));
    var list = new BedrockServers.GameList(file);
    Check(list.UnreadableLines == 0 && list.Entries.Count == 2 &&
        list.Entries[1].Server.Host == "2001:db8::1", "Minecraft lines with IPv6");
    var first = list.Entries[0];
    list.Replace(first, first.Server with { Name = "Updated", Host = "other.example" });
    string result = File.ReadAllText(file);
    Check(result == "1:Updated:other.example:19132:123\r\n" + other, "editing preserves other rows and metadata");
    Check(File.ReadAllBytes(file).AsSpan().StartsWith(Encoding.UTF8.Preamble), "editing preserves UTF-8 BOM");
    Check(Directory.GetFiles(directory.FullName, "*.bak").Length == 1, "original is backed up");
    try
    {
        list.Replace(first, first.Server with { Name = "Stale" });
        throw new Exception("stale edit accepted");
    }
    catch (IOException) { Check(File.ReadAllText(file) == result, "stale edits are rejected"); }
    File.AppendAllText(file, "unknown format\n");
    list = new BedrockServers.GameList(file);
    try
    {
        list.Replace(list.Entries[0], first.Server);
        throw new Exception("unknown format edit accepted");
    }
    catch (InvalidDataException) { Check(list.UnreadableLines == 1, "unknown formats are protected"); }
}
finally { directory.Delete(true); }

using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
var endpoint = (IPEndPoint)server.Client.LocalEndPoint!;
var ping = BedrockServers.PingAsync(new SavedServer(Guid.NewGuid(), "Loopback", "127.0.0.1", endpoint.Port, false, true), timeout.Token);
var packet = await server.ReceiveAsync(timeout.Token);
Check(packet.Buffer.Length == 33 && packet.Buffer[0] == 0x01, "RakNet ping packet");
var pong = new byte[39];
pong[0] = 0x1C;
packet.Buffer.AsSpan(1, 8).CopyTo(pong.AsSpan(1, 8));
packet.Buffer.AsSpan(9, 16).CopyTo(pong.AsSpan(17, 16));
BinaryPrimitives.WriteUInt16BigEndian(pong.AsSpan(33, 2), 4);
"MCPE"u8.CopyTo(pong.AsSpan(35));
await server.SendAsync(pong, packet.RemoteEndPoint, timeout.Token);
Check(await ping != null, "RakNet pong is accepted");

namespace KoklukBedrockModloader
{
    static class BedrockSkinSource
    {
        internal static bool IsGameClosed() => true;
    }
}
