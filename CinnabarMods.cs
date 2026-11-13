using Microsoft.VisualBasic.FileIO;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    static bool IsCinnabarMod(string path) => Path.GetExtension(path).Equals(".wasm", StringComparison.OrdinalIgnoreCase)
        && !Path.GetFileName(path).StartsWith('.');

    static IEnumerable<string> ListCinnabarMods(string folder) =>
        Directory.Exists(folder) && !IsReparsePoint(folder)
            ? Directory.EnumerateFiles(folder).Where(path => IsCinnabarMod(path) && !IsReparsePoint(path)).ToArray() : [];

    void BuildCinnabarMods()
    {
        AddPageHeading(T("Cinnabar mods", "Cinnabar modları"),
            T("Manage personal WASM mods. Review permissions and enable them in Cinnabar.", "Kişisel WASM modlarını yönet. İzinleri incelemek ve etkinleştirmek için Cinnabar’ı kullan."));
        string root = CinnabarDataRoot();
        string folder = Path.Combine(root, "mods");
        var page = new BufferedActionLayout { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        var drop = new RoundedPanel { Dock = DockStyle.Fill, Padding = new Padding(12), CornerRadius = 18, AllowDrop = true };
        var add = MakeButton(T("Choose WASM files or drop them here", "WASM dosyası seç veya buraya bırak"), () => { }, icon: "download");
        var list = new SpacedCheckedListBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, CheckOnClick = true, IntegralHeight = false };
        var remove = MakeButton(T("Delete checked", "Seçilenleri sil"), () => { }, 166, "trash");
        var reveal = new AnimatedActionReveal(remove, 38, 8, height => page.RowStyles[3].Height = height) { Dock = DockStyle.Fill };
        add.Click += (_, _) =>
        {
            using var picker = new OpenFileDialog { Filter = "WebAssembly (*.wasm)|*.wasm", Multiselect = true };
            if (picker.ShowDialog(this) == DialogResult.OK) Import(picker.FileNames);
        };
        add.Dock = DockStyle.Fill;
        add.AllowDrop = true;
        foreach (var target in new Control[] { drop, add })
        {
            target.DragEnter += (_, e) => e.Effect = e.Data?.GetData(DataFormats.FileDrop) is string[] paths && paths.Any(IsCinnabarMod)
                ? DragDropEffects.Copy : DragDropEffects.None;
            target.DragDrop += (_, e) => { if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths) Import(paths); };
        }
        drop.Controls.Add(add);
        var tools = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        tools.Controls.Add(MakeButton(T("Refresh", "Yenile"), Refresh));
        tools.Controls.Add(MakeButton(T("Official mod", "Resmî mod"), () => OpenUrl("https://cinnabar.gg/mod/"), 142, "external"));
        list.ItemCheck += (_, e) => reveal.SetShown(list.CheckedItems.Count + (e.NewValue == CheckState.Checked ? 1 : -1) > 0);
        remove.Click += (_, _) =>
        {
            string[] selected = list.CheckedItems.OfType<string>().ToArray();
            if (selected.Length == 0 || !GameClosed() ||
                ShowThemedPopup(T("Remove mods", "Modları kaldır"), T("Move selected mod files to the Recycle Bin?", "Seçili mod dosyaları Geri Dönüşüm Kutusu’na taşınsın mı?"),
                    true, T("Remove", "Kaldır"), destructive: true) != DialogResult.Yes || !GameClosed()) return;
            try
            {
                foreach (var name in selected)
                {
                    string path = BedrockSkinSource.SafeChild(root, "mods", name);
                    if (File.Exists(path) && IsCinnabarMod(path) && !IsReparsePoint(path))
                        FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException or InvalidDataException)
            { ShowThemedPopup(T("Mod removal failed", "Mod kaldırılamadı"), ex.Message); }
            Refresh();
        };
        page.Controls.Add(drop, 0, 0);
        page.Controls.Add(list, 0, 1);
        page.Controls.Add(tools, 0, 2);
        page.Controls.Add(reveal, 0, 3);
        contentHost.Controls.Add(page);
        Refresh();

        void Refresh()
        {
            list.Items.Clear();
            try
            {
                string safeFolder = BedrockSkinSource.SafeChild(root, "mods");
                list.Items.AddRange(ListCinnabarMods(safeFolder).Select(Path.GetFileName).OfType<string>().Order().ToArray());
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            { UpdateStatus(T("Cinnabar mod folder could not be read.", "Cinnabar mod klasörü okunamadı.")); }
            reveal.SetShown(false, animate: false);
        }

        bool GameClosed()
        {
            var running = System.Diagnostics.Process.GetProcessesByName("cinnabar");
            foreach (var process in running) process.Dispose();
            if (running.Length == 0) return true;
            ShowThemedPopup(T("Close Cinnabar", "Cinnabar’ı kapat"), T("Close Cinnabar before changing mod files.", "Mod dosyalarını değiştirmeden önce Cinnabar’ı kapat."));
            return false;
        }

        void Import(string[] sources)
        {
            if (!GameClosed()) return;
            int added = 0;
            var rejected = new List<string>();
            foreach (var source in sources)
            {
                try
                {
                    if (!IsCinnabarMod(source) || IsReparsePoint(source)) throw new InvalidDataException();
                    using var input = File.OpenRead(source);
                    Span<byte> header = new byte[8];
                    input.ReadExactly(header);
                    if (!header.SequenceEqual(new byte[] { 0, 97, 115, 109, 13, 0, 1, 0 }) || input.Length > 64 * 1024 * 1024)
                        throw new InvalidDataException();
                    string target = BedrockSkinSource.SafeChild(root, "mods", Path.GetFileName(source));
                    Directory.CreateDirectory(folder);
                    string temporary = Path.Combine(folder, $".{Guid.NewGuid():N}.importing");
                    try
                    {
                        input.Position = 0;
                        using (var output = File.Open(temporary, FileMode.CreateNew)) input.CopyTo(output);
                        File.Move(temporary, target);
                        added++;
                    }
                    finally { if (File.Exists(temporary)) File.Delete(temporary); }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
                { rejected.Add(Path.GetFileName(source)); }
            }
            Refresh();
            UpdateStatus(T($"{added} Cinnabar mod file(s) added.", $"{added} Cinnabar mod dosyası eklendi."));
            if (rejected.Count > 0)
                ShowThemedPopup(T("Files skipped", "Atlanan dosyalar"), T("Unsupported files or duplicate names:", "Desteklenmeyen dosyalar veya yinelenen adlar:") +
                    "\n\n" + string.Join("\n", rejected));
        }
    }
}
