using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Helpers.App.Windows;
using Helpers.Core.Capture;
using Helpers.Core.Settings;
using Helpers.Core.Spelling;
using Helpers.Core.Platform;

namespace Helpers.App.Services;

/// <summary>
/// Owns the Compose window: which window it sends to, the spelling engine,
/// the saved draft, and what happens on Read back, Copy and Send.
/// </summary>
public sealed class ComposeController : IDisposable
{
    private static readonly TimeSpan PickTimeout = TimeSpan.FromSeconds(20);

    private readonly IPlatform _platform = AppPlatform.Current;
    private readonly SettingsStore _settings;
    private readonly ReadingController _reading;
    private readonly ToastService _toasts;
    private readonly DraftSpelling _spelling;
    private ComposeWindow? _window;
    private ITargetWindow? _target;
    private CancellationTokenSource? _picking;
    private bool _sending;

    private readonly AssistantService _assistant;
    private readonly WordToolsService _words;

    public ComposeController(SettingsStore settings, ReadingController reading, ToastService toasts, AssistantService assistant, WordToolsService words)
    {
        _settings = settings;
        _reading = reading;
        _toasts = toasts;
        _assistant = assistant;
        _words = words;
        _words.ReplaceRequested += word => _window?.ReplaceLookedUpWord(word);

        var dictionary = new UserDictionary(Path.Combine(SettingsStore.DefaultFolder(), "dictionary.txt"));
        dictionary.Load();
        _spelling = new DraftSpelling(_platform.CreateSpellChecker("en-GB"), dictionary);
    }

    /// <summary>True when Windows has an English (UK) checker, for the settings page.</summary>
    public bool SpellingAvailable => _spelling.IsAvailable;

    /// <summary>
    /// Opens Compose, sending to whatever window is in front at this moment.
    /// Called from the hotkey before Compose takes focus, so the window the
    /// user was working in is the one remembered. From the tray the front
    /// window is the taskbar, which is skipped, and the last target stays.
    /// </summary>
    public void Open()
    {
        var candidate = _platform.Windows.Target(_platform.Windows.Foreground);
        if (candidate is not null)
        {
            _target = candidate;
        }
        else if (_target is { IsAlive: false })
        {
            _target = null;
        }

        var window = EnsureWindow();
        window.SetTarget(_target);
        window.SetEditingMode(false);

        if (!window.IsVisible)
        {
            Place(window);
            window.Show();
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
        window.FocusEditor();
    }

    /// <summary>
    /// Edit on the pill: opens Compose on the selected text, sending back to
    /// the window it came from. The main button then reads "Put it back".
    /// Any unsent draft is replaced through the editor's undo, so Ctrl+Z
    /// brings it back.
    /// </summary>
    public void OpenWith(string text, ITargetWindow? source)
    {
        if (source is not null)
        {
            _target = source;
        }

        var window = EnsureWindow();
        window.SetTarget(_target);
        window.LoadForEditing(text);
        window.SetEditingMode(true);

        if (!window.IsVisible)
        {
            Place(window);
            window.Show();
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
        window.FocusEditor();
    }

    /// <summary>Opens Compose and runs an AI action on the draft, for the developer switches.</summary>
    public void OpenAndRun(Core.Ai.AssistantAction action)
    {
        Open();
        _window?.RunAction(action);
    }

    public void Dispose()
    {
        _picking?.Cancel();
        if (_window is not null)
        {
            _window.HideKeepingDraft();
            _window.Close();
            _window = null;
        }
    }

    private ComposeWindow EnsureWindow()
    {
        if (_window is not null)
        {
            return _window;
        }

        var window = new ComposeWindow(_spelling, _assistant, _settings);
        window.Text = _settings.Current.ComposeDraft ?? string.Empty;
        window.ReadBackRequested += ReadBack;
        window.LookupRequested += word =>
        {
            var (x, y) = _platform.Pointer.Position;
            _words.Show(word, new PixelPoint(x, y));
        };
        window.CopyRequested += text => Copy(text, announce: true);
        window.SendRequested += text => _ = SendAsync(text);
        window.UseThisWindowRequested += () => _ = PickWindowAsync();
        window.DraftChanged += text =>
        {
            _settings.Update(s => s.ComposeDraft = string.IsNullOrEmpty(text) ? null : text);
            window.ShowDraftSaved();
        };
        window.PlacementChanged += (position, size) => _settings.Update(s =>
        {
            s.ComposePlacement = new WindowPlacement(position.X, position.Y);
            s.ComposeWidth = size.Width;
            s.ComposeHeight = size.Height;
        });
        _window = window;
        return window;
    }

    /// <summary>Where it was last left, if that is still on a screen; otherwise the middle of the mouse's screen.</summary>
    private void Place(ComposeWindow window)
    {
        var settings = _settings.Current;
        if (settings.ComposeWidth is { } w && settings.ComposeHeight is { } h && w >= window.MinWidth && h >= window.MinHeight)
        {
            window.Width = w;
            window.Height = h;
        }

        var (x, y) = _platform.Pointer.Position;
        var mouseScreen = window.Screens.ScreenFromPoint(new PixelPoint(x, y)) ?? window.Screens.Primary;

        if (settings.ComposePlacement is { } saved)
        {
            var point = new PixelPoint(saved.X, saved.Y);
            var screen = window.Screens.ScreenFromPoint(point);
            if (screen is not null)
            {
                window.Position = point;
                return;
            }
        }

        if (mouseScreen is not null)
        {
            var area = mouseScreen.WorkingArea;
            var size = PixelSize.FromSize(new Size(window.Width, window.Height), mouseScreen.Scaling);
            window.Position = new PixelPoint(
                Math.Max(area.X, area.X + (area.Width - size.Width) / 2),
                Math.Max(area.Y, area.Y + (area.Height - size.Height) / 2));
        }
    }

    private void ReadBack(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _toasts.Info("Nothing to read yet");
            return;
        }

        _reading.Read(text);
    }

    private void Copy(string text, bool announce)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _toasts.Info("Nothing to copy yet");
            return;
        }

        if (_platform.Clipboard.TrySetText(text))
        {
            if (announce)
            {
                _toasts.Success("Copied. Paste it wherever you like.");
            }
        }
        else
        {
            _toasts.Error("The clipboard was busy. Try again.");
        }
    }

    /// <summary>Pastes into the target. Never presses Enter. Every failure is a toast with Copy as the way forward.</summary>
    private async Task SendAsync(string text)
    {
        if (_sending || _window is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            _toasts.Info("Nothing to send yet");
            return;
        }

        if (_target is null)
        {
            _toasts.Error("Choose a window to send to first.", "Use this window", () => _ = PickWindowAsync());
            return;
        }

        var name = _target.AppName;
        _sending = true;
        try
        {
            var outcome = await _target.PasteAsync(text, CancellationToken.None);
            switch (outcome)
            {
                case PasteOutcome.Pasted:
                    var editing = _window.IsEditing;
                    _window.Text = string.Empty;
                    _window.SetEditingMode(false);
                    _settings.Update(s => s.ComposeDraft = null);
                    _window.HideKeepingDraft();
                    _toasts.Success(editing ? $"Put back in {name}." : $"Sent to {name}. Press Enter there when you're ready.");
                    break;
                case PasteOutcome.WindowGone:
                    _target = null;
                    _window.SetTarget(null);
                    _toasts.Error($"{name} has closed, so nothing was sent.", "Copy instead", () => Copy(text, announce: true));
                    break;
                case PasteOutcome.Elevated:
                    _toasts.Error($"{name} runs as administrator, so keys can't be sent to it.", "Copy instead", () => Copy(text, announce: true));
                    break;
                case PasteOutcome.CouldNotFocus:
                    _toasts.Error($"Couldn't bring {name} to the front, so nothing was sent.", "Copy instead", () => Copy(text, announce: true));
                    break;
                case PasteOutcome.ClipboardBusy:
                    _toasts.Error("The clipboard was busy, so nothing was sent. Try again.", "Copy instead", () => Copy(text, announce: true));
                    break;
            }
        }
        catch (Exception ex)
        {
            _toasts.Error($"Couldn't send: {ex.Message}", "Copy instead", () => Copy(text, announce: true));
        }
        finally
        {
            _sending = false;
        }
    }

    /// <summary>
    /// "Use this window": wait for the user to click some other window, take
    /// it as the target, and bring Compose back. Gives up quietly after a while.
    /// </summary>
    private async Task PickWindowAsync()
    {
        if (_window is null || _picking is not null)
        {
            return;
        }

        _picking = new CancellationTokenSource(PickTimeout);
        _window.ShowPicking(true);
        try
        {
            var startedOn = _platform.Windows.Foreground;
            while (!_picking.IsCancellationRequested)
            {
                await Task.Delay(100, _picking.Token);
                var front = _platform.Windows.Foreground;
                if (front == startedOn)
                {
                    continue;
                }

                var candidate = _platform.Windows.Target(front);
                if (candidate is null)
                {
                    continue;
                }

                _target = candidate;
                _window.SetTarget(_target);
                _window.Activate();
                _toasts.Info($"Now sending to {candidate.AppName}");
                return;
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _picking.Dispose();
            _picking = null;
            Dispatcher.UIThread.Post(() =>
            {
                _window?.ShowPicking(false);
                _window?.SetTarget(_target);
            });
        }
    }
}
