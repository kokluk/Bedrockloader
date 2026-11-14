using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace KoklukBedrockModloader;

internal sealed class ClientProfile
{
    public string Name { get; set; } = "";
    public string Provider { get; set; } = "generic";
    public string TargetVersion { get; set; } = "";
    public string LeviExecutable { get; set; } = "";
    public string LeviInstance { get; set; } = "";
    public string LeviLauncherHash { get; set; } = "";
    public List<string> Enabled { get; set; } = [];
}

internal sealed record ClientBinary(string Name, string Path, string Hash, long Length, string Version,
    Machine Machine, IReadOnlyList<string> Imports)
{
    public override string ToString() => Name;

    public static ClientBinary Read(string path)
    {
        if (!System.IO.Path.GetExtension(path).Equals(".dll", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Expected a .dll file.");
        using var stream = File.OpenRead(path);
        using var reader = new PEReader(stream, PEStreamOptions.LeaveOpen);
        var headers = reader.PEHeaders;
        if (headers.PEHeader is not { } pe || !headers.IsDll)
            throw new InvalidDataException("Expected a Windows PE DLL.");
        var imports = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var directory = pe.ImportTableDirectory;
        if (directory.Size > 0)
        {
            if (directory.Size < 20 || directory.Size > 20 * 4096)
                throw new InvalidDataException("Invalid PE import directory.");
            var descriptors = reader.GetSectionData(directory.RelativeVirtualAddress).GetReader(0, directory.Size);
            while (descriptors.RemainingBytes >= 20)
            {
                descriptors.Offset += 12;
                int nameRva = descriptors.ReadInt32();
                descriptors.Offset += 4;
                if (nameRva == 0) break;
                var name = reader.GetSectionData(nameRva).GetReader();
                var bytes = new List<byte>();
                while (name.RemainingBytes > 0 && bytes.Count < 260)
                {
                    byte value = name.ReadByte();
                    if (value == 0) break;
                    bytes.Add(value);
                }
                var dependency = Encoding.ASCII.GetString(bytes.ToArray());
                if (dependency.Length > 0 && dependency.IndexOfAny(['/', (char)92, ':']) < 0) imports.Add(dependency);
            }
        }
        stream.Position = 0;
        string hash = Convert.ToHexString(SHA256.HashData(stream));
        var info = FileVersionInfo.GetVersionInfo(path);
        return new ClientBinary(System.IO.Path.GetFileName(path), path, hash, stream.Length,
            info.FileVersion ?? "", headers.CoffHeader.Machine, imports.Order().ToArray());
    }

    public string Architecture => Machine switch { Machine.Amd64 => "x64", Machine.I386 => "x86", Machine.Arm64 => "ARM64", _ => Machine.ToString() };
}

internal sealed record ClientProvider(string Id, string Name, string? OfficialUrl, string GuidanceEn, string GuidanceTr)
{
    public override string ToString() => Name;

    public static readonly ClientProvider[] All =
    [
        new("generic", "DLL", null, "No verified loader for this DLL.", "Bu DLL için doğrulanmış yükleyici yok."),
        new("oderso", "OderSoClient", "https://github.com/OderSoClient",
            "No documented external profile launch was verified.", "Belgelenmiş harici profil başlatma yöntemi doğrulanamadı."),
        new("flarial", "Flarial", "https://github.com/flarialmc/Flarial.Launcher",
            "Manage Flarial through its own launcher.", "Flarial’ı kendi başlatıcısı üzerinden yönet."),
        new("levi", "LeviLauncher / LiteLoader", "https://docs.levimc.org/LeviLauncher/guide/mods-integrations",
            "Import supported DLLs in LeviLauncher for the exact game instance.", "Desteklenen DLL’leri ilgili oyun sürümü için LeviLauncher içinden içe aktar.")
    ];
}

internal sealed record ClientGame(string Kind, string Version, string Directory, Machine Machine)
{
    public override string ToString() => $"{Kind} · {Version}";

    public static ClientGame[] Detect(string contentPath)
    {
        var games = new List<ClientGame>();
        foreach (string folder in MinecraftInstallation.FindGameDirectories(contentPath))
        {
            try
            {
                string exe = Path.Combine(folder, "Minecraft.Windows.exe");
                using var stream = File.OpenRead(exe);
                using var pe = new PEReader(stream);
                var version = FileVersionInfo.GetVersionInfo(exe);
                bool gdk = File.Exists(Path.Combine(folder, "MicrosoftGame.config"));
                string manifest = Path.Combine(folder, "AppxManifest.xml");
                string kind = gdk ? "GDK" : "Bedrock";
                string? packageVersion = null;
                if (File.Exists(manifest))
                {
                    using var xml = XmlReader.Create(manifest, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, MaxCharactersInDocument = 1024 * 1024 });
                    var identity = XDocument.Load(xml).Root?.Elements().FirstOrDefault(x => x.Name.LocalName == "Identity");
                    if ((string?)identity?.Attribute("Name") == "Microsoft.MinecraftUWP" && !gdk) kind = "UWP";
                    if ((string?)identity?.Attribute("Name") == "Microsoft.MinecraftWindows") kind = "GDK";
                    packageVersion = (string?)identity?.Attribute("Version");
                }
                string detected = version.FileMajorPart > 0
                    ? new Version(version.FileMajorPart, version.FileMinorPart, version.FileBuildPart, version.FilePrivatePart).ToString()
                    : System.Version.TryParse(packageVersion, out var parsed) ? parsed.ToString() : "—";
                games.Add(new ClientGame(kind, detected, folder, pe.PEHeaders.CoffHeader.Machine));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BadImageFormatException or XmlException) { }
        }
        return games.ToArray();
    }
}
