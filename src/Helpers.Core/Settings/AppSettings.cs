using Helpers.Core.Text;

namespace Helpers.Core.Settings;

public enum Vibe
{
    Neon,
    Custom,
    Calm,
}

public enum ThemeChoice
{
    FollowOS,
    Light,
    Dark,
}

/// <summary>Where a window was last left, in screen pixels.</summary>
public sealed record WindowPlacement(int X, int Y);

public enum ReadingFont
{
    System,
    Lexend,
    AtkinsonHyperlegible,
}

public enum ReadingTint
{
    None,
    Cream,
    Grey,
}

public enum MessageScreen
{
    /// <summary>Always the main monitor, like Windows' own notifications.</summary>
    Primary,

    /// <summary>The monitor holding the window that has focus.</summary>
    Focused,
}

/// <summary>Everything the user can change. Saved as JSON in the app's settings file.</summary>
public sealed class AppSettings
{
    public string VoiceId { get; set; } = "bm_george";

    /// <summary>0.5 to 2.0.</summary>
    public float Speed { get; set; } = 1.0f;

    /// <summary>0 to 1.</summary>
    public float Volume { get; set; } = 1.0f;

    /// <summary>The output device's name, or null for the OS default. Stored by name because device numbers change.</summary>
    public string? OutputDeviceName { get; set; }

    public Vibe Vibe { get; set; } = Vibe.Neon;

    public ThemeChoice Theme { get; set; } = ThemeChoice.FollowOS;

    /// <summary>Two or three hex colours for the Custom vibe's gradient.</summary>
    public List<string> GradientColours { get; set; } = ["#19E6FF", "#FF2FD1", "#FFE14D"];

    /// <summary>Let the gradient and highlight drift while reading.</summary>
    public bool AnimateWhileReading { get; set; } = true;

    /// <summary>How many flecks of colour drift around the player, 0 to 100. Calm ignores it.</summary>
    public int FleckDensity { get; set; } = 35;

    /// <summary>Scale for every overlay. 1.0 is the designed size.</summary>
    public double UiScale { get; set; } = 0.9;

    /// <summary>The reading view's last size, in device-independent pixels. Null means the default.</summary>
    public double? ReadingViewWidth { get; set; }

    public double? ReadingViewHeight { get; set; }

    /// <summary>Whether the player was left open in the reading view.</summary>
    public bool PlayerExpanded { get; set; }

    /// <summary>Read any new text that lands on the clipboard.</summary>
    public bool WatchClipboard { get; set; }

    /// <summary>Show the Read button after a mouse selection.</summary>
    public bool ReadButtonEnabled { get; set; } = true;

    /// <summary>How long after the selection the button appears. Leaves room for a third click.</summary>
    public int ReadButtonDelayMs { get; set; } = 180;

    /// <summary>Process names, without .exe, where the Read button must never appear.</summary>
    public List<string> ExcludedApps { get; set; } = ["1Password", "Bitwarden", "KeePass", "KeePassXC", "mstsc"];

    /// <summary>The read shortcut a new copy starts with. H for "hear it".</summary>
    public const string DefaultReadSelectionHotkey = "Ctrl+Alt+H";

    /// <summary>The default up to version 0.2.0. Other apps claim it too, the Claude desktop app among them.</summary>
    public const string PreviousReadSelectionHotkey = "Ctrl+Alt+Space";

    /// <summary>The global shortcut that reads the current selection, or stops reading. Text such as "Ctrl+Alt+H".</summary>
    public string ReadSelectionHotkey { get; set; } = DefaultReadSelectionHotkey;

    public bool ReadSelectionHotkeyEnabled { get; set; } = true;

    /// <summary>The global shortcut that opens Compose, sending to the window that had focus.</summary>
    public string ComposeHotkey { get; set; } = "Ctrl+Alt+C";

    public bool ComposeHotkeyEnabled { get; set; } = true;

    /// <summary>The unsent Compose draft, so closing the window loses nothing.</summary>
    public string? ComposeDraft { get; set; }

    public WindowPlacement? ComposePlacement { get; set; }

    public double? ComposeWidth { get; set; }

    public double? ComposeHeight { get; set; }

    /// <summary>Seconds after reading ends before the player hides. 0 keeps it open.</summary>
    public int PlayerHideAfterSeconds { get; set; } = 4;

    public WindowPlacement? PlayerPlacement { get; set; }

    /// <summary>Which monitor toasts appear on.</summary>
    public MessageScreen MessagesOn { get; set; } = MessageScreen.Primary;

    /// <summary>Folder holding the voice models. Null means the default under local app data.</summary>
    public string? ModelsFolder { get; set; }

    /// <summary>Minutes of silence before the voice is unloaded to free its memory. 0 keeps it loaded.</summary>
    public int UnloadVoiceAfterMinutes { get; set; } = 15;

    public ReadingSettings Reading { get; set; } = new();

    /// <summary>The AI helper: which provider, which model, the prompts, this month's spend. Never the key.</summary>
    public Helpers.Core.Ai.AiSettings Ai { get; set; } = new();

    /// <summary>The opt-in update check.</summary>
    public Helpers.Core.Updates.UpdateSettings Updates { get; set; } = new();

    /// <summary>Font for the user's own text: the reading view, the sentence bar, Compose.</summary>
    public ReadingFont ReadingFontChoice { get; set; } = ReadingFont.Lexend;

    /// <summary>Reading text size in device-independent pixels. 20 is the designed size.</summary>
    public double ReadingFontSize { get; set; } = 20;

    /// <summary>Line height as a multiple of the font size.</summary>
    public double ReadingLineSpacing { get; set; } = 1.6;

    public ReadingTint ReadingTintChoice { get; set; } = ReadingTint.None;

    /// <summary>Mirrors the Windows Run key so the setting shows correctly; the key is the truth.</summary>
    public bool StartWithWindows { get; set; }

    /// <summary>Set once the first-run screen has been seen.</summary>
    public bool FirstRunDone { get; set; }
}
