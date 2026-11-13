using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using Helpers.App.Overlays;
using Helpers.App.Services;
using Helpers.Core.Settings;
using Helpers.Speech;
using Helpers.Windows;

namespace Helpers.App;

public partial class App : Application
{
    private SettingsStore? _settings;
    private ToastService? _toasts;
    private ToastWindow? _toastWindow;
    private ReadingController? _reading;
    private ClipboardWatcher? _clipboard;
    private HotkeyService? _hotkeys;
    private SelectionCapture? _selection;
    private InputMonitor? _input;
    private ReadButtonService? _readButton;
    private ComposeController? _compose;
    private AssistantService? _assistant;
    private WordToolsService? _words;
    private UpdateService? _updates;
    private KokoroEngine? _engine;
    private QuickMenuWindow? _quickMenu;
    private TrayIcon? _tray;
    private NativeMenuItem? _watchItem;
    private NativeMenuItem? _calmItem;
    private NativeMenuItem? _pausePillItem;
    private Windows.SettingsWindow? _settingsWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // No main window: the app lives in the tray and shows small surfaces when needed.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _settings = new SettingsStore();
            var settings = _settings.Load();
            Vibes.Apply(this, settings);
            ReadableText.Apply(this, settings);
            UiScale.Set(settings.UiScale);

            _toasts = new ToastService();
            _toastWindow = new ToastWindow(_toasts) { FollowFocus = settings.MessagesOn == MessageScreen.Focused };

            var modelsRoot = settings.ModelsFolder ?? SettingsStore.DefaultModelsFolder();
            var engine = new KokoroEngine(modelsRoot, Math.Clamp(Environment.ProcessorCount / 2, 2, 4));
            _engine = engine;
            _reading = new ReadingController(engine, new NAudioOutput(), _settings, _toasts);
            _reading.SettingsRequested += ShowSettings;

            _clipboard = new ClipboardWatcher();
            _clipboard.Changed += () => Dispatcher.UIThread.Post(OfferToReadClipboard);
            if (settings.WatchClipboard)
            {
                _clipboard.Start();
            }

            _selection = new SelectionCapture();
            _assistant = new AssistantService(_settings, new CredentialStore());
            _words = new WordToolsService(_settings, _reading, _toasts);
            _updates = new UpdateService(_settings, _toasts);
            _updates.Start();
            _compose = new ComposeController(_settings, _reading, _toasts, _assistant, _words);
            _hotkeys = new HotkeyService();
            ClaimReadHotkeyAtStart();
            ApplyComposeHotkey();

            // Started a second time: that copy has closed, so say this one is here.
            if (Program.Instance is { } instance)
            {
                instance.StartedAgain += () => _toasts.Info("Unmuddle is already running. Its icon is by the clock.");
                instance.Listen();
            }

            // The mouse and keyboard hooks share the hotkey's message thread.
            _input = new InputMonitor(_hotkeys.Window);
            if (_input.Install())
            {
                _readButton = new ReadButtonService(_input, _settings, _reading, _selection, _toasts, _compose);
                ApplyReadButtonHotkey();
                TellAboutTheReadButtonChange();
                _input.MouseDown += (_, _, _, ours) =>
                {
                    if (!ours)
                    {
                        _quickMenu?.HideMenu();
                    }
                };
                _input.KeyPressed += () => _quickMenu?.HideMenu();
            }
            else
            {
                _toasts.Error("Windows wouldn't allow the mouse hook, so the Read button is off. The shortcut still works.");
            }

            _tray = BuildTrayIcon(desktop, settings);
            TrayIcon.SetIcons(this, [_tray]);

            desktop.Exit += (_, _) =>
            {
                _compose?.Dispose();
                _updates?.Dispose();
                _words?.Dispose();
                _assistant?.Dispose();
                _readButton?.Dispose();
                _input?.Dispose();
                _hotkeys?.Dispose();
                _clipboard?.Dispose();
                _reading?.Dispose();
            };

            if (!settings.FirstRunDone)
            {
                ShowFirstRun(engine);
            }

            _ = StartAsync(desktop.Args ?? []);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>One screen on the first launch. Everything it sets can be changed later in Settings.</summary>
    private void ShowFirstRun(KokoroEngine engine)
    {
        if (_settings is null || _reading is null)
        {
            return;
        }

        var current = engine.Voices.FirstOrDefault(v => v.Id == _settings.Current.VoiceId) ?? KokoroEngine.DefaultVoice;
        var window = new Windows.FirstRunWindow(engine.Voices, current);
        window.VoiceChosen += voice => _reading.SetVoice(voice);
        window.PreviewRequested += voice =>
        {
            _reading.SetVoice(voice);
            _reading.Read($"Hello, I'm {voice.DisplayName}. Select some text anywhere and I will read it to you.");
        };
        window.Finished += (voice, readButton, startup, updates) =>
        {
            _settings.Update(s =>
            {
                s.VoiceId = voice.Id;
                s.ReadButtonAfterSelection = readButton;
                s.ReadButtonChangeTold = true;
                s.FirstRunDone = true;
                s.Updates.CheckForUpdates = updates;
                s.Updates.Asked = true;
            });
            if (startup != StartupRegistration.IsEnabled())
            {
                SetStartWithWindows(startup);
            }

            _updates?.Start();
        };
        window.Show();
    }

    private async Task StartAsync(string[] args)
    {
        if (_reading is null)
        {
            return;
        }

        await _reading.WarmUpAsync();

        // Developer switches, harmless if never used.
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--read-file" && i + 1 < args.Length && File.Exists(args[i + 1]))
            {
                _reading.Read(await File.ReadAllTextAsync(args[i + 1]));
            }
            else if (args[i] == "--read-clipboard")
            {
                _reading.ReadClipboard();
            }
            else if (args[i] == "--settings")
            {
                ShowSettings();
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    _settingsWindow?.SelectTab(args[++i]);
                }
            }
            else if (args[i] == "--lookup" && i + 1 < args.Length)
            {
                _words?.Show(args[++i]);
            }
            else if (args[i] == "--dictionary" && _words is not null && _toasts is not null)
            {
                var toast = _toasts.Progress("Downloading the dictionary…");
                try
                {
                    await _words.DownloadDictionaryAsync(new Progress<double>(fraction => toast.Fraction = fraction));
                    _toasts.Info("Dictionary ready");
                }
                catch (Exception ex)
                {
                    _toasts.Error($"Dictionary download failed: {ex.Message}");
                }
                finally
                {
                    _toasts.Dismiss(toast);
                }
            }
            else if (args[i] == "--compose-check")
            {
                _compose?.OpenAndRun(Helpers.Core.Ai.AssistantAction.CheckMyThinking);
            }
            else if (args[i] == "--compose-tidy")
            {
                _compose?.OpenAndRun(Helpers.Core.Ai.AssistantAction.Tidy);
            }
            else if (args[i] == "--ai-test" && i + 2 < args.Length)
            {
                _ = AiTestAsync(args[i + 1], args[i + 2]);
                i += 2;
            }
            else if (args[i] == "--expanded")
            {
                _reading.ExpandPlayer();
            }
            else if (args[i] == "--menu")
            {
                ShowQuickMenu();
            }
            else if (args[i] == "--compose")
            {
                _compose?.Open();
            }
            else if (args[i] == "--first-run" && _engine is not null)
            {
                ShowFirstRun(_engine);
            }
            else if (args[i] == "--pill")
            {
                _readButton?.ShowForPreview();
            }
        }
    }

    /// <summary>
    /// A developer check of the local model: downloads it if needed, runs Check
    /// my thinking and Tidy on a file, and writes what came back and how long
    /// it took to another file. Uses its own settings file, so nothing the user
    /// chose is touched.
    /// </summary>
    private async Task AiTestAsync(string inputPath, string outputPath)
    {
        if (_toasts is null)
        {
            return;
        }

        var store = new SettingsStore(Path.Combine(Path.GetTempPath(), "helpers-ai-test-settings.json"));
        store.Load();
        store.Update(s => s.Ai.Provider = Helpers.Core.Ai.AiProvider.Local);
        using var service = new AssistantService(store, new CredentialStore());
        var log = new System.Text.StringBuilder();
        try
        {
            var text = await File.ReadAllTextAsync(inputPath);
            if (!service.LocalModelDownloaded)
            {
                var toast = _toasts.Progress("Downloading the local model for a test…");
                await service.DownloadLocalModelAsync(new Progress<double>(fraction => toast.Fraction = fraction));
                _toasts.Dismiss(toast);
                log.AppendLine("downloaded");
            }

            foreach (var action in new[] { Helpers.Core.Ai.AssistantAction.CheckMyThinking, Helpers.Core.Ai.AssistantAction.Tidy })
            {
                var watch = Stopwatch.StartNew();
                var result = await service.RunAsync(action, text, null, CancellationToken.None);
                log.AppendLine($"== {action} in {watch.Elapsed.TotalSeconds:0.0} s");
                log.AppendLine(result.Output);
                var notes = Helpers.Core.Ai.NotesParser.Parse(result.Output, text, action);
                log.AppendLine($"-- {notes.Count} notes parsed");
                foreach (var note in notes)
                {
                    log.AppendLine($"   [{note.Kind}] at {note.Start}+{note.Length} \"{note.Span}\" -> {note.Note} | fix: {note.Fix ?? "none"}");
                }
            }

            using var process = Process.GetCurrentProcess();
            process.Refresh();
            log.AppendLine($"working set {process.WorkingSet64 / 1_048_576:N0} MB with the model loaded");
        }
        catch (Exception ex)
        {
            log.AppendLine("ERROR " + ex);
        }

        await File.WriteAllTextAsync(outputPath, log.ToString());
        _toasts.Info("AI test finished");
    }

    private TrayIcon BuildTrayIcon(IClassicDesktopStyleApplicationLifetime desktop, AppSettings settings)
    {
        var menu = new NativeMenu();

        var readClipboard = new NativeMenuItem("Read clipboard");
        readClipboard.Click += (_, _) => _reading?.ReadClipboard();

        _watchItem = new NativeMenuItem("Watch clipboard") { ToggleType = MenuItemToggleType.CheckBox, IsChecked = settings.WatchClipboard };
        _watchItem.Click += (_, _) => ToggleWatchClipboard();

        var pause = new NativeMenuItem("Pause / Resume");
        pause.Click += (_, _) => _reading?.TogglePause();

        var stop = new NativeMenuItem("Stop");
        stop.Click += (_, _) => _reading?.Stop();

        var showPlayer = new NativeMenuItem("Show player");
        showPlayer.Click += (_, _) => _reading?.ShowPlayer();

        var compose = new NativeMenuItem("Compose…");
        compose.Click += (_, _) => _compose?.Open();

        var settingsItem = new NativeMenuItem("Settings…");
        settingsItem.Click += (_, _) => ShowSettings();

        _pausePillItem = new NativeMenuItem("Pause the Read button for 1 hour") { ToggleType = MenuItemToggleType.CheckBox };
        _pausePillItem.Click += (_, _) => TogglePillPause();

        _calmItem = new NativeMenuItem("Calm look") { ToggleType = MenuItemToggleType.CheckBox, IsChecked = settings.Vibe == Vibe.Calm };
        _calmItem.Click += (_, _) => ToggleCalm();

        _startupItem = new NativeMenuItem("Start with Windows") { ToggleType = MenuItemToggleType.CheckBox, IsChecked = StartupRegistration.IsEnabled() };
        _startupItem.Click += (_, _) => SetStartWithWindows(!StartupRegistration.IsEnabled());

        var memory = new NativeMenuItem("Memory in use");
        memory.Click += (_, _) => ShowMemory();

        var exit = new NativeMenuItem("Exit");
        exit.Click += (_, _) => desktop.Shutdown();

        menu.Items.Add(readClipboard);
        menu.Items.Add(_watchItem);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(pause);
        menu.Items.Add(stop);
        menu.Items.Add(showPlayer);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(compose);
        menu.Items.Add(settingsItem);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(_pausePillItem);
        menu.Items.Add(_calmItem);
        menu.Items.Add(_startupItem);
        menu.Items.Add(memory);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(exit);

        var tray = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Unmuddle/Assets/app.ico"))),
            ToolTipText = "Unmuddle",
            Menu = menu,
            IsVisible = true,
        };
        tray.Clicked += (_, _) => ShowQuickMenu();
        return tray;
    }

    /// <summary>The styled menu on a left-click of the tray icon.</summary>
    private void ShowQuickMenu()
    {
        if (_settings is null || _reading is null)
        {
            return;
        }

        _quickMenu ??= new QuickMenuWindow();
        _quickMenu.SetItems(
        [
            new QuickMenuItem("Read clipboard", () => _reading.ReadClipboard()),
            new QuickMenuItem("Pause / Resume", () => _reading.TogglePause()),
            new QuickMenuItem("Stop", () => _reading.Stop()),
            new QuickMenuItem("Show player", () => _reading.ShowPlayer()),
            QuickMenuItem.Separator,
            new QuickMenuItem("Compose…", () => _compose?.Open()),
            QuickMenuItem.Separator,
            new QuickMenuItem("Watch clipboard", ToggleWatchClipboard, () => _settings.Current.WatchClipboard),
            new QuickMenuItem("Pause the Read button for 1 hour", TogglePillPause, () => _readButton?.IsPaused ?? false),
            new QuickMenuItem("Calm look", ToggleCalm, () => _settings.Current.Vibe == Vibe.Calm),
            new QuickMenuItem("Start with Windows", () => SetStartWithWindows(!StartupRegistration.IsEnabled()), StartupRegistration.IsEnabled),
            QuickMenuItem.Separator,
            new QuickMenuItem("Settings…", ShowSettings),
            new QuickMenuItem("Memory in use", ShowMemory),
            new QuickMenuItem("Exit", () => (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown()),
        ]);

        var (x, y) = CursorPosition.Get();
        _quickMenu.ShowNear(new PixelPoint(x, y));
    }

    private NativeMenuItem? _startupItem;

    /// <summary>Registers or removes the Run key entry and keeps the menus and settings in step. Returns false if Windows refused.</summary>
    private bool SetStartWithWindows(bool enabled)
    {
        var ok = StartupRegistration.SetEnabled(enabled);
        var now = StartupRegistration.IsEnabled();
        _settings?.Update(s => s.StartWithWindows = now);
        if (_startupItem is not null)
        {
            _startupItem.IsChecked = now;
        }

        if (!ok)
        {
            _toasts?.Error("Windows wouldn't let the app change its start-up entry.");
        }
        else
        {
            _toasts?.Info(now ? "Unmuddle will start with Windows" : "Unmuddle will no longer start with Windows");
        }

        return ok;
    }

    private void ShowMemory()
    {
        using var process = Process.GetCurrentProcess();
        process.Refresh();
        _toasts?.Info($"Working set {process.WorkingSet64 / 1_048_576:N0} MB, private {process.PrivateMemorySize64 / 1_048_576:N0} MB");
    }

    private void TogglePillPause()
    {
        if (_readButton is null)
        {
            return;
        }

        if (_readButton.IsPaused)
        {
            _readButton.Resume();
            _toasts?.Info("The Read button is back");
        }
        else
        {
            _readButton.PauseFor(TimeSpan.FromHours(1));
            _toasts?.Info("Read button paused for an hour");
        }

        if (_pausePillItem is not null)
        {
            _pausePillItem.IsChecked = _readButton.IsPaused;
        }
    }

    /// <summary>
    /// Watch clipboard offers rather than reads: a copy during normal work must
    /// never start the voice by itself. The offer fades if ignored.
    /// </summary>
    private void OfferToReadClipboard()
    {
        if (_reading is null || _toasts is null)
        {
            return;
        }

        var text = ClipboardText.TryGet();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _toasts.Offer("Read what you just copied?", "Read", () => _reading.Read(text));
    }

    /// <summary>The shortcut's handler. The clipboard watcher sleeps while capture borrows the clipboard.</summary>
    private async Task ReadSelectionAsync()
    {
        if (_reading is null || _selection is null)
        {
            return;
        }

        _clipboard?.Suspend();
        try
        {
            await _reading.ReadSelectionOrStopAsync(_selection);
        }
        finally
        {
            _clipboard?.Resume();
        }
    }

    /// <summary>
    /// The read shortcut at start-up. Ctrl+Alt+Space was the first default, and
    /// other apps claim it too. A copy still on that default moves to the new
    /// one by itself when Windows refuses the old, and says so in a message
    /// that fades, rather than an error at every start.
    /// </summary>
    private void ClaimReadHotkeyAtStart()
    {
        if (_settings is null)
        {
            return;
        }

        var settings = _settings.Current;
        var onOldDefault = settings.ReadSelectionHotkeyEnabled
            && string.Equals(settings.ReadSelectionHotkey, AppSettings.PreviousReadSelectionHotkey, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(settings.ComposeHotkey, AppSettings.DefaultReadSelectionHotkey, StringComparison.OrdinalIgnoreCase);
        if (!onOldDefault)
        {
            ApplyReadHotkey();
            return;
        }

        if (TryApplyReadHotkey())
        {
            return;
        }

        _settings.Update(s => s.ReadSelectionHotkey = AppSettings.DefaultReadSelectionHotkey);
        if (TryApplyReadHotkey())
        {
            _toasts?.Offer(
                $"Another app uses {AppSettings.PreviousReadSelectionHotkey}, so reading is now {AppSettings.DefaultReadSelectionHotkey}.",
                "Open Settings",
                ShowSettings);
            return;
        }

        // Neither is free: leave the choice as it was and say so.
        _settings.Update(s => s.ReadSelectionHotkey = AppSettings.PreviousReadSelectionHotkey);
        ApplyReadHotkey();
    }

    /// <summary>Registers the read-selection shortcut from settings and tells the user if Windows refused it.</summary>
    private bool ApplyReadHotkey()
    {
        var ok = TryApplyReadHotkey();
        if (!ok && _settings is not null)
        {
            _toasts?.Error($"Couldn't claim the shortcut {_settings.Current.ReadSelectionHotkey}. Another app may be using it.", "Open Settings", ShowSettings);
        }

        return ok;
    }

    private bool TryApplyReadHotkey()
    {
        if (_hotkeys is null || _settings is null)
        {
            return false;
        }

        var settings = _settings.Current;
        var gesture = Helpers.Core.Input.HotkeyGesture.Parse(settings.ReadSelectionHotkey);
        return _hotkeys.Apply("read", gesture, settings.ReadSelectionHotkeyEnabled, () => _ = ReadSelectionAsync());
    }

    /// <summary>Registers the Compose shortcut. Pressed, it opens Compose sending to the window that had focus.</summary>
    private bool ApplyComposeHotkey()
    {
        if (_hotkeys is null || _settings is null)
        {
            return false;
        }

        var settings = _settings.Current;
        var gesture = Helpers.Core.Input.HotkeyGesture.Parse(settings.ComposeHotkey);
        var ok = _hotkeys.Apply("compose", gesture, settings.ComposeHotkeyEnabled, () => _compose?.Open());
        if (!ok)
        {
            _toasts?.Error($"Couldn't claim the Compose shortcut {settings.ComposeHotkey}. Another app may be using it.", "Open Settings", ShowSettings);
        }

        return ok;
    }

    /// <summary>Registers the shortcut that shows the Read button at the mouse.</summary>
    private bool ApplyReadButtonHotkey()
    {
        if (_hotkeys is null || _settings is null)
        {
            return false;
        }

        var settings = _settings.Current;
        var gesture = Helpers.Core.Input.HotkeyGesture.Parse(settings.ReadButtonHotkey);
        var ok = _hotkeys.Apply("read-button", gesture, settings.ReadButtonHotkeyEnabled, () => _readButton?.ShowOnRequest());
        if (!ok)
        {
            _toasts?.Error($"Couldn't claim the Read button shortcut {settings.ReadButtonHotkey}. Another app may be using it.", "Open Settings", ShowSettings);
        }

        return ok;
    }

    /// <summary>
    /// Up to 0.2.1 the Read button appeared after every selection. A copy from
    /// then is told, once, that it now waits to be asked. A new copy hears it
    /// on the welcome screen instead.
    /// </summary>
    private void TellAboutTheReadButtonChange()
    {
        if (_settings is null || _toasts is null)
        {
            return;
        }

        var settings = _settings.Current;
        if (!settings.FirstRunDone || settings.ReadButtonChangeTold)
        {
            return;
        }

        _settings.Update(s => s.ReadButtonChangeTold = true);
        var how = settings.ReadButtonHotkeyEnabled ? $"Press {settings.ReadButtonHotkey} when you want it." : "Give it a shortcut in Settings.";
        _toasts.Notice($"The Read button no longer pops up after every selection. {how} Settings can bring the old way back.", "Open Settings", () =>
        {
            ShowSettings();
            _settingsWindow?.SelectTab("Read button");
        });
    }

    private void ShowSettings()
    {
        if (_settings is null || _reading is null)
        {
            return;
        }

        if (_settingsWindow is null)
        {
            var viewModel = new ViewModels.SettingsViewModel(
                _settings,
                _reading,
                SetWatchClipboard,
                ApplyLook,
                ApplyReadHotkey,
                ApplyComposeHotkey,
                ApplyReadButtonHotkey,
                () => ReadableText.Apply(this, _settings.Current),
                SetStartWithWindows,
                StartupRegistration.IsEnabled(),
                _assistant!,
                _updates!)
            {
                MessagesFollowFocusChanged = follow =>
                {
                    if (_toastWindow is not null)
                    {
                        _toastWindow.FollowFocus = follow;
                    }
                },
            };
            _settingsWindow = new Windows.SettingsWindow(viewModel);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        }
        else
        {
            _settingsWindow.Activate();
        }
    }

    private void ToggleWatchClipboard()
    {
        if (_settings is null)
        {
            return;
        }

        var on = !_settings.Current.WatchClipboard;
        _settings.Update(s => s.WatchClipboard = on);
        SetWatchClipboard(on);
    }

    private void SetWatchClipboard(bool on)
    {
        if (_clipboard is null)
        {
            return;
        }

        if (_watchItem is not null)
        {
            _watchItem.IsChecked = on;
        }

        if (on)
        {
            _clipboard.Start();
            _toasts?.Info("Watching the clipboard. Copy anything and you will be offered a Read button.");
        }
        else
        {
            _clipboard.Stop();
            _toasts?.Info("Stopped watching the clipboard");
        }
    }

    private void ToggleCalm()
    {
        if (_settings is null)
        {
            return;
        }

        var calm = _settings.Current.Vibe != Vibe.Calm;
        _settings.Update(s => s.Vibe = calm ? Vibe.Calm : Vibe.Neon);
        ApplyLook();
    }

    private void ApplyLook()
    {
        if (_settings is null)
        {
            return;
        }

        if (_calmItem is not null)
        {
            _calmItem.IsChecked = _settings.Current.Vibe == Vibe.Calm;
        }

        Vibes.Apply(this, _settings.Current);
    }
}
