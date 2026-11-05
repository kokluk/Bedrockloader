namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    void BuildClientManager()
    {
        AddPageHeading("OderSoClient", T("Manage local OderSo DLL files.", "Yerel OderSo DLL dosyalarını yönet."));
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Tag = "page" };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        var guidance = WrappedText(T("Use the official OderSo launcher to start the client. Local DLLs are listed for inspection.",
            "İstemciyi başlatmak için resmî OderSo Launcher’ı kullan. Yerel DLL dosyalarını burada inceleyebilirsin."));
        guidance.Dock = DockStyle.Fill;
        var list = new ListBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, Tag = "page" };
        actions.Controls.Add(MakeButton(T("OderSo downloads", "OderSo indirmeleri"), () => OpenUrl("https://discord.com/channels/1158057414583652372/1158059899419369493"), 165));
        actions.Controls.Add(MakeButton(T("Add DLL", "DLL ekle"), () =>
        {
            using var picker = new OpenFileDialog { Filter = "Windows DLL|*.dll", Multiselect = true };
            if (picker.ShowDialog(this) == DialogResult.OK) Import(picker.FileNames);
        }));
        actions.Controls.Add(MakeButton(T("Open mods folder", "Mods klasörünü aç"), OpenMods, 155));
        layout.Controls.Add(guidance, 0, 0);
        layout.Controls.Add(list, 0, 1);
        layout.Controls.Add(actions, 0, 2);
        contentHost.Controls.Add(layout);
        list.AllowDrop = true;
        list.DragEnter += (_, e) => e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
        list.DragDrop += (_, e) => { if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths) Import(paths); };
        list.DoubleClick += (_, _) =>
        {
            if (list.SelectedItem is not string file) return;
            try
            {
                var dll = ClientBinary.Read(BedrockSkinSource.SafeChild(modsFolder, file));
                ShowThemedPopup(dll.Name, $"{dll.Architecture} · {dll.Length:N0} B\n{dll.Version}\nSHA-256\n{dll.Hash}\n\n" +
                    T("Minecraft compatibility is unverified.", "Minecraft uyumluluğu doğrulanmadı."));
            }
            catch (Exception ex) { ShowError(ex, T("DLL could not be read", "DLL okunamadı")); }
        };
        Refresh();

        void Import(IEnumerable<string> files)
        {
            try { CopyMods(files.Select(file => ClientBinary.Read(file).Path).ToArray()); Refresh(); }
            catch (Exception ex) { ShowError(ex, T("DLL could not be imported", "DLL içe aktarılamadı")); }
        }

        void Refresh()
        {
            list.Items.Clear();
            list.Items.AddRange(Directory.GetFiles(modsFolder, "*.dll").Select(Path.GetFileName).OfType<string>().Order().ToArray());
        }
    }
}
