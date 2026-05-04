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
    private readonly ToolTip _toolTip = new();
    private readonly Button _checkButton = new();
    private readonly Button _updateButton = new();
    private readonly Button _cancelButton = new();
    private readonly Button _openLauncherButton = new();
    private readonly Button _openFolderButton = new();
    private readonly Button _logsButton = new();
    private readonly Button _copyLogPathButton = new();
    private readonly Button _helpButton = new();

    private CancellationTokenSource? _cts;
    private UpdatePlan? _lastPlan;
    private string _activeOperation = "idle";

    public MainForm()
    {
        _stateStore = new StateStore(_logger);
        _config = UpdaterConfig.Load(_logger);
        _state = _stateStore.Load();

        Text = "Обновлятор MuDickLand";
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
            Text = "Этот обновлятор управляет только файлами сборки. Он не работает с аккаунтами Minecraft и авторизацией.",
            AutoSize = true,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 4, 0, 4)
        });

        root.Controls.Add(new Label
        {
            Text = "Телеметрия необязательна и ограничена событиями обновлятора, версией приложения, версией сборки, статусом и случайным id установки. Она не отправляет списки процессов, аккаунты, токены, id железа, никнеймы или содержимое папок.",
            AutoSize = true,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 0, 0, 10)
        });
        if (_config.AllowInsecureHttp)
        {
            root.Controls.Add(new Label
            {
                Text = "Внимание: в updater.json включен публичный HTTP. Подписи манифеста по-прежнему защищают файлы, но трафик не шифруется. Для публичной раздачи используйте HTTPS.",
                AutoSize = true,
                Dock = DockStyle.Fill,
                ForeColor = Color.DarkRed,
                Padding = new Padding(0, 0, 0, 10)
            });
        }

        ConfigureToolTips();

        root.Controls.Add(MakeLabeledRow(
            "Папка установки",
            _installDir,
            ("Обзор...", BrowseInstallDir, "Выбрать папку, куда будут скачиваться и обновляться файлы сборки.")));
        root.Controls.Add(MakeLabeledRow(
            "URL latest.json",
            _latestUrl,
            ("Копировать URL", (_, _) => CopyLatestUrl(), "Скопировать текущий URL latest.json в буфер обмена.")));

        _telemetryEnabled.Text = "Отправлять минимальную телеметрию обновлятора";
        _telemetryEnabled.AutoSize = true;
        _telemetryEnabled.Padding = new Padding(0, 6, 0, 6);
        root.Controls.Add(_telemetryEnabled);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Dock = DockStyle.Fill
        };

        ConfigureButton(_checkButton, "Проверить", async (_, _) => await RunCheckAsync());
        ConfigureButton(_updateButton, "Обновить", async (_, _) => await RunUpdateAsync());
        ConfigureButton(_cancelButton, "Стоп", (_, _) => _cts?.Cancel());
        ConfigureButton(_openLauncherButton, "Открыть лаунчер", async (_, _) => await OpenLauncherAsync());
        ConfigureButton(_openFolderButton, "Открыть папку", (_, _) => OpenInstallFolder());
        ConfigureButton(_logsButton, "Открыть лог", (_, _) => OpenLogs());
        ConfigureButton(_copyLogPathButton, "Копировать путь лога", (_, _) => CopyLogPath());
        ConfigureButton(_helpButton, "Помощь", (_, _) => ShowHelp());

        buttons.Controls.AddRange([
            _checkButton,
            _updateButton,
            _cancelButton,
            _openLauncherButton,
            _openFolderButton,
            _logsButton,
            _copyLogPathButton,
            _helpButton
        ]);
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
        links.Controls.Add(MakeLink("Сайт", _config.SiteUrl));
        links.Controls.Add(MakeLink("Telegram", _config.TelegramUrl));
        links.Controls.Add(MakeLink("Поддержка / ошибки", _config.SupportUrl));
        links.Controls.Add(new Label { Text = "Лог: " + _logger.LogPath, AutoSize = true, Padding = new Padding(12, 5, 0, 0) });
        root.Controls.Add(links);

        _status.Text = "Готово.";
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

    private Control MakeLabeledRow(string label, TextBox textBox, params (string Text, EventHandler Handler, string ToolTip)[] buttons)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2 + buttons.Length,
            Padding = new Padding(0, 4, 0, 4)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < buttons.Length; i++)
        {
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128));
        }

        panel.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        textBox.Dock = DockStyle.Fill;
        panel.Controls.Add(textBox, 1, 0);

        for (var i = 0; i < buttons.Length; i++)
        {
            var button = buttons[i];
            var btn = new Button { Text = button.Text, Dock = DockStyle.Fill };
            btn.Click += button.Handler;
            _toolTip.SetToolTip(btn, button.ToolTip);
            panel.Controls.Add(btn, 2 + i, 0);
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
            Description = "Выберите папку установки MuDickLand",
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
            _stateStore.Save(_state);
            Append($"Версия: {_lastPlan.Manifest.Version}");
            Append($"Нужно скачать: {_lastPlan.Downloads.Count} файлов, {FormatBytes(_lastPlan.BytesToDownload)}");
            Append($"Нужно удалить: {_lastPlan.Deletes.Count} файлов");
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
            Append("Остановлено.");
            _status.Text = "Остановлено.";
        }
        catch (Exception ex)
        {
            Append("ОШИБКА: " + ex.Message);
            _status.Text = "Ошибка.";
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
        _copyLogPathButton.Enabled = !busy;
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
            "Путь к лаунчеру не настроен. Откройте Minecraft Launcher вручную и укажите в нем папку игры: выбранную папку установки.",
            "Лаунчер не настроен",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void ConfigureToolTips()
    {
        _toolTip.AutoPopDelay = 12000;
        _toolTip.InitialDelay = 400;
        _toolTip.ReshowDelay = 100;
        _toolTip.ShowAlways = true;
        _toolTip.SetToolTip(_installDir, "Папка, где лежат файлы сборки. Обновлятор будет сверять и менять файлы внутри нее.");
        _toolTip.SetToolTip(_latestUrl, "Адрес latest.json с информацией о последней версии сборки.");
        _toolTip.SetToolTip(_telemetryEnabled, "Включает или выключает минимальные события обновлятора без персональных данных.");
        _toolTip.SetToolTip(_checkButton, "Проверить манифест и показать, какие файлы нужно скачать или удалить.");
        _toolTip.SetToolTip(_updateButton, "Скачать недостающие файлы, заменить устаревшие и удалить лишнее по манифесту.");
        _toolTip.SetToolTip(_cancelButton, "Остановить текущую проверку или обновление.");
        _toolTip.SetToolTip(_openLauncherButton, "Запустить настроенный лаунчер или открыть папку установки, если лаунчер не задан.");
        _toolTip.SetToolTip(_openFolderButton, "Открыть выбранную папку установки в проводнике.");
        _toolTip.SetToolTip(_logsButton, "Открыть файл лога обновлятора.");
        _toolTip.SetToolTip(_copyLogPathButton, "Скопировать путь к файлу лога в буфер обмена.");
        _toolTip.SetToolTip(_helpButton, "Показать краткую справку по кнопкам и частым ошибкам.");
        _toolTip.SetToolTip(_progress, "Текущий прогресс проверки или обновления.");
        _toolTip.SetToolTip(_log, "Журнал действий за текущий запуск. Полный файл лога можно открыть кнопкой \"Открыть лог\".");
    }

    private void ShowHelp()
    {
        MessageBox.Show(
            this,
            "Проверить: скачивает latest.json, сверяет манифест с выбранной папкой и пишет в журнал, что нужно скачать или удалить.\n\n" +
            "Обновить: выполняет найденный план обновления. Если проверка не запускалась, сначала строит план автоматически.\n\n" +
            "Стоп: останавливает текущую проверку или обновление. Кнопка активна только во время операции.\n\n" +
            "Лог: краткий журнал виден в окне ниже. Полный файл открывается кнопкой \"Открыть лог\". Путь можно скопировать кнопкой \"Копировать путь лога\".\n\n" +
            "Ошибка 429: сервер временно ограничил частоту запросов. Подождите несколько минут и повторите попытку. Если ошибка повторяется, откройте лог и отправьте его в поддержку вместе с URL latest.json.",
            "Помощь",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void CopyLatestUrl()
    {
        CopyTextToClipboard(_latestUrl.Text, "URL latest.json");
    }

    private void CopyLogPath()
    {
        CopyTextToClipboard(_logger.LogPath, "Путь к логу");
    }

    private void CopyTextToClipboard(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            _status.Text = $"{name} пуст.";
            return;
        }

        Clipboard.SetText(value);
        _status.Text = $"{name} скопирован.";
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
