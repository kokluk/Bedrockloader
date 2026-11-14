using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace KoklukBedrockModloader;

internal sealed record SavedServer(Guid Id, string Name, string Host, int Port, bool Favorite, bool AutoPing);

internal static class BedrockServers
{
    internal const int DefaultPort = 19132;
    static readonly byte[] RakNetMagic = Convert.FromHexString("00FFFF00FEFEFEFEFDFDFDFD12345678");
    static readonly UTF8Encoding Utf8 = new(false, true);
    static readonly Regex GameLine = new(@"\A([0-9]+):([^:]+):(.+):([0-9]+):([0-9]+)\r?\z", RegexOptions.NonBacktracking);

    internal static string? NormalizeHost(string input)
    {
        string host = input.Trim();
        if (host.Length is 0 or > 254 || host.Contains('%')) return null;
        bool bracketed = host.StartsWith('[') && host.EndsWith(']');
        if (bracketed) host = host[1..^1];
        if (host.Contains('[') || host.Contains(']')) return null;
        if (host.Contains(':'))
            return IPAddress.TryParse(host, out var ipv6) && ipv6.AddressFamily == AddressFamily.InterNetworkV6
                ? (ipv6.IsIPv4MappedToIPv6 ? ipv6.MapToIPv4() : ipv6).ToString() : null;
        if (bracketed) return null;
        if (host.EndsWith('.')) host = host[..^1];
        if (host.All(c => char.IsAsciiDigit(c) || c == '.'))
        {
            string[] parts = host.Split('.');
            return parts.Length == 4 && parts.All(p => p.Length is > 0 and <= 3 &&
                byte.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                ? string.Join(".", parts.Select(p => byte.Parse(p, CultureInfo.InvariantCulture))) : null;
        }
        try { host = new IdnMapping { UseStd3AsciiRules = true }.GetAscii(host).ToLowerInvariant(); }
        catch (ArgumentException) { return null; }
        return host.Length <= 253 && host.Split('.').All(label => label.Length is > 0 and <= 63 &&
            label[0] != '-' && label[^1] != '-' && label.All(c => char.IsAsciiLetterOrDigit(c) || c == '-')) ? host : null;
    }

    internal static bool TryPort(string input, out int port) =>
        int.TryParse(input.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out port) && port is >= 1 and <= 65535;

    internal static string Endpoint(SavedServer server) =>
        (server.Host.Contains(':') ? "[" + server.Host + "]" : server.Host) + ":" + server.Port.ToString(CultureInfo.InvariantCulture);

    internal static async Task<long?> PingAsync(SavedServer server, CancellationToken cancel)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            var addresses = IPAddress.TryParse(server.Host, out var ip) ? [ip] : await Dns.GetHostAddressesAsync(server.Host, timeout.Token);
            var probes = addresses.Where(a => a.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
                .Take(6).Select(ProbeAsync).ToList();
            while (probes.Count != 0)
            {
                var completed = await Task.WhenAny(probes);
                probes.Remove(completed);
                if (await completed is { } elapsed) { timeout.Cancel(); return elapsed; }
            }
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException or ArgumentException) { }
        return null;

        async Task<long?> ProbeAsync(IPAddress address)
        {
            try
            {
                using var udp = new UdpClient(address.AddressFamily);
                udp.Connect(address, server.Port);
                var ping = new byte[33];
                ping[0] = 0x01;
                RandomNumberGenerator.Fill(ping.AsSpan(1, 8));
                RakNetMagic.CopyTo(ping, 9);
                RandomNumberGenerator.Fill(ping.AsSpan(25));
                long start = Stopwatch.GetTimestamp();
                await udp.SendAsync(ping.AsMemory(), timeout.Token);
                while (true)
                {
                    byte[] pong = (await udp.ReceiveAsync(timeout.Token)).Buffer;
                    if (pong.Length >= 35 && pong[0] == 0x1C &&
                        pong.AsSpan(1, 8).SequenceEqual(ping.AsSpan(1, 8)) &&
                        pong.AsSpan(17, 16).SequenceEqual(RakNetMagic) &&
                        BinaryPrimitives.ReadUInt16BigEndian(pong.AsSpan(33, 2)) <= pong.Length - 35)
                        return (long)Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                }
            }
            catch (Exception ex) when (ex is SocketException or OperationCanceledException or ObjectDisposedException) { return null; }
        }
    }

    internal sealed record GameEntry(int Line, string Key, string Timestamp, SavedServer Server);

    internal sealed class GameList
    {
        readonly byte[] original;
        readonly string[] lines;
        readonly bool hasBom;
        internal string Path { get; }
        internal IReadOnlyList<GameEntry> Entries { get; }
        internal int UnreadableLines { get; }

        internal GameList(string path)
        {
            Path = path;
            original = ReadBytes(path);
            hasBom = original.AsSpan().StartsWith(Encoding.UTF8.Preamble);
            lines = Utf8.GetString(original, hasBom ? 3 : 0, original.Length - (hasBom ? 3 : 0)).Split('\n');
            var entries = new List<GameEntry>();
            for (int index = 0; index < lines.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(lines[index])) continue;
                var match = GameLine.Match(lines[index]);
                if (!match.Success || NormalizeHost(match.Groups[3].Value) is not { } host ||
                    !TryPort(match.Groups[4].Value, out int port))
                {
                    UnreadableLines++;
                    continue;
                }
                entries.Add(new GameEntry(index, match.Groups[1].Value, match.Groups[5].Value,
                    new SavedServer(Guid.NewGuid(), match.Groups[2].Value, host, port, false, false)));
            }
            Entries = entries;
        }

        internal void Replace(GameEntry entry, SavedServer updated)
        {
            if (!Entries.Contains(entry) || updated.Host.Contains(':') || updated.Name.Contains(':') ||
                updated.Name.Any(char.IsControl) || UnreadableLines > 0 ||
                Entries.Any(other => other != entry && other.Server.Host == updated.Host && other.Server.Port == updated.Port))
                throw new InvalidDataException("Unsupported or duplicate Minecraft server record.");
            if (!ReadBytes(Path).AsSpan().SequenceEqual(original))
                throw new IOException("The Minecraft server list changed. Refresh and try again.");
            string[] next = (string[])lines.Clone();
            next[entry.Line] = FormattableString.Invariant($"{entry.Key}:{updated.Name}:{updated.Host}:{updated.Port}:{entry.Timestamp}")
                + (lines[entry.Line].EndsWith('\r') ? "\r" : "");
            string temporary = Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, string.Join("\n", next), new UTF8Encoding(hasBom));
                if (!BedrockSkinSource.IsGameClosed() || !ReadBytes(Path).AsSpan().SequenceEqual(original))
                    throw new IOException("The Minecraft server list is in use or changed. Refresh and try again.");
                File.Replace(temporary, Path, Path + "." + Guid.NewGuid().ToString("N") + ".bak");
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        static byte[] ReadBytes(string path)
        {
            using var input = File.OpenRead(path);
            if (input.Length > 1024 * 1024) throw new InvalidDataException("Minecraft server list is too large.");
            var bytes = new byte[(int)input.Length];
            input.ReadExactly(bytes);
            return bytes;
        }
    }
}
