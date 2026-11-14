using DiscordRPC;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    const string DefaultDiscordApplicationId = "1567637573680238662";
    readonly CancellationTokenSource presenceLifetime = new();
    DiscordOptions presenceOptions = new();

    void StartDiscordPresence()
    {
        RefreshDiscordOptions();
        _ = RunDiscordPresence(presenceLifetime.Token);
    }

    void RefreshDiscordOptions()
    {
        var options = settings.Discord ?? new DiscordOptions();
        Volatile.Write(ref presenceOptions, options with
        {
            ContentPath = settings.ContentPath,
            LocalName = NormalizeLocalName(settings.LocalName),
            English = settings.Language == "EN",
            OnHomePage = currentPage == "Home",
            ApplicationId = string.IsNullOrWhiteSpace(options.ApplicationId) ? DefaultDiscordApplicationId : options.ApplicationId
        });
    }

    Task RunDiscordPresence(CancellationToken cancel) => Task.Run(async () =>
    {
        DiscordRpcClient? client = null;
        string? applicationId = null;
        PresenceSnapshot? published = null;
        int readyGeneration = 0, sentGeneration = 0;
        try
        {
            while (!cancel.IsCancellationRequested)
            {
                try
                {
                    var options = Volatile.Read(ref presenceOptions);
                    bool enabled = options.Enabled && ValidDiscordId(options.ApplicationId);
                    var game = enabled ? MinecraftInstallation.FindRunningGame(options.ContentPath) : null;
                    bool showPresence = enabled && (game is not null || options.OnHomePage);
                    if (!showPresence || applicationId != options.ApplicationId)
                    {
                        client?.Dispose();
                        client = null;
                        published = null;
                        applicationId = options.ApplicationId;
                    }
                    if (showPresence)
                    {
                        if (client == null)
                        {
                            Volatile.Write(ref readyGeneration, 0);
                            sentGeneration = 0;
                            client = new DiscordRpcClient(options.ApplicationId);
                            client.OnError += (_, _) => LogAction("Discord rejected a presence request.", "Discord etkinlik isteğini reddetti.");
                            client.OnReady += (_, _) => Interlocked.Increment(ref readyGeneration);
                            client.OnPresenceUpdate += (_, _) => LogAction("Discord acknowledged a presence update.", "Discord etkinlik güncellemesini onayladı.");
                        }
                        if (!client.IsInitialized) client.Initialize();

                        string largeImage = string.IsNullOrWhiteSpace(options.LargeImage) ? "bedrockloader" : options.LargeImage;
                        var snapshot = game is null
                            ? new PresenceSnapshot(options.English ? "Main Menu" : "Ana Menü",
                                options.English ? "Idle 🌙" : "Boşta 🌙", null, largeImage, options.SmallImage)
                            : new PresenceSnapshot(options.ShowVersion && game.Version is { } version
                                    ? $"Minecraft {version}" : "Minecraft",
                                string.IsNullOrEmpty(options.LocalName) ? null : options.LocalName,
                                options.ShowTime ? game.StartedUtc : null, largeImage, options.SmallImage);
                        int generation = Volatile.Read(ref readyGeneration);
                        if (client.IsInitialized && generation > 0 && (snapshot != published || generation != sentGeneration))
                        {
                            client.SetPresence(new RichPresence
                            {
                                Details = snapshot.Details,
                                State = snapshot.State,
                                Timestamps = snapshot.StartedUtc is { } started ? new Timestamps { Start = started } : null,
                                Assets = new Assets { LargeImageKey = snapshot.LargeImage, SmallImageKey = snapshot.SmallImage }
                            });
                            published = snapshot;
                            sentGeneration = generation;
                        }
                    }
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    client?.Dispose();
                    client = null;
                    published = null;
                }
                await Task.Delay(TimeSpan.FromSeconds(3), cancel);
            }
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested) { }
        finally { client?.Dispose(); }
    });

    static bool ValidDiscordId(string value) => ulong.TryParse(value, System.Globalization.NumberStyles.None,
        System.Globalization.CultureInfo.InvariantCulture, out ulong id) && id > 0;

    sealed record PresenceSnapshot(string Details, string? State, DateTime? StartedUtc, string LargeImage, string SmallImage);

    void ConfigureDiscordPresence()
    {
        var options = settings.Discord ?? new DiscordOptions();
        using var dialog = new RoundedPopupForm(Accent)
        {
            Text = "Discord Rich Presence", StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.None,
            ClientSize = new Size(570, 252), AutoScaleMode = AutoScaleMode.Dpi, BackColor = Surface, ForeColor = TextColor,
            ShowInTaskbar = false, MinimizeBox = false, MaximizeBox = false, Font = Font
        };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5, Padding = new Padding(20), AutoScroll = true };
        int row = 0;
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
        dialog.Controls.Add(layout);
        using var tips = new ToolTip { InitialDelay = 350 };
        Span(new Label { Text = "Discord Rich Presence", Font = new Font(Font.FontFamily, 16, FontStyle.Bold), Dock = DockStyle.Fill }, 38);
        var enabled = Check(T("Enable Rich Presence", "Rich Presence’ı etkinleştir"), options.Enabled);
        var version = Check(T("Share Minecraft version", "Minecraft sürümünü paylaş"), options.ShowVersion);
        var time = Check(T("Share elapsed time", "Oyun süresini paylaş"), options.ShowTime);
        var cancel = MakeButton(T("Back", "Geri"), () => dialog.DialogResult = DialogResult.Cancel);
        var save = MakeButton(T("Save", "Kaydet"), () =>
        {
            settings.Discord = options with { Enabled = enabled.Checked, ShowVersion = version.Checked, ShowTime = time.Checked };
            SaveSettings();
            RefreshDiscordOptions();
            dialog.DialogResult = DialogResult.OK;
        });
        save.Tag = "primary";
        ApplyButtonPalette(save);
        ApplyButtonPalette(cancel);
        tips.SetToolTip(cancel, T("Click to discard changes", "Değişiklikleri iptal etmek için tıkla"));
        tips.SetToolTip(save, T("Click to save changes", "Değişiklikleri kaydetmek için tıkla"));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        cancel.Anchor = save.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        cancel.Margin = new Padding(0, 0, 5, 0);
        save.Margin = new Padding(5, 0, 0, 0);
        layout.Controls.Add(cancel, 0, row); layout.Controls.Add(save, 1, row);
        dialog.CancelButton = cancel;
        dialog.AcceptButton = save;
        var close = MakeShortcutClose(dialog);
        close.Dock = DockStyle.None;
        close.Size = new Size(34, 34);
        close.Location = new Point(dialog.ClientSize.Width - 48, 14);
        close.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        dialog.Controls.Add(close);
        close.BringToFront();
        dialog.ShowDialog(this);

        CheckBox Check(string label, bool value)
        {
            var box = new PopupToggle(Accent, SurfaceAlt) { Text = label, Checked = value, Cursor = Cursors.Hand };
            tips.SetToolTip(box, T("Click to toggle", "Açmak veya kapatmak için tıkla"));
            Span(box, 34);
            return box;
        }
        void Span(Control control, int height)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            control.Dock = DockStyle.Fill;
            layout.Controls.Add(control, 0, row++);
            layout.SetColumnSpan(control, 2);
        }
    }

    sealed record DiscordOptions
    {
        public bool Enabled { get; init; } = true;
        public bool ShowVersion { get; init; } = true;
        public bool ShowTime { get; init; } = true;
        public string ApplicationId { get; init; } = DefaultDiscordApplicationId;
        public string LargeImage { get; init; } = "bedrockloader";
        public string SmallImage { get; init; } = "";
        [System.Text.Json.Serialization.JsonIgnore]
        public string ContentPath { get; init; } = "";
        [System.Text.Json.Serialization.JsonIgnore]
        public string LocalName { get; init; } = "";
        [System.Text.Json.Serialization.JsonIgnore]
        public bool English { get; init; }
        [System.Text.Json.Serialization.JsonIgnore]
        public bool OnHomePage { get; init; }
    }
}
