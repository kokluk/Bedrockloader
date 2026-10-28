using System.Diagnostics;
using System.Net.Http;
using System.Reflection.PortableExecutable;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    const string ModloaderDownloadUrl = "https://github.com/kokluk/BedrockFree/releases/download/McBedrock/vcruntime140_1.dll";
    const string ModloaderFileName = "vcruntime140_1.dll";
    const long MaxModloaderBytes = 64L * 1024 * 1024;
    bool installingModloader;
    Button? installModloaderButton;

    async void InstallModloader()
    {
        if (installingModloader) return;
        installingModloader = true;
        if (installModloaderButton != null) installModloaderButton.Enabled = false;
        string? stagedFile = null;
        try
        {
            string folder = settings.ContentPath.Trim().Trim('"');
            if (!Directory.Exists(folder))
            {
                using var picker = new FolderBrowserDialog
                {
                    Description = T("Select Minecraft's Content folder", "Minecraft'ın Content klasörünü seç"),
                    UseDescriptionForTitle = true,
                    ShowNewFolderButton = false
                };
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                folder = picker.SelectedPath;
            }
            folder = Path.GetFullPath(folder);
            string target = Path.Combine(folder, ModloaderFileName);
            string consent = T(
                $"Download {ModloaderFileName} from the following link and install it in your Minecraft Content folder?\n\nSource:\n{ModloaderDownloadUrl}\n\nDestination:\n{target}\n\nThe download and installation will only start if you allow installation.",
                $"{ModloaderFileName} dosyası aşağıdaki bağlantıdan indirilip Minecraft Content klasörüne kurulsun mu?\n\nKaynak:\n{ModloaderDownloadUrl}\n\nHedef:\n{target}\n\nİndirme ve kurulum yalnızca kuruluma izin verirsen başlayacak.");
            if (ShowThemedPopup(T("Install modloader", "Modloader kurulumu"), consent, confirmation: true) != DialogResult.Yes) return;

            bool replaceExisting = File.Exists(target);
            if (replaceExisting && ShowThemedPopup(T("Replace existing file?", "Var olan dosya değiştirilsin mi?"),
                T($"A file already exists here:\n\n{target}\n\nReplace it with the downloaded modloader? Close Minecraft before continuing.",
                  $"Bu konumda zaten bir dosya var:\n\n{target}\n\nİndirilen modloader ile değiştirilsin mi? Devam etmeden önce Minecraft'ı kapat."),
                confirmation: true) != DialogResult.Yes) return;

            // No HTTP request or file write occurs before both applicable approvals.
            stagedFile = Path.Combine(folder, ".bedrockloader-" + Guid.NewGuid().ToString("N") + ".tmp");
            UpdateStatus(T("Downloading modloader…", "Modloader indiriliyor…"));
            using var response = await Http.GetAsync(ModloaderDownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaxModloaderBytes)
                throw new InvalidDataException(T("The modloader download exceeds 64 MB.", "Modloader indirmesi 64 MB sınırını aşıyor."));

            using (var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5)))
            {
                await using var input = await response.Content.ReadAsStreamAsync(timeout.Token);
                await using var output = new FileStream(stagedFile, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                var buffer = new byte[81920];
                long total = 0;
                var progressClock = Stopwatch.StartNew();
                int count;
                while ((count = await input.ReadAsync(buffer, timeout.Token)) != 0)
                {
                    total += count;
                    if (total > MaxModloaderBytes)
                        throw new InvalidDataException(T("The modloader download exceeds 64 MB.", "Modloader indirmesi 64 MB sınırını aşıyor."));
                    await output.WriteAsync(buffer.AsMemory(0, count), timeout.Token);
                    if (progressClock.ElapsedMilliseconds >= 150)
                    {
                        UpdateStatus(T($"Downloading modloader… {total / 1024:N0} KB", $"Modloader indiriliyor… {total / 1024:N0} KB"));
                        progressClock.Restart();
                    }
                }
                if (response.Content.Headers.ContentLength is long expected && total != expected)
                    throw new InvalidDataException(T("The download is incomplete. Try again.", "İndirme tamamlanamadı. Yeniden dene."));
            }

            ValidateModloaderFile(stagedFile);
            // Staging in the same directory allows the completed file to be moved
            // into place without exposing a partial DLL. A newly appeared file is
            // never overwritten if the user didn't approve replacement.
            File.Move(stagedFile, target, overwrite: replaceExisting);
            stagedFile = null;
            if (!string.Equals(settings.ContentPath, folder, StringComparison.OrdinalIgnoreCase))
            {
                settings.ContentPath = folder;
                SaveSettings();
            }
            LogAction($"Installed modloader from {ModloaderDownloadUrl} to {target}");
            UpdateStatus(T("Modloader installed.", "Modloader kuruldu."));
            ShowThemedPopup(T("Installation complete", "Kurulum tamamlandı"), T($"Modloader installed successfully.\n\n{target}", $"Modloader başarıyla kuruldu.\n\n{target}"));
        }
        catch (UnauthorizedAccessException)
        {
            UpdateStatus(T("Modloader installation failed.", "Modloader kurulamadı."));
            ShowThemedPopup(T("Folder access denied", "Klasöre erişim reddedildi"), T("Windows did not allow writing to the Content folder. Check the folder's write permissions and try again.",
                "Windows, Content klasörüne yazmaya izin vermedi. Klasörün yazma izinlerini kontrol edip tekrar dene."));
        }
        catch (Exception ex)
        {
            LogAction($"Modloader installation failed: {ex.Message}");
            UpdateStatus(T("Modloader installation failed.", "Modloader kurulamadı."));
            ShowThemedPopup(T("Modloader installation failed", "Modloader kurulamadı"), ex.Message);
        }
        finally
        {
            if (stagedFile != null)
            {
                try { File.Delete(stagedFile); }
                catch (Exception ex) { LogAction($"Could not remove incomplete modloader download: {ex.Message}"); }
            }
            installingModloader = false;
            if (installModloaderButton is { IsDisposed: false }) installModloaderButton.Enabled = true;
        }
    }

    static void ValidateModloaderFile(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new PEReader(stream);
        if (reader.PEHeaders.PEHeader == null ||
            (reader.PEHeaders.CoffHeader.Characteristics & Characteristics.Dll) == 0)
            throw new InvalidDataException("The downloaded file is not a Windows DLL / İndirilen dosya bir Windows DLL dosyası değil.");
    }
}
