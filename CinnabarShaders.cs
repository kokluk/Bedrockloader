using System.IO.Compression;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    static string CinnabarDataRoot()
    {
        var configured = Environment.GetEnvironmentVariable("CINNABAR_USER_ROOT");
        if (configured != null && Path.IsPathFullyQualified(configured))
            return Path.Combine(configured, "data");
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cinnabar");
    }

    static IEnumerable<string> ListCinnabarShaders(string folder) => Directory.Exists(folder)
        ? Directory.EnumerateFileSystemEntries(folder).Where(path =>
            Directory.Exists(path) ? Directory.Exists(Path.Combine(path, "shaders")) :
            Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        : [];

    void BuildCinnabarShaders()
    {
        AddPageHeading(T("Shaders", "Shaderlar"),
            T("Install compatible Java shader packs and select them in Cinnabar.", "Uyumlu Java shader paketlerini kur ve Cinnabar içinden seç."));
        AddCurseForgeHeadingButton("CurseShaders");
        var layout = new BufferedActionLayout { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Tag = "page" };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        var card = new RoundedPanel { Dock = DockStyle.Fill, CornerRadius = 22, Padding = new Padding(14) };
        var title = WrappedText(T("Installed shader packs", "Kurulu shader paketleri"), 11, true);
        title.Dock = DockStyle.Top;
        title.Height = 32;
        var entries = new SpacedCheckedListBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, CheckOnClick = true, Tag = "card" };
        card.Controls.Add(entries);
        card.Controls.Add(title);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Tag = "page" };
        var folder = Path.Combine(CinnabarDataRoot(), "shaderpacks");
        actions.Controls.Add(MakeButton(T("Open shader folder", "Shader klasörünü aç"), () =>
        {
            if (Directory.Exists(folder)) OpenUrl(folder);
        }, 170));
        var delete = MakeButton(T("Delete checked", "Seçilenleri sil"), () => { }, 160, icon: "trash");
        var reveal = new AnimatedActionReveal(delete, 38, 8, height => layout.RowStyles[2].Height = height)
            { Dock = DockStyle.Fill, BackColor = Surface, Tag = "page" };
        layout.Controls.Add(reveal, 0, 2);
        delete.Click += (_, _) => Delete();
        actions.Controls.Add(MakeButton(T("Refresh", "Yenile"), Refresh));
        entries.ItemCheck += (_, e) =>
        {
            int checkedCount = entries.CheckedItems.Count + (e.NewValue == CheckState.Checked ? 1 : -1);
            reveal.SetShown(checkedCount > 0);
        };
        layout.Controls.Add(card, 0, 0);
        layout.Controls.Add(actions, 0, 1);
        contentHost.Controls.Add(layout);
        Refresh();

        void Refresh()
        {
            entries.Items.Clear();
            reveal.SetShown(false);
            try
            {
                entries.Items.AddRange(ListCinnabarShaders(folder).Select(Path.GetFileName).OfType<string>().ToArray());
                title.Text = entries.Items.Count == 0 ? T("No shader packs installed.", "Kurulu shader paketi yok.")
                    : T($"Installed shader packs ({entries.Items.Count})", $"Kurulu shader paketleri ({entries.Items.Count})");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { title.Text = ex.Message; }
        }

        void Delete()
        {
            var selected = entries.CheckedItems.OfType<string>().ToArray();
            if (selected.Length == 0 || ShowThemedPopup(T("Remove shaders", "Shaderları kaldır"),
                T("Move the selected shader packs to the Recycle Bin?", "Seçili shader paketleri Geri Dönüşüm Kutusu’na taşınsın mı?"),
                true, T("Remove", "Kaldır")) != DialogResult.Yes) return;
            try
            {
                foreach (string name in selected)
                {
                    string path = BedrockSkinSource.SafeChild(folder, name);
                    if (!ListCinnabarShaders(folder).Contains(path, StringComparer.OrdinalIgnoreCase)) continue;
                    if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) continue;
                    if (Directory.Exists(path))
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(path, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                    else
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
            {
                ShowThemedPopup(T("Could not remove shader packs", "Shader paketleri kaldırılamadı"), ex.Message);
            }
            Refresh();
        }
    }

    static void InstallCinnabarShader(string staged, string name)
    {
        using (var archive = ZipFile.OpenRead(staged))
        {
            if (archive.Entries.Count > 20000 ||
                !archive.Entries.Any(entry => entry.FullName.StartsWith("shaders/", StringComparison.Ordinal) && entry.Length > 0) ||
                archive.Entries.Any(entry => entry.FullName.StartsWith('/') || entry.FullName.Contains((char)92) ||
                    entry.FullName.Contains(':') || entry.FullName.Split('/').Contains("..")))
                throw new InvalidDataException("Expected a Java shader ZIP with a root shaders folder.");
        }
        string folder = BedrockSkinSource.SafeChild(CinnabarDataRoot(), "shaderpacks");
        Directory.CreateDirectory(folder);
        File.Move(staged, BedrockSkinSource.SafeChild(folder, name));
    }
}
