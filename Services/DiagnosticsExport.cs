using System.IO.Compression;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    void ExportDiagnostics()
    {
        using var picker = new SaveFileDialog { Filter = "ZIP (*.zip)|*.zip",
            FileName = $"BedrockLoader-logs-{DateTime.Now:yyyyMMdd-HHmm}.zip" };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        string temporary = picker.FileName + $".{Guid.NewGuid():N}.tmp";
        try
        {
            using (var output = File.Open(temporary, FileMode.CreateNew))
            using (var zip = new ZipArchive(output, ZipArchiveMode.Create))
            {
                using (var writer = new StreamWriter(zip.CreateEntry("BedrockLoader.log").Open()))
                    writer.Write(ReadLogs());
                AddReports(appDataRoot, "crashes", "BedrockLoader");
                AddReports(CinnabarDataRoot(), "crashes", "Cinnabar");
                AddReports(CinnabarDataRoot(), "logs", "Cinnabar");

                void AddReports(string root, string folder, string prefix)
                {
                    if (!Directory.Exists(root)) return;
                    string path = BedrockSkinSource.SafeChild(root, folder);
                    if (!Directory.Exists(path)) return;
                    foreach (var report in new DirectoryInfo(path).EnumerateFiles()
                        .Where(file => !file.Attributes.HasFlag(FileAttributes.ReparsePoint) &&
                            file.Extension.ToLowerInvariant() is ".log" or ".txt" or ".json" or ".dmp" &&
                            file.Length <= 16 * 1024 * 1024)
                        .OrderByDescending(file => file.LastWriteTimeUtc).Take(10))
                        zip.CreateEntryFromFile(report.FullName, $"{prefix}/{folder}/{report.Name}");
                }
            }
            File.Move(temporary, picker.FileName, true);
            UpdateStatus(T("Diagnostics exported.", "Tanılama kayıtları dışa aktarıldı."));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        { ShowThemedPopup(T("Export failed", "Dışa aktarma başarısız"), ex.Message); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
