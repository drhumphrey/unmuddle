using Avalonia.Threading;
using Helpers.Core.Input;
using Helpers.Core.Platform;

namespace Helpers.App.Services;

/// <summary>
/// Owns the system's mouse and key events and re-raises them on the UI thread,
/// each tagged with whether the mouse was over one of our own windows.
/// The Read button and the quick menu both listen here.
/// </summary>
public sealed class InputMonitor : IDisposable
{
    private readonly IInputEvents _events;
    private readonly IDesktopWindows _windows;

    public InputMonitor(IInputEvents events, IDesktopWindows windows)
    {
        _events = events;
        _windows = windows;
        _events.MouseDown += (button, point, time) => Dispatcher.UIThread.Post(() => MouseDown?.Invoke(button, point, time, IsOurs(point)));
        _events.MouseUp += (button, point, time) => Dispatcher.UIThread.Post(() => MouseUp?.Invoke(button, point, time, IsOurs(point)));
        _events.Wheel += () => Dispatcher.UIThread.Post(() => Wheel?.Invoke());
        _events.KeyPressed += () => Dispatcher.UIThread.Post(() => KeyPressed?.Invoke());
    }

    /// <summary>Button, screen point, timestamp, and whether the point is over one of this app's windows.</summary>
    public event Action<PointerButton, GesturePoint, long, bool>? MouseDown;

    public event Action<PointerButton, GesturePoint, long, bool>? MouseUp;

    public event Action? Wheel;

    public event Action? KeyPressed;

    public bool IsInstalled => _events.IsInstalled;

    public bool Install() => _events.Install();

    public void Dispose() => _events.Dispose();

    private bool IsOurs(GesturePoint point) => _windows.IsOurs(_windows.At(point.X, point.Y));
}
