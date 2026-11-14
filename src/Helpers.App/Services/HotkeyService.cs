using Avalonia.Threading;
using Helpers.Core.Input;
using Helpers.Core.Platform;

namespace Helpers.App.Services;

/// <summary>
/// Owns the global shortcuts, one per name: "read" for the selection,
/// "compose" for the Compose window, "read-button" for the Read button.
/// Each re-registers when its setting changes.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private readonly IGlobalHotkeys _hotkeys;
    private readonly Dictionary<string, int> _ids = new(StringComparer.Ordinal);
    private readonly Dictionary<int, Action> _actions = new();
    private readonly object _sync = new();

    public HotkeyService(IGlobalHotkeys hotkeys)
    {
        _hotkeys = hotkeys;
        _hotkeys.Pressed += OnPressed;
    }

    /// <summary>
    /// Registers, or re-registers, one named shortcut. Off, missing or
    /// modifier-less gestures just unregister. Returns false if the system
    /// refused the combination, usually because another app owns it.
    /// </summary>
    public bool Apply(string name, HotkeyGesture? gesture, bool enabled, Action onPressed)
    {
        Unregister(name);
        if (!enabled || gesture is null || !gesture.HasModifier)
        {
            return true;
        }

        var id = _hotkeys.Register(gesture);
        if (id <= 0)
        {
            return false;
        }

        lock (_sync)
        {
            _ids[name] = id;
            _actions[id] = onPressed;
        }

        return true;
    }

    public void Dispose()
    {
        foreach (var name in _ids.Keys.ToArray())
        {
            Unregister(name);
        }

        _hotkeys.Pressed -= OnPressed;
    }

    private void OnPressed(int id)
    {
        Action? action;
        lock (_sync)
        {
            _actions.TryGetValue(id, out action);
        }

        if (action is not null)
        {
            Dispatcher.UIThread.Post(action);
        }
    }

    private void Unregister(string name)
    {
        int id;
        lock (_sync)
        {
            if (!_ids.Remove(name, out id))
            {
                return;
            }

            _actions.Remove(id);
        }

        _hotkeys.Unregister(id);
    }
}
