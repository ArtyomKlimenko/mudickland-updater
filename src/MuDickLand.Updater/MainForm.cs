using System.Diagnostics;

namespace MuDickLand.Updater;

public sealed class MainForm : Form
{
    private readonly AppLogger _logger = new();
    private readonly HttpClient _http = new();
    private readonly StateStore _stateStore;
    private readonly UpdaterConfig _config;
    private readonly UpdaterState _state;

    private readonly TextBox _installDir = new();
    private readonly TextBox _latestUrl = new();
    private readonly CheckBox _telemetryEnabled = new();
    private readonly ProgressBar _progress = new();
    private readonly Label _status = new();
    private readonly RichTextBox _log = new();
    private readonly Button _checkButton = new();
    private readonly Button _updateButton = new();
    private readonly Button _cancelButton = new();
    private readonly Button _openLauncherButton = new();
    private readonly Button _openFolderButton = new();
    private readonly Button _logsButton = new();

    private CancellationTokenSource? _cts;
    private UpdatePlan? _lastPlan;
    private string _activeOperation = "idle";

    public MainForm()
    {
        _stateStore = new StateStore(_logger);
        _config = UpdaterConfig.Load(_logger);
        _state = _stateStore.Load();

        Text = "MuDickLand Updater";
        MinimumSize = new Size(820, 620);
        StartPosition = FormStartPosition.CenterScreen;

        BuildUi();
        LoadStateIntoUi();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 8,
            Padding = new Padding(16),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var title = new Label
        {
            Text = "MuDickLand Experimental",
            Font = new Font(Font.FontFamily, 18, FontStyle.Bold),
            AutoSize = true,
            Dock = DockStyle.Fill
        };
        root.Controls.Add(title);

        root.Controls.Add(new Label
        {
            Text = "This updater only manages pack files. It does not handle Minecraft accounts or authentication.",
            AutoSize = true,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 4, 0, 4)
        });

        root.Controls.Add(new Label
        {
            Text = "Telemetry is optional and limited to updater events, app version, pack version, status, and a random install id. It never sends process lists, accounts, tokens, hardware ids, nicknames, or folder contents.",
            AutoSize = true,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 0, 0, 10)
        });
        if (_config.AllowInsecureHttp)
        {
            root.Controls.Add(new Label
            {
                Text = "Warning: public HTTP is enabled in updater.json. Manifest signatures still protect files, but traffic is not encrypted. Use HTTPS for public distribution.",
                AutoSize = true,
                Dock = DockStyle.Fill,
                ForeColor = Color.DarkRed,
                Padding = new Padding(0, 0, 0, 10)
            });
        }

        root.Controls.Add(MakeLabeledRow("Install directory", _installDir, ("Browse", BrowseInstallDir)));
        root.Controls.Add(MakeLabeledRow("latest.json URL", _latestUrl));

        _telemetryEnabled.Text = "Send minimal updater telemetry";
        _telemetryEnabled.AutoSize = true;
        _telemetryEnabled.Padding = new Padding(0, 6, 0, 6);
        root.Controls.Add(_telemetryEnabled);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Dock = DockStyle.Fill
        };

        ConfigureButton(_checkButton, "Check", async (_, _) => await RunCheckAsync());
        ConfigureButton(_updateButton, "Update", async (_, _) => await RunUpdateAsync());
        ConfigureButton(_cancelButton, "Cancel", (_, _) => _cts?.Cancel());
        ConfigureButton(_openLauncherButton, "Open Launcher", async (_, _) => await OpenLauncherAsync());
        ConfigureButton(_openFolderButton, "Open Folder", (_, _) => OpenInstallFolder());
        ConfigureButton(_logsButton, "Logs", (_, _) => OpenLogs());

        buttons.Controls.AddRange([_checkButton, _updateButton, _cancelButton, _openLauncherButton, _openFolderButton, _logsButton]);
        root.Controls.Add(buttons);

        _progress.Dock = DockStyle.Fill;
        _progress.Height = 24;
        root.Controls.Add(_progress);

        _log.Dock = DockStyle.Fill;
        _log.ReadOnly = true;
        _log.Font = new Font("Consolas", 10);
        root.Controls.Add(_log);

        var links = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Dock = DockStyle.Fill
        };
        links.Controls.Add(MakeLink("Website", _config.SiteUrl));
        links.Controls.Add(MakeLink("Telegram", _config.TelegramUrl));
        links.Controls.Add(MakeLink("Support / Issues", _config.SupportUrl));
        links.Controls.Add(new Label { Text = "Logs: " + _logger.LogPath, AutoSize = true, Padding = new Padding(12, 5, 0, 0) });
        root.Controls.Add(links);

        _status.Text = "Ready.";
        _status.AutoSize = true;
        _status.Dock = DockStyle.Bottom;
        Controls.Add(_status);
    }

    private void LoadStateIntoUi()
    {
        _installDir.Text = !string.IsNullOrWhiteSpace(_state.InstallDir)
            ? _state.InstallDir
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                UpdaterConfig.DefaultInstallFolderName);
        _latestUrl.Text = _config.LatestUrl;
        _telemetryEnabled.Checked = _state.TelemetryEnabled;
        _cancelButton.Enabled = false;
    }

    private static Control MakeLabeledRow(string label, TextBox textBox, (string Text, EventHandler Handler)? button = null)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = button is null ? 2 : 3,
            Padding = new Padding(0, 4, 0, 4)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        if (button is not null)
        {
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        }

        panel.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        textBox.Dock = DockStyle.Fill;
        panel.Controls.Add(textBox, 1, 0);

        if (button is not null)
        {
            var btn = new Button { Text = button.Value.Text, Dock = DockStyle.Fill };
            btn.Click += button.Value.Handler;
            panel.Controls.Add(btn, 2, 0);
        }

        return panel;
    }

    private static void ConfigureButton(Button button, string text, EventHandler handler)
    {
        button.Text = text;
        button.AutoSize = true;
        button.MinimumSize = new Size(120, 34);
        button.Click += handler;
    }

    private static LinkLabel MakeLink(string text, string url)
    {
        var link = new LinkLabel { Text = text, AutoSize = true, Padding = new Padding(0, 5, 12, 0) };
        link.Click += (_, _) => OpenUrl(url);
        return link;
    }

    private void BrowseInstallDir(object? sender, EventArgs args)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select MuDickLand install directory",
            UseDescriptionForTitle = true,
            SelectedPath = _installDir.Text
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _installDir.Text = dialog.SelectedPath;
        }
    }

    private async Task RunCheckAsync()
    {
        await RunWithUiLockAsync(async cancellationToken =>
        {
            SaveUiState();
            var engine = NewEngine();
            var progress = MakeProgress();
            _lastPlan = await engine.BuildPlanAsync(_installDir.Text, progress, cancellationToken);
            Append($"Version: {_lastPlan.Manifest.Version}");
            Append($"Need download: {_lastPlan.Downloads.Count} files, {FormatBytes(_lastPlan.BytesToDownload)}");
            Append($"Need delete: {_lastPlan.Deletes.Count} files");
            await NewTelemetryClient().SendAsync("check", "success", _lastPlan.Manifest.Version, cancellationToken);
        }, "check");
    }

    private async Task RunUpdateAsync()
    {
        await RunWithUiLockAsync(async cancellationToken =>
        {
            SaveUiState();
            var engine = NewEngine();
            var progress = MakeProgress();
            var plan = _lastPlan ?? await engine.BuildPlanAsync(_installDir.Text, progress, cancellationToken);
            await engine.ApplyPlanAsync(_installDir.Text, plan, progress, cancellationToken);
            _state.LastReleaseNumbers[plan.Manifest.PackId] = plan.Manifest.ReleaseNumber;
            _stateStore.Save(_state);
            await NewTelemetryClient().SendAsync("update_success", "success", plan.Manifest.Version, cancellationToken);
            _lastPlan = null;
        }, "update");
    }

    private async Task RunWithUiLockAsync(Func<CancellationToken, Task> action, string operation)
    {
        SetBusy(true);
        _cts = new CancellationTokenSource();
        try
        {
            _activeOperation = operation;
            await action(_cts.Token);
        }
        catch (OperationCanceledException)
        {
            Append("Canceled.");
            _status.Text = "Canceled.";
        }
        catch (Exception ex)
        {
            Append("ERROR: " + ex.Message);
            _status.Text = "Error.";
            _logger.Write(ex.ToString());
            try
            {
                var eventName = _activeOperation == "update" ? "update_failed" : "check";
                await NewTelemetryClient().SendAsync(eventName, ex.GetType().Name, "", CancellationToken.None);
            }
            catch
            {
                // Telemetry errors are non-fatal.
            }
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            _activeOperation = "idle";
            SetBusy(false);
        }
    }

    private Progress<UpdaterProgress> MakeProgress()
    {
        return new Progress<UpdaterProgress>(progress =>
        {
            _progress.Value = Math.Clamp(progress.Percent, 0, 100);
            _status.Text = progress.Message;
            Append(progress.Message);
        });
    }

    private void SaveUiState()
    {
        _state.InstallDir = _installDir.Text;
        _state.TelemetryEnabled = _telemetryEnabled.Checked;
        _stateStore.Save(_state);
        _config.LatestUrl = _latestUrl.Text;
    }

    private UpdaterEngine NewEngine() => new(_http, _config, _state, _logger);

    private TelemetryClient NewTelemetryClient() => new(_http, _config, _state, _logger);

    private void SetBusy(bool busy)
    {
        _checkButton.Enabled = !busy;
        _updateButton.Enabled = !busy;
        _cancelButton.Enabled = busy;
        _openLauncherButton.Enabled = !busy;
        _openFolderButton.Enabled = !busy;
        _logsButton.Enabled = !busy;
    }

    private void Append(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(Append), message);
            return;
        }

        _log.AppendText($"[{DateTimeOffset.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        _log.ScrollToCaret();
        _logger.Write(message);
    }

    private async Task OpenLauncherAsync()
    {
        if (!string.IsNullOrWhiteSpace(_config.LauncherPath) && File.Exists(_config.LauncherPath))
        {
            Process.Start(new ProcessStartInfo(_config.LauncherPath) { UseShellExecute = true });
            await NewTelemetryClient().SendAsync("open_launcher", "configured", "", CancellationToken.None);
            return;
        }

        OpenInstallFolder();
        await NewTelemetryClient().SendAsync("open_launcher", "not_configured", "", CancellationToken.None);
        MessageBox.Show(
            this,
            "Launcher path is not configured. Open your Minecraft launcher manually and set its game directory to the selected install folder.",
            "Launcher not configured",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void OpenInstallFolder()
    {
        Directory.CreateDirectory(_installDir.Text);
        Process.Start(new ProcessStartInfo(_installDir.Text) { UseShellExecute = true });
    }

    private void OpenLogs()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_logger.LogPath)!);
        if (!File.Exists(_logger.LogPath))
        {
            File.WriteAllText(_logger.LogPath, "");
        }
        Process.Start(new ProcessStartInfo(_logger.LogPath) { UseShellExecute = true });
    }

    private static void OpenUrl(string url)
    {
        if (!string.IsNullOrWhiteSpace(url))
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var value = (double)bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }
}
