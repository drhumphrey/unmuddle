namespace Helpers.Core.Input;

/// <summary>
/// A keyboard shortcut such as "Ctrl+Alt+Space", stored as text in settings.
/// Platform code maps the key name to a virtual key.
/// </summary>
public sealed record HotkeyGesture(bool Ctrl, bool Alt, bool Shift, bool Win, string Key)
{
    public static HotkeyGesture Default { get; } = new(true, true, false, false, "H");

    /// <summary>True when at least one modifier is held, which a global shortcut needs so it doesn't swallow plain typing.</summary>
    public bool HasModifier => Ctrl || Alt || Shift || Win;

    /// <summary>Parses "Ctrl+Alt+Space" and the like. Returns null for anything it can't read.</summary>
    public static HotkeyGesture? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var ctrl = false;
        var alt = false;
        var shift = false;
        var win = false;
        string? key = null;

        foreach (var raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    ctrl = true;
                    break;
                case "alt":
                    alt = true;
                    break;
                case "shift":
                    shift = true;
                    break;
                case "win":
                case "windows":
                case "cmd":
                    win = true;
                    break;
                default:
                    if (key is not null)
                    {
                        return null;
                    }

                    key = NormaliseKey(raw);
                    break;
            }
        }

        return key is null ? null : new HotkeyGesture(ctrl, alt, shift, win, key);
    }

    public override string ToString()
    {
        var parts = new List<string>(5);
        if (Ctrl) parts.Add("Ctrl");
        if (Alt) parts.Add("Alt");
        if (Shift) parts.Add("Shift");
        if (Win) parts.Add("Win");
        parts.Add(Key);
        return string.Join("+", parts);
    }

    private static string NormaliseKey(string key)
    {
        if (key.Length == 1)
        {
            return key.ToUpperInvariant();
        }

        return key.ToLowerInvariant() switch
        {
            "space" or "spacebar" => "Space",
            "ins" or "insert" => "Insert",
            "del" or "delete" => "Delete",
            "esc" or "escape" => "Escape",
            "enter" or "return" => "Enter",
            "tab" => "Tab",
            "home" => "Home",
            "end" => "End",
            "pageup" or "pgup" => "PageUp",
            "pagedown" or "pgdn" => "PageDown",
            "up" => "Up",
            "down" => "Down",
            "left" => "Left",
            "right" => "Right",
            "pause" => "Pause",
            "scrolllock" or "scroll" => "ScrollLock",
            "backspace" or "back" => "Backspace",
            _ => key.Length >= 2 && (key[0] is 'F' or 'f') && int.TryParse(key[1..], out var f) && f is >= 1 and <= 24
                ? $"F{f}"
                : key,
        };
    }
}
