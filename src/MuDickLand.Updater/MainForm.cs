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
    private readonly ProgressBar _progress = new();
    private readonly Label _status = new();
    private readonly RichTextBox _log = new();
    private readonly ToolTip _toolTip = new();
    private readonly Button _checkButton = new();
    private readonly Button _updateButton = new();
    private readonly Button _cancelButton = new();
    private readonly Button _openFolderButton = new();
    private readonly Button _helpButton = new();

    private CancellationTokenSource? _cts;
    private UpdatePlan? _lastPlan;
    private string _activeOperation = "idle";

    public MainForm()
    {
        _stateStore = new StateStore(_logger);
        _config = UpdaterConfig.Load(_logger);
        _state = _stateStore.Load();

        Text = "Апдейтер MuDickLand";
        MinimumSize = new Size(680, 520);
        Size = new Size(920, 640);
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
            RowCount = 10,
            Padding = new Padding(16),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
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
            Text = "Апдейтер ставит и обновляет файлы сборки. Логин, пароль и аккаунт Minecraft он не просит.",
            AutoSize = false,
            Height = 28,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 4, 0, 4)
        });

        root.Controls.Add(new Label
        {
            Text = "Порядок простой: выбери папку игры, нажми «Проверить», потом «Обновить». Эту же папку укажи в лаунчере как Game directory.",
            AutoSize = false,
            Height = 40,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 0, 0, 10)
        });

        ConfigureToolTips();

        root.Controls.Add(MakeLabeledRow(
            "Папка игры",
            _installDir,
            ("Обзор...", BrowseInstallDir, "Выбрать папку, куда апдейтер поставит сборку.")));

        root.Controls.Add(new Label
        {
            Text = @"Рекомендуемый отдельный профиль: %APPDATA%\.minecraft\versions\MuDickLand_experimental. Если играешь через обычную .minecraft, выбери в апдейтере %APPDATA%\.minecraft.",
            AutoSize = false,
            Height = 54,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 0, 0, 8)
        });

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 2, 0, 8)
        };

        ConfigureButton(_checkButton, "Проверить", async (_, _) => await RunCheckAsync());
        ConfigureButton(_updateButton, "Обновить", async (_, _) => await RunUpdateAsync());
        ConfigureButton(_cancelButton, "Стоп", (_, _) => _cts?.Cancel());
        ConfigureButton(_openFolderButton, "Открыть папку", (_, _) => OpenInstallFolder());
        ConfigureButton(_helpButton, "Помощь", (_, _) => ShowHelp());

        buttons.Controls.AddRange([
            _checkButton,
            _updateButton,
            _cancelButton,
            _openFolderButton,
            _helpButton
        ]);
        root.Controls.Add(buttons);

        var progressPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 8,
            MinimumSize = new Size(0, 8),
            Margin = new Padding(0, 0, 0, 8)
        };
        _progress.Dock = DockStyle.Fill;
        _progress.Height = 6;
        _progress.Style = ProgressBarStyle.Continuous;
        progressPanel.Controls.Add(_progress);
        root.Controls.Add(progressPanel);

        _log.Dock = DockStyle.Fill;
        _log.ReadOnly = true;
        _log.Font = new Font("Consolas", 10);
        _log.WordWrap = false;
        root.Controls.Add(_log);

        var links = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Dock = DockStyle.Fill
        };
        links.Controls.Add(MakeLink("Сайт", _config.SiteUrl));
        links.Controls.Add(MakeLink("Telegram", _config.TelegramUrl));
        links.Controls.Add(MakeLink("GitHub", _config.GitHubUrl));
        links.Controls.Add(MakeLink("Поддержка", _config.SupportUrl));
        root.Controls.Add(links);

        _status.Text = "Готово.";
        _status.AutoSize = false;
        _status.AutoEllipsis = true;
        _status.Height = 30;
        _status.Dock = DockStyle.Fill;
        _status.Padding = new Padding(0, 6, 0, 0);
        root.Controls.Add(_status);
    }

    private void LoadStateIntoUi()
    {
        _installDir.Text = !string.IsNullOrWhiteSpace(_state.InstallDir)
            ? _state.InstallDir
            : DefaultInstallDir();
        _cancelButton.Enabled = false;
    }

    private static string DefaultInstallDir()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ".minecraft",
            "versions",
            "MuDickLand_experimental");
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
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < buttons.Length; i++)
        {
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
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
        button.AutoSize = false;
        button.Size = new Size(118, 34);
        button.MinimumSize = new Size(118, 34);
        button.Margin = new Padding(0, 0, 8, 8);
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
            Append($"Старых файлов к удалению: {_lastPlan.Deletes.Count}");
            if (_lastPlan.Deletes.Count > 0)
            {
                Append("Будут удалены:");
                foreach (var path in _lastPlan.Deletes.Take(20))
                {
                    Append("  " + path);
                }

                if (_lastPlan.Deletes.Count > 20)
                {
                    Append($"  ...и еще {_lastPlan.Deletes.Count - 20}");
                }
            }
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
        catch (UpdaterOutdatedException ex)
        {
            Append("ОШИБКА: " + ex.Message);
            _status.Text = "Нужно обновить апдейтер.";
            _logger.Write(ex.ToString());
            var target = !string.IsNullOrWhiteSpace(ex.DownloadUrl)
                ? ex.DownloadUrl
                : !string.IsNullOrWhiteSpace(ex.PageUrl)
                    ? ex.PageUrl
                    : _config.SiteUrl;
            var result = MessageBox.Show(
                this,
                ex.Message + "\n\nОткрыть страницу скачивания?",
                "Обновите апдейтер",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (result == DialogResult.Yes)
            {
                OpenUrl(target);
            }
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
            if (ShouldAppendProgress(progress.Message))
            {
                Append(progress.Message);
            }
        });
    }

    private static bool ShouldAppendProgress(string message)
    {
        return !message.StartsWith("Проверяю файл ", StringComparison.OrdinalIgnoreCase);
    }

    private void SaveUiState()
    {
        _state.InstallDir = _installDir.Text;
        _stateStore.Save(_state);
    }

    private UpdaterEngine NewEngine() => new(_http, _config, _state, _logger);

    private TelemetryClient NewTelemetryClient() => new(_http, _config, _state, _logger);

    private void SetBusy(bool busy)
    {
        _checkButton.Enabled = !busy;
        _updateButton.Enabled = !busy;
        _cancelButton.Enabled = busy;
        _openFolderButton.Enabled = !busy;
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

    private void ConfigureToolTips()
    {
        _toolTip.AutoPopDelay = 12000;
        _toolTip.InitialDelay = 400;
        _toolTip.ReshowDelay = 100;
        _toolTip.ShowAlways = true;
        _toolTip.SetToolTip(_installDir, "Папка, куда апдейтер ставит сборку.");
        _toolTip.SetToolTip(_checkButton, "Проверить, что нужно скачать.");
        _toolTip.SetToolTip(_updateButton, "Скачать и применить обновление.");
        _toolTip.SetToolTip(_cancelButton, "Остановить текущую проверку или обновление.");
        _toolTip.SetToolTip(_openFolderButton, "Открыть папку игры.");
        _toolTip.SetToolTip(_helpButton, "Показать короткую подсказку.");
        _toolTip.SetToolTip(_progress, "Прогресс.");
        _toolTip.SetToolTip(_log, "Журнал действий. Текст можно выделить и скопировать.");
    }

    private void ShowHelp()
    {
        MessageBox.Show(
            this,
            "1. Выбери папку игры.\n" +
            "2. Нажми «Проверить».\n" +
            "3. Нажми «Обновить».\n" +
            "4. В лаунчере укажи эту же папку как Game directory.\n\n" +
            @"Рекомендуемый отдельный профиль: %APPDATA%\.minecraft\versions\MuDickLand_experimental." + "\n\n" +
            @"Обычная %APPDATA%\.minecraft тоже подходит, если в апдейтере выбрана она же." + "\n\n" +
            "Главное правило: папка в апдейтере и Game directory в лаунчере должны совпадать.\n\n" +
            "Если появилась ошибка, выдели текст в нижнем журнале, скопируй его и отправь в поддержку.",
            "Помощь",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void OpenInstallFolder()
    {
        Directory.CreateDirectory(_installDir.Text);
        Process.Start(new ProcessStartInfo(_installDir.Text) { UseShellExecute = true });
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
