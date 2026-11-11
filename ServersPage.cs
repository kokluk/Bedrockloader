using System.Diagnostics;
using System.Text;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    Control? serversView;

    void BuildServersPage()
    {
        AddPageHeading(T("Servers", "Sunucular"), cinnabarMode
            ? T("Your saved Bedrock servers for Cinnabar.", "Cinnabar için kayıtlı Bedrock sunucuların.")
            : T("Manage your Bedrock servers and Minecraft’s external servers.",
            "Bedrock sunucularını ve Minecraft’ın üçüncü taraf sunucu listesini yönet."));
        var page = new Panel { Dock = DockStyle.Fill, Tag = "page" };
        serversView = page;
        var grid = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, Tag = "page" };
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 47, Tag = "page" };
        page.Controls.Add(grid);
        page.Controls.Add(toolbar);
        contentHost.Controls.Add(page);
        var lifetime = new CancellationTokenSource();
        CancellationTokenSource? refresh = null;
        page.Disposed += (_, _) => { lifetime.Cancel(); lifetime.Dispose(); };
        bool minecraft = false;
        var minecraftTab = MakeButton(T("Minecraft servers", "Minecraft sunucuları"), () => { }, 165);
        minecraftTab.Click += (_, _) => SelectSource(!minecraft);
        if (!cinnabarMode) toolbar.Controls.Add(minecraftTab);
        else page.Disposed += (_, _) => minecraftTab.Dispose();
        toolbar.Controls.Add(MakeButton(T("Add server", "Sunucu ekle"), () => EditLocal(null), 128));
        toolbar.Controls.Add(MakeButton(T("Refresh", "Yenile"), () => _ = RefreshAsync(), 94));
        grid.Resize += (_, _) => ResizeCards();
        SelectSource(false);

        void SelectSource(bool value)
        {
            minecraft = value;
            minecraftTab.Tag = value ? "primary" : null;
            minecraftTab.Text = value ? T("Back to saved servers", "Kayıtlı sunuculara dön") : T("Minecraft servers", "Minecraft sunucuları");
            ApplyButtonPalette(minecraftTab);
            _ = RefreshAsync();
        }

        async Task RefreshAsync()
        {
            refresh?.Cancel();
            using var request = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            refresh = request;
            var cancel = request.Token;
            var probes = new List<(SavedServer Server, Label Status)>();
            try
            {
                while (grid.Controls.Count != 0) grid.Controls[0].Dispose();
                if (minecraft)
                {
                    var lists = await Task.Run(ReadMinecraftServerLists, cancel);
                    if (cancel.IsCancellationRequested) return;
                    foreach (var list in lists)
                    {
                        if (list.Data?.Entries.Count == 0 && list.UnreadableLines == 0) continue;
                        var caption = MakeLabel(list.Label, 10, true);
                        grid.Controls.Add(caption);
                        if (list.UnreadableLines > 0)
                            grid.Controls.Add(MakeLabel(T("Some records could not be read. Editing is unavailable for this list.",
                                "Bazı kayıtlar okunamadı. Bu liste düzenlenemiyor.")));
                        if (list.Data is not { } data) continue;
                        foreach (var entry in data.Entries)
                            AddCard(entry.Server, () => EditMinecraft(data, entry), false, data.UnreadableLines == 0);
                    }
                }
                else
                    foreach (var server in settings.Servers.OrderByDescending(item => item.Favorite).ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
                        AddCard(server, () => EditLocal(server), true, true);

                if (grid.Controls.Count == 0)
                    grid.Controls.Add(MakeLabel(minecraft ? T("No external Minecraft servers found.", "Minecraft’ta üçüncü taraf sunucu bulunamadı.")
                        : T("No saved servers yet.", "Henüz kayıtlı sunucu yok.")));
                ResizeCards();
                ApplyTheme();
                using var slots = new SemaphoreSlim(4);
                await Task.WhenAll(probes.Select(async probe =>
                {
                    await slots.WaitAsync(cancel);
                    try
                    {
                        long? latency = await BedrockServers.PingAsync(probe.Server, cancel);
                        if (!cancel.IsCancellationRequested && !probe.Status.IsDisposed)
                        {
                            probe.Status.Text = latency is { } ms ? $"{ms} ms" : T("No response", "Yanıt yok");
                            if (latency != null)
                            {
                                probe.Status.Parent!.Tag = "server-latency-ready";
                                ApplyLatencyPalette(probe.Status.Parent, probe.Status);
                            }
                        }
                    }
                    finally { slots.Release(); }
                }));
            }
            catch (OperationCanceledException) when (cancel.IsCancellationRequested) { }
            finally { if (refresh == request) refresh = null; }

            void AddCard(SavedServer server, Action edit, bool canAdd, bool canEdit)
            {
                var card = new RoundedPanel { Height = 66, CornerRadius = 18, Padding = new Padding(14, 10, 8, 10), Tag = "card" };
                var actions = new FlowLayoutPanel { Dock = DockStyle.Right, Width = canAdd ? 302 : 101, FlowDirection = FlowDirection.RightToLeft,
                    WrapContents = false, Tag = "card" };
                var editButton = MakeButton(T("Edit", "Düzenle"), edit, 92);
                editButton.Tag = "card-button";
                editButton.Enabled = canEdit;
                actions.Controls.Add(editButton);
                if (canAdd)
                {
                    var remove = new SymbolButton { Symbol = "trash", Width = 34, Height = editButton.Height,
                        Margin = new Padding(3, 5, 3, 5), AccessibleName = T("Delete server", "Sunucuyu sil") };
                    remove.Click += (_, _) =>
                    {
                        if (ShowThemedPopup(T("Delete server", "Sunucuyu sil"), server.Name, true, T("Delete", "Sil")) != DialogResult.Yes) return;
                        settings.Servers.RemoveAll(item => item.Id == server.Id);
                        SaveSettings();
                        _ = RefreshAsync();
                    };
                    actions.Controls.Add(remove);
                    actions.Controls.SetChildIndex(remove, 0);
                    var action = cinnabarMode
                        ? MakeButton(T("Copy address", "Adresi kopyala"), () => CopyCinnabarAddress(server), 148)
                        : MakeButton(T("Add to Minecraft", "Minecraft’a ekle"), () => AddServerToMinecraft(server), 148);
                    action.Tag = "card-button";
                    actions.Controls.Add(action);
                }
                var title = WrappedText((server.Favorite ? "★  " : "") + server.Name, 11, true);
                title.Height = 23;
                title.AutoEllipsis = true;
                var info = WrappedText(BedrockServers.Endpoint(server), 9);
                info.Dock = DockStyle.Fill;
                info.AutoEllipsis = true;
                var addressMenu = new ContextMenuStrip { ShowImageMargin = false };
                addressMenu.Items.Add(T("Copy address", "Adresi kopyala"), null, (_, _) =>
                    RunClipboardAction(() => WriteClipboardText(BedrockServers.Endpoint(server))));
                info.ContextMenuStrip = addressMenu;
                info.Disposed += (_, _) => addressMenu.Dispose();
                var details = new Panel { Dock = DockStyle.Fill, Tag = "card" };
                if (server.AutoPing)
                {
                    var state = WrappedText(T("Checking…", "Sorgulanıyor…"), 9);
                    state.TextAlign = ContentAlignment.MiddleCenter;
                    var badge = new RoundedPanel { Size = new Size(104, editButton.Height), CornerRadius = editButton.CornerRadius,
                        Margin = editButton.Margin, Tag = "server-latency" };
                    state.Dock = DockStyle.Fill;
                    badge.Controls.Add(state);
                    actions.Width += badge.Width + badge.Margin.Horizontal;
                    actions.Controls.Add(badge);
                    probes.Add((server, state));
                }
                details.Controls.Add(info);
                var identity = new Panel { Dock = DockStyle.Fill, Tag = "card" };
                identity.Controls.Add(details);
                identity.Controls.Add(title);
                card.Controls.Add(identity);
                card.Controls.Add(actions);
                grid.Controls.Add(card);
            }
        }

        void ResizeCards()
        {
            foreach (var card in grid.Controls.OfType<RoundedPanel>())
                card.Width = Math.Max(1, grid.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 8);
        }

        void EditLocal(SavedServer? original)
        {
            var updated = ShowServerEditor(original, settings.Servers, false);
            if (updated == null) return;
            var previous = settings.Servers;
            settings.Servers = original == null ? [.. previous, updated] :
                previous.Select(server => server.Id == original.Id ? updated : server).ToList();
            try { WriteSettings(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                settings.Servers = previous;
                ShowError(ex, T("Could not save server", "Sunucu kaydedilemedi"));
                return;
            }
            SelectSource(false);
        }

        void EditMinecraft(BedrockServers.GameList list, BedrockServers.GameEntry entry)
        {
            if (!BedrockSkinSource.IsGameClosed())
            {
                ShowThemedPopup(T("Minecraft is open", "Minecraft açık"), T("Close Minecraft before editing its server list.",
                    "Sunucu listesini düzenlemeden önce Minecraft’ı kapat."));
                return;
            }
            var updated = ShowServerEditor(entry.Server, list.Entries.Select(item => item.Server), true);
            if (updated == null) return;
            try { list.Replace(entry, updated); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                ShowThemedPopup(T("Could not save server", "Sunucu kaydedilemedi"),
                    T("Close Minecraft, refresh the list, and try again. Check that the game folder is writable.",
                      "Minecraft’ı kapatıp listeyi yenile ve tekrar dene. Oyun klasörünün yazılabilir olduğunu kontrol et."));
                return;
            }
            _ = RefreshAsync();
        }
    }

    List<(string Label, BedrockServers.GameList? Data, int UnreadableLines)> ReadMinecraftServerLists()
    {
        var result = new List<(string, BedrockServers.GameList?, int)>();
        var roots = WorldRoots().Select(path => Path.GetDirectoryName(path)!).Distinct(StringComparer.OrdinalIgnoreCase);
        int profile = 0;
        foreach (string root in roots)
        {
            try
            {
                string path = BedrockSkinSource.SafeChild(root, "minecraftpe", "external_servers.txt");
                if (!File.Exists(path)) continue;
                var list = new BedrockServers.GameList(path);
                result.Add((T($"Minecraft list {++profile}", $"Minecraft listesi {profile}"), list, list.UnreadableLines));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or DecoderFallbackException)
            {
                result.Add((T($"Minecraft list {++profile}", $"Minecraft listesi {profile}"), null, 1));
            }
        }
        return result;
    }

    void AddServerToMinecraft(SavedServer server)
    {
        if (server.Name.Contains('|'))
        {
            ShowThemedPopup(T("Server name", "Sunucu adı"), T("Remove | from the name before sending it to Minecraft.",
                "Minecraft’a göndermeden önce sunucu adından | karakterini kaldır."));
            return;
        }
        if (!MinecraftInstallation.IsInstalled(settings.ContentPath))
        {
            ShowThemedPopup("Minecraft", T("Install Minecraft for Windows first.", "Önce Minecraft for Windows’u yükle."));
            return;
        }
        try
        {
            string uri = "minecraft://?addExternalServer=" + Uri.EscapeDataString(server.Name) + "|" + BedrockServers.Endpoint(server);
            using var process = Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
            UpdateStatus(T("Check the server addition prompt in Minecraft.", "Sunucu ekleme isteğini Minecraft’ta kontrol et."));
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        { ShowError(ex, T("Could not open Minecraft", "Minecraft açılamadı")); }
    }

    void CopyCinnabarAddress(SavedServer server)
    {
        try
        {
            WriteClipboardText(BedrockServers.Endpoint(server));
            UpdateStatus(T("Address copied. Paste it in Cinnabar’s server screen.",
                "Adres kopyalandı. Cinnabar’ın sunucu ekranına yapıştır."));
        }
        catch (System.Runtime.InteropServices.ExternalException ex)
        { ShowError(ex, T("Could not copy address", "Adres kopyalanamadı")); }
    }

    void ApplyLatencyPalette(Control badge, Label label)
    {
        bool ready = Equals(badge.Tag, "server-latency-ready");
        Color green = lightMode ? Color.FromArgb(24, 105, 62) : Color.FromArgb(121, 226, 155);
        badge.BackColor = MixColors(SurfaceAlt, ready ? green : Accent, .15);
        label.ForeColor = ready ? green : Muted;
    }

    SavedServer? ShowServerEditor(SavedServer? original, IEnumerable<SavedServer> peers, bool minecraft)
    {
        using var popup = new RoundedPopupForm(Accent)
        {
            FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.CenterParent, ShowInTaskbar = false,
            ClientSize = new Size(Math.Min(630, ClientSize.Width - 40), minecraft ? 422 : 478),
            BackColor = Surface, ForeColor = TextColor, AutoScaleMode = AutoScaleMode.Dpi,
            Text = original == null ? T("New server", "Yeni sunucu") : T("Edit server", "Sunucuyu düzenle")
        };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(22) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        popup.Controls.Add(layout);
        var heading = WrappedText(popup.Text, 17, true);
        var subtitle = WrappedText("Minecraft Bedrock Edition  •  UDP", 9);
        subtitle.ForeColor = Muted;
        AddRow(heading, 33);
        AddRow(subtitle, 28);
        var (name, nameError) = Field(T("Server name *", "Sunucu adı *"), original?.Name ?? "", "Survival SMP", 128);
        var (host, hostError) = Field(T("IP or domain *", "IP veya domain *"), original?.Host ?? "", "play.example.net", 255);
        var (port, portError) = Field(T("Port *", "Port *"), (original?.Port ?? BedrockServers.DefaultPort).ToString(), "19132", 12);
        portError.Text = T("Default Bedrock port is 19132.", "Varsayılan Bedrock portu 19132.");
        var favorite = new PopupToggle(Accent, SurfaceAlt) { Text = T("Add to favorites", "Favorilere ekle"), Checked = original?.Favorite ?? false };
        var autoPing = new PopupToggle(Accent, SurfaceAlt) { Text = T("Automatically check status", "Sunucu durumunu otomatik sorgula"), Checked = original?.AutoPing ?? true };
        if (!minecraft)
        {
            AddRow(favorite, 28);
            AddRow(autoPing, 28);
        }
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        SavedServer? saved = null;
        var save = MakeButton(T("Save", "Kaydet"), () =>
        {
            string title = name.Text.Trim();
            string? address = BedrockServers.NormalizeHost(host.Text);
            bool validPort = BedrockServers.TryPort(port.Text, out int number);
            nameError.Text = title.Length is 0 or > 50 || title.Any(char.IsControl)
                ? T("Enter a name of 1–50 characters.", "1–50 karakter uzunluğunda bir ad gir.") :
                minecraft && title.Contains(':') ? T("Minecraft server names here cannot contain a colon.", "Minecraft sunucu adı iki nokta içeremez.") : "";
            hostError.Text = address == null ? T("Enter a valid IPv4, IPv6, or domain.", "Geçerli bir IPv4, IPv6 veya domain gir.") :
                minecraft && address.Contains(':') ? T("Edit IPv6 entries in Minecraft itself.", "IPv6 kayıtlarını Minecraft’ın içinden düzenle.") : "";
            portError.Text = validPort ? "" : T("Enter a port from 1 to 65535.", "1 ile 65535 arasında bir port gir.");
            if (address != null && validPort && hostError.Text.Length == 0 && peers.Any(server =>
                server.Id != original?.Id && BedrockServers.NormalizeHost(server.Host) == address && server.Port == number))
                hostError.Text = T("This address and port are already saved.", "Bu adres ve port zaten kayıtlı.");
            if (nameError.Text.Length + hostError.Text.Length + portError.Text.Length != 0) return;
            saved = new SavedServer(original?.Id ?? Guid.NewGuid(), title, address!, number, favorite.Checked, autoPing.Checked);
            popup.DialogResult = DialogResult.OK;
        });
        save.Tag = "primary";
        ApplyButtonPalette(save);
        var cancel = MakeButton(T("Cancel", "İptal"), () => popup.DialogResult = DialogResult.Cancel);
        actions.Controls.Add(save);
        actions.Controls.Add(cancel);
        AddRow(actions, 48);
        popup.AcceptButton = save;
        popup.CancelButton = cancel;
        popup.Shown += (_, _) => name.Focus();
        return popup.ShowDialog(this) == DialogResult.OK ? saved : null;

        void AddRow(Control control, int height)
        {
            int row = layout.RowCount++;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            control.Dock = DockStyle.Fill;
            control.Margin = Padding.Empty;
            layout.Controls.Add(control, 0, row);
        }

        (TextBox Input, Label Error) Field(string label, string value, string placeholder, int maxLength)
        {
            AddRow(WrappedText(label, 9, true), 20);
            var input = MakeTextBox(value);
            input.PlaceholderText = placeholder;
            input.MaxLength = maxLength;
            input.AccessibleName = label;
            AddRow(WrapPopupInput(input), 36);
            var error = WrappedText("", 9);
            error.ForeColor = lightMode ? Color.DarkRed : Color.LightSalmon;
            AddRow(error, 25);
            input.TextChanged += (_, _) => error.Text = "";
            return (input, error);
        }
    }
}
