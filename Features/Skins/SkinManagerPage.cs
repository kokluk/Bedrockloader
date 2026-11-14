using System.Diagnostics;
using System.Security.Cryptography;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    Control? skinView;
    void DisposeSkinPage()
    {
        skinView?.Dispose();
        skinView = null;
    }

    async void BuildSkinPage()
    {
        var library = new SkinLibrary(Path.Combine(appDataRoot, "Skins", "Library"));
        var entries = library.Read();
        IReadOnlyList<SkinEntry> detected = [];
        var view = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Tag = "page" };
        view.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 47));
        view.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 53));
        view.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        view.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        view.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        skinView = view;
        var preview = new SkinPreview { Dock = DockStyle.Fill, BackColor = SurfaceAlt, ForeColor = Accent };
        var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Tag = "page" };
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var search = MakeTextBox();
        search.Dock = DockStyle.Fill;
        search.BorderStyle = BorderStyle.None;
        var searchCard = new RoundedPanel { Dock = DockStyle.Fill, CornerRadius = 15, Padding = new Padding(36, 10, 12, 0), Margin = new Padding(0, 0, 0, 8), Tag = "card" };
        searchCard.Controls.Add(search);
        searchCard.Paint += (_, e) => SoftIcons.Draw(e.Graphics, "search", new RectangleF(11, 9, 20, 20), Muted);
        search.PlaceholderText = T("Search skins", "Skin ara");
        var skinHashes = new System.Runtime.CompilerServices.ConditionalWeakTable<byte[], string>();
        var grid = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Tag = "page" };
        var details = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
        details.Dock = DockStyle.Top; details.Height = 34; details.TextAlign = ContentAlignment.MiddleCenter;
        var stage = new RoundedPanel { Dock = DockStyle.Fill, CornerRadius = 20, Padding = new Padding(8), Tag = "card" };
        stage.Controls.Add(preview);
        var layer = MakeButton(T("Layers: On", "Katman: Açık"), () => { }, 164, "layers");
        var motion = MakeButton(T("Motion · Idle", "Hareket · Duruş"), () => { });
        motion.Dock = DockStyle.Fill; motion.Tag = "primary";
        var motionBar = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 38, ColumnCount = 2, RowCount = 1, Tag = "card" };
        motionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        motionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 43));
        motion.Margin = new Padding(0, 0, 6, 0);
        motionBar.Controls.Add(motion, 0, 0);
        stage.Controls.Add(motionBar);
        stage.Controls.Add(details);
        var trash = new SymbolButton { Symbol = "trash", Size = new Size(36, 36), Anchor = AnchorStyles.None, Margin = Padding.Empty, AccessibleName = T("Remove skin", "Skini kaldır") };
        motionBar.Controls.Add(trash, 1, 0);
        motion.Click += (_, _) => motion.Text = preview.NextMotion() switch
        { 1 => T("Motion · Walk", "Hareket · Yürüme"), 2 => T("Motion · Run", "Hareket · Koşma"), _ => T("Motion · Idle", "Hareket · Duruş") };
        layer.Click += (_, _) => layer.Text = preview.ToggleLayers() ? T("Layers: On", "Katman: Açık") : T("Layers: Off", "Katman: Kapalı");
        right.Controls.Add(searchCard, 0, 0);
        right.Controls.Add(grid, 0, 1);
        view.Controls.Add(stage, 0, 1);
        view.SetRowSpan(stage, 2);
        view.Controls.Add(right, 1, 1);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, Tag = "page" };
        view.Controls.Add(buttons, 0, 0);
        view.SetColumnSpan(buttons, 2);
        contentHost.Controls.Add(view);
        AddPageHeading(T("Skin Manager", "Skin Yöneticisi"), T("Preview and manage Minecraft skins.", "Minecraft skinlerini önizle ve yönet."));

        AddCurseForgeHeadingButton("CurseSkins");
        string? folder = null;
        bool favoritesFolder = false;
        SkinEntry? selection = null;
        AnimatedRoundButton? favoriteButton = null;
        AnimatedRoundButton? exportButton = null, editButton = null, applyButton = null;
        SkinEntry? Selected() => selection;
        bool Current() => !view.IsDisposed && skinView == view;
        void Status(string en, string tr) => UpdateStatus(T(en, tr));

        SkinEntry[] VisibleSkins() => entries.Concat(detected)
            .Where(skin => (favoritesFolder ? skin.Favorite : folder == null ? skin.Pack == null || search.TextLength > 0 : skin.Pack == folder)
                && skin.Name.Contains(search.Text.Trim(), StringComparison.OrdinalIgnoreCase))
            .DistinctBy(skin => (skin.Name.ToUpperInvariant(), skin.Slim, skinHashes.GetValue(skin.Png, static png => Convert.ToHexString(SHA256.HashData(png)))))
            .ToArray();

        void SendEntries(Guid? selected = null)
        {
            selected ??= Selected()?.Id;
            grid.SuspendLayout();
            while (grid.Controls.Count > 0) grid.Controls[0].Dispose();
            if (folder != null || favoritesFolder) AddFolder(T("‹ Folders", "‹ Klasörler"), null);
            else if (search.TextLength == 0)
            {
                if (entries.Any(s => s.Favorite)) AddFolder(T("Favorites", "Favoriler"), null, true);
                foreach (string pack in entries.Concat(detected).Select(s => s.Pack).OfType<string>().Distinct())
                    AddFolder(pack, pack);
            }
            var visible = VisibleSkins();
            foreach (var skin in visible)
            {
                var tile = new NativeSkinTile(skin.Name, skin) { AccessibleName = skin.Name };
                tile.Click += (_, _) => SelectSkin(skin);
                grid.Controls.Add(tile);
            }
            if (grid.Controls.Count == 0)
                grid.Controls.Add(new Label { AutoSize = true, Text = T("No skins found.", "Skin bulunamadı.") });
            grid.ResumeLayout();
            SelectSkin(visible.FirstOrDefault(skin => skin.Id == selected) ?? visible.FirstOrDefault());

            void AddFolder(string title, string? pack, bool favorites = false)
            {
                var tile = new NativeSkinTile(title, null, favorites) { AccessibleName = title };
                tile.Click += (_, _) => { folder = pack; favoritesFolder = favorites; search.Clear(); SendEntries(); };
                grid.Controls.Add(tile);
            }
        }

        void SelectSkin(SkinEntry? skin)
        {
            selection = skin;
            if (exportButton != null) exportButton.Enabled = skin is not null;
            if (editButton != null) editButton.Enabled = skin is not null;
            if (applyButton != null) applyButton.Enabled = skin is not null;
            if (favoriteButton != null)
            {
                favoriteButton.Visible = entries.Count > 0 || detected.Count > 0;
                favoriteButton.Enabled = skin is not null && entries.Contains(skin);
            }
            layer.Enabled = motionBar.Visible = details.Visible = skin is not null;
            trash.Enabled = skin is not null && entries.Contains(skin);
            preview.SetSkin(skin);
            details.Text = skin is null ? "" : skin.Name + "   •   " + (skin.Slim ? "Alex" : "Steve") + "   •   PNG";
            foreach (var tile in grid.Controls.OfType<NativeSkinTile>())
            {
                tile.Tag = skin is not null && tile.SkinId == skin.Id ? "primary" : "card-button";
                ApplyButtonPalette(tile);
            }
        }
        search.TextChanged += (_, _) => SendEntries();

        AnimatedRoundButton Add(string title, Action action, int width = 110)
        {
            var button = MakeButton(title, () =>
            {
                try { action(); }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    ShowError(ex, T("Skin action failed", "Skin işlemi başarısız"));
                }
            }, width);
            buttons.Controls.Add(button);
            return button;
        }

        var applyBar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Tag = "page" };
        var apply = MakeButton(T("Apply skin", "Skin uygula"), () =>
        {
            if (Selected() is not { } skin) return;
            try { ApplySkin(skin); }
            catch (Exception ex) { ShowError(ex, T("Skin action failed", "Skin işlemi başarısız")); }
        }, 144);
        apply.Tag = "primary";
        applyButton = apply;
        applyBar.Controls.Add(apply);
        view.Controls.Add(applyBar, 1, 2);
        exportButton = Add(T("Export", "Dışa aktar"), () =>
        {
            var skin = Selected();
            if (folder == null && skin == null) return;
            using var save = new SaveFileDialog
            {
                Filter = folder == null ? "PNG|*.png" : "Minecraft skin pack|*.mcpack",
                FileName = SanitizeName(folder ?? skin!.Name) + (folder == null ? ".png" : ".mcpack")
            };
            if (save.ShowDialog(this) != DialogResult.OK) return;
            if (folder != null) SkinLibrary.WritePack(save.FileName, VisibleSkins(), folder);
            else File.WriteAllBytes(save.FileName, SkinLibrary.Normalize(skin!.Png));
        });
        favoriteButton = Add(T("Favorite", "Favori"), () =>
        {
            if (Selected() is not { } skin || !entries.Contains(skin)) return;
            library.Save(skin with { Favorite = !skin.Favorite });
            entries = library.Read();
            SendEntries(skin.Id);
        });
        editButton = Add(T("Edit", "Düzenle"), () =>
        {
            if (Selected() is not { } skin) return;
            bool local = !entries.Contains(skin);
            if (local && entries.Count >= SkinLibrary.Capacity) throw new InvalidDataException("Skin library is full.");
            var edited = EditNativeSkin(skin);
            if (edited == null) return;
            if (local) edited = edited with { Id = Guid.NewGuid(), Created = DateTime.UtcNow };
            else Backup(skin, "Edits");
            library.Save(edited);
            entries = library.Read();
            SendEntries(edited.Id);
        }, 130);
        buttons.Controls.SetChildIndex(favoriteButton, 2);
        buttons.Controls.Add(layer);
        buttons.Controls.SetChildIndex(layer, buttons.Controls.GetChildIndex(favoriteButton) + 1);
        trash.Click += (_, _) =>
        {
            if (Selected() is not { } skin || !entries.Contains(skin)) return;
            if (ShowThemedPopup(T("Remove skin", "Skini kaldır"), skin.Name, true, T("Remove", "Kaldır")) != DialogResult.Yes) return;
            try
            {
                Backup(skin, "Removed");
                library.Remove(skin.Id);
                entries = library.Read();
                SendEntries();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ShowError(ex, T("Skin action failed", "Skin işlemi başarısız"));
            }
        };

        SendEntries();
        try
        {
            string label = T("Local Minecraft skin", "Yerel Minecraft skini");
            var found = await Task.Run(() => BedrockSkinSource.Discover(label));
            if (Current()) { detected = found.Candidates; SendEntries(); }
        }
        catch (Exception ex) { if (Current()) ShowError(ex, T("Skin scan failed", "Skin taraması başarısız")); }

        string Backup(SkinEntry skin, string folder)
        {
            string path = Path.Combine(appDataRoot, "Skins", "Backups", folder, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            File.WriteAllBytes(Path.Combine(path, "skin.png"), SkinLibrary.Normalize(skin.Png));
            return path;
        }

        void ApplySkin(SkinEntry skin)
        {
            if (!BedrockSkinSource.IsGameClosed())
            {
                Status("Close Minecraft before importing a skin.", "Skin aktarmadan önce Minecraft’ı kapat.");
                return;
            }
            if (!MinecraftInstallation.IsInstalled(settings.ContentPath))
            {
                Status("Minecraft for Windows could not be detected. You can export the skin.", "Minecraft for Windows bulunamadı. Skini dışa aktarabilirsin.");
                return;
            }
            var answer = ShowThemedPopup(T("Import into Minecraft", "Minecraft’a aktar"),
                T("Minecraft will open a skin pack for import. After Minecraft confirms the import, equip the skin in Dressing Room → Classic Skins.",
                  "Minecraft skin paketini içe aktarmak için açılacak. Oyun içe aktarmayı doğruladıktan sonra Giyinme Odası → Klasik Skinler bölümünden skini seç."), true);
            if (answer != DialogResult.Yes) return;
            if (!BedrockSkinSource.IsGameClosed())
            {
                Status("Close Minecraft before importing a skin.", "Skin aktarmadan önce Minecraft’ı kapat.");
                return;
            }
            string folder = Backup(skin, "MinecraftImport");
            string pack = Path.Combine(folder, "skin.mcpack");
            SkinLibrary.WritePack(pack, [skin], skin.Name);
            if (!BedrockSkinSource.IsGameClosed())
            {
                Status("Minecraft opened while preparing the pack. Import was canceled.", "Paket hazırlanırken Minecraft açıldı. Aktarma iptal edildi.");
                return;
            }
            using var process = Process.Start(new ProcessStartInfo(pack) { UseShellExecute = true });
            Status("Import request opened. Check the result in Minecraft.", "İçe aktarma isteği açıldı. Sonucu Minecraft’ta kontrol et.");
        }

    }

    sealed class NativeSkinTile : AnimatedRoundButton
    {
        readonly string title;
        readonly Bitmap? thumbnail;
        readonly bool favorite, favoritesFolder;
        public Guid? SkinId { get; }
        public NativeSkinTile(string title, SkinEntry? skin, bool favoritesFolder = false)
        {
            this.title = title;
            SkinId = skin?.Id;
            favorite = skin?.Favorite == true;
            this.favoritesFolder = favoritesFolder;
            Size = new Size(98, 84);
            CornerRadius = 15;
            if (skin is null) return;
            using var stream = new MemoryStream(skin.Png);
            using var image = Image.FromStream(stream);
            using var texture = new Bitmap(image);
            int scale = texture.Width / 64;
            thumbnail = new Bitmap(8, 8);
            using var g = Graphics.FromImage(thumbnail);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            var target = new Rectangle(0, 0, 8, 8);
            g.DrawImage(texture, target, new Rectangle(8*scale, 8*scale, 8*scale, 8*scale), GraphicsUnit.Pixel);
            g.DrawImage(texture, target, new Rectangle(40*scale, 8*scale, 8*scale, 8*scale), GraphicsUnit.Pixel);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var icon = new Rectangle((Width-40)/2, 9, 40, 40);
            var state = e.Graphics.Save();
            ApplyIconMotion(e.Graphics, icon);
            if (thumbnail is not null)
            {
                e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                e.Graphics.DrawImage(thumbnail, icon);
            }
            else
                SoftIcons.Draw(e.Graphics, favoritesFolder ? "skin-star" : "skin-pack", icon, ForeColor, true);
            e.Graphics.Restore(state);
            if (favorite) SoftIcons.Draw(e.Graphics, "skin-star", new RectangleF(Width - 23, 6, 16, 16), ForeColor, true);
            TextRenderer.DrawText(e.Graphics, title, Font,
                new Rectangle(5, 51, Width-10, Height-55), ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) thumbnail?.Dispose();
            base.Dispose(disposing);
        }
    }
}
