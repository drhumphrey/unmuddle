using Avalonia;
using Avalonia.Threading;
using Helpers.App.Overlays;
using Helpers.Core.Capture;
using Helpers.Core.Input;
using Helpers.Core.Settings;
using Helpers.Windows;

namespace Helpers.App.Services;

/// <summary>
/// Shows the Read button after a mouse selection, following the brief's rules:
/// not on frames or drags between windows, not over our own windows, not in
/// excluded apps, not while paused. Hides on any other input or after a while.
/// </summary>
public sealed class ReadButtonService : IDisposable
{
    private readonly SelectionGestureDetector _detector = new();
    private readonly InputMonitor _input;
    private readonly SettingsStore _settings;
    private readonly ReadingController _reading;
    private readonly ISelectionSource _selection;
    private readonly ToastService _toasts;
    private readonly ComposeController _compose;
    private readonly DispatcherTimer _showTimer;
    private PillWindow? _pill;
    private nint _pressedWindow;
    private nint _pendingWindow;
    private GesturePoint _pressedPoint;
    private GesturePoint _pendingPoint;
    private PointerShape _pressedPointer;
    private DateTime? _pausedUntil;

    /// <summary>Key presses closer together than this, after the shortcut, are the shortcut still being held.</summary>
    private const int ShortcutKeysSettleMs = 350;
    private long? _ignoreKeysSince;

    public ReadButtonService(InputMonitor input, SettingsStore settings, ReadingController reading, ISelectionSource selection, ToastService toasts, ComposeController compose)
    {
        _input = input;
        _settings = settings;
        _reading = reading;
        _selection = selection;
        _toasts = toasts;
        _compose = compose;

        _showTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(settings.Current.ReadButtonDelayMs), DispatcherPriority.Background, (_, _) => ShowPill());
        _detector.SelectionMade += OnSelectionMade;
        _detector.Dismissed += () =>
        {
            _showTimer.Stop();
            _pill?.HidePill();
        };

        input.MouseDown += OnMouseDown;
        input.MouseUp += OnMouseUp;
        input.Wheel += () => _detector.Wheel();
        input.KeyPressed += OnKeyPressed;
    }

    /// <summary>
    /// Shows the button at the mouse because the user asked for it with the
    /// shortcut. Read, Edit and Paste then act on the window in front.
    /// </summary>
    public void ShowOnRequest()
    {
        var (x, y) = CursorPosition.Get();
        _pendingPoint = new GesturePoint(x, y);
        _pendingWindow = ForegroundWindow.Handle;

        // The shortcut's own keys are still down, and repeat while held. They mustn't hide what they just showed.
        _ignoreKeysSince = Environment.TickCount64;
        ShowPill(TimeSpan.FromSeconds(6));
    }

    private void OnKeyPressed()
    {
        if (_ignoreKeysSince is { } since)
        {
            var now = Environment.TickCount64;
            if (now - since < ShortcutKeysSettleMs)
            {
                _ignoreKeysSince = now;
                return;
            }

            _ignoreKeysSince = null;
        }

        _detector.KeyPressed();
    }

    public bool IsPaused => _pausedUntil is { } until && until > DateTime.UtcNow;

    /// <summary>Stops the button appearing for a while, for screen sharing or presenting.</summary>
    public void PauseFor(TimeSpan span)
    {
        _pausedUntil = DateTime.UtcNow + span;
        _pill?.HidePill();
    }

    public void Resume() => _pausedUntil = null;

    public void Dispose()
    {
        _showTimer.Stop();
        _pill?.Close();
    }

    private void OnMouseDown(PointerButton button, GesturePoint point, long time, bool ours)
    {
        if (ours)
        {
            // A click on the pill or the player must neither hide the pill nor start a gesture.
            _detector.CancelPress();
            return;
        }

        _pressedWindow = WindowAtPoint.TopLevelHandle(point.X, point.Y);
        _pressedPoint = point;
        _pressedPointer = CursorShape.Current();
        _detector.MouseDown(button, point, time);
    }

    private void OnMouseUp(PointerButton button, GesturePoint point, long time, bool ours)
    {
        if (ours)
        {
            return;
        }

        _detector.MouseUp(button, point, time);
    }

    private void OnSelectionMade(GesturePoint point)
    {
        if (!_settings.Current.ReadButtonAfterSelection || IsPaused)
        {
            return;
        }

        // The ordinary arrow at both ends: opening a file, dragging one, or a box around several. Not text.
        if (!PointerShapes.CouldBeTextSelection(_pressedPointer, CursorShape.Current()))
        {
            return;
        }

        // Released over a different window than pressed: a window or file drag, not a selection.
        var releasedWindow = WindowAtPoint.TopLevelHandle(point.X, point.Y);
        if (releasedWindow == 0 || releasedWindow != _pressedWindow)
        {
            return;
        }

        // Pressed on a title bar, border or scrollbar: a drag, not a selection.
        if (WindowAtPoint.IsOnFrame(WindowAtPoint.Handle(_pressedPoint.X, _pressedPoint.Y), _pressedPoint.X, _pressedPoint.Y))
        {
            return;
        }

        // Dragging out a snip looks just like selecting text; never offer to read a screenshot.
        var process = WindowAtPoint.ProcessName(releasedWindow);
        if (KnownApps.IsScreenCaptureTool(process)
            || _settings.Current.ExcludedApps.Any(app => string.Equals(app, process, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _pendingPoint = point;
        _pendingWindow = releasedWindow;
        _showTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(0, _settings.Current.ReadButtonDelayMs));
        _showTimer.Stop();
        _showTimer.Start();
    }

    private void ShowPill() => ShowPill(TimeSpan.FromSeconds(3));

    private void ShowPill(TimeSpan life)
    {
        _showTimer.Stop();
        _pill ??= CreatePill();
        var canPaste = !string.IsNullOrEmpty(ClipboardText.TryGet());
        _pill.ShowAt(new PixelPoint(_pendingPoint.X, _pendingPoint.Y), life, canPaste);
    }

    /// <summary>Shows the pill at the mouse as if a selection had just been made. A dev switch, for looking at it.</summary>
    public void ShowForPreview()
    {
        var (x, y) = CursorPosition.Get();
        _pendingPoint = new GesturePoint(x, y);
        _pendingWindow = WindowAtPoint.TopLevelHandle(x, y);
        ShowPill();
    }

    private PillWindow CreatePill()
    {
        var pill = new PillWindow();
        pill.ReadRequested += () => _ = ReadAsync();
        pill.EditRequested += () => _ = EditAsync();
        pill.PasteRequested += () => _ = PasteAsync();
        pill.Closed += (_, _) => _pill = null;
        return pill;
    }

    private async Task ReadAsync()
    {
        try
        {
            await _reading.ReadSelectionOrStopAsync(_selection);
        }
        catch (Exception ex)
        {
            _toasts.Error($"Couldn't read that: {ex.Message}");
        }
    }

    /// <summary>Edit: grab the selection the same way Read does, then open it in Compose, sending back to its window.</summary>
    private async Task EditAsync()
    {
        var source = TargetWindow.From(_pendingWindow);
        SelectionResult result;
        try
        {
            result = await _selection.CaptureAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _toasts.Error($"Couldn't grab the text: {ex.Message}");
            return;
        }

        switch (result.Outcome)
        {
            case SelectionOutcome.Text:
                _compose.OpenWith(result.Text!, source);
                break;
            case SelectionOutcome.Nothing:
                _toasts.Info("Nothing selected to edit");
                break;
            case SelectionOutcome.Elevated:
                _toasts.Info("That window runs as administrator, so its text can't be read");
                break;
            case SelectionOutcome.Password:
                _toasts.Info("That's a password field, so nothing was taken");
                break;
            default:
                _toasts.Error($"Couldn't grab the text: {result.Message}");
                break;
        }
    }

    /// <summary>Paste: the clipboard over the selection, in the window the selection was made in.</summary>
    private async Task PasteAsync()
    {
        var target = TargetWindow.From(_pendingWindow);
        if (target is null)
        {
            _toasts.Info("That window has gone");
            return;
        }

        try
        {
            var outcome = await target.PasteClipboardAsync(CancellationToken.None);
            switch (outcome)
            {
                case PasteOutcome.Pasted:
                    _toasts.Success($"Pasted into {target.AppName}");
                    break;
                case PasteOutcome.Elevated:
                    _toasts.Error($"{target.AppName} runs as administrator, so keys can't be sent to it.");
                    break;
                case PasteOutcome.CouldNotFocus:
                    _toasts.Error($"Couldn't bring {target.AppName} to the front, so nothing was pasted.");
                    break;
                default:
                    _toasts.Info("That window has gone");
                    break;
            }
        }
        catch (Exception ex)
        {
            _toasts.Error($"Couldn't paste: {ex.Message}");
        }
    }
}
