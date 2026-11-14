using Helpers.Core.Ai;
using Helpers.Core.Capture;
using Helpers.Core.Input;
using Helpers.Core.Speech;
using Helpers.Core.Spelling;

namespace Helpers.Core.Platform;

/// <summary>
/// Everything the app asks of the operating system, in one place. The shared
/// app talks only to this; <c>Helpers.Windows</c> answers for Windows, and a
/// <c>Helpers.Mac</c> will answer for macOS. A window belonging to another
/// program is passed around as an opaque <see cref="nint"/>: only the
/// platform that handed it out knows what it means.
/// </summary>
public interface IPlatform : IDisposable
{
    /// <summary>The system's secure store, for the API key.</summary>
    ISecretStore Secrets { get; }

    IClipboard Clipboard { get; }

    IPointer Pointer { get; }

    IDesktopWindows Windows { get; }

    IGlobalHotkeys Hotkeys { get; }

    IStartupRegistration Startup { get; }

    /// <summary>True when the user has asked the system for less animation.</summary>
    bool ReducedMotion { get; }

    /// <summary>True when this copy was put there by the installer, so it can update itself.</summary>
    bool IsInstalled { get; }

    IAudioOutput CreateAudioOutput();

    ISelectionSource CreateSelectionSource();

    ISpellChecker CreateSpellChecker(string languageTag);

    IClipboardWatcher CreateClipboardWatcher();

    /// <summary>The mouse and key watching behind the Read button. Not installed until asked.</summary>
    IInputEvents CreateInputEvents();

    /// <summary>
    /// Claims the right to be the only copy running. Returns null when another
    /// copy already holds it; that copy has then been told, and the caller should exit.
    /// </summary>
    ISingleInstance? TryClaimSingleInstance(string name);

    /// <summary>
    /// Carries what the system holds under the app's previous name, the saved
    /// key and the start-up entry, across to the current one. Safe at every start.
    /// </summary>
    void MoveFromPreviousName(string secretName);

    /// <summary>Makes one of the app's own native windows one that never takes focus when clicked.</summary>
    void MakeNonActivating(nint ownWindow);
}

public interface IClipboard
{
    /// <summary>The text on the clipboard, or null when there is none or it was busy.</summary>
    string? TryGetText();

    /// <summary>Puts text on the clipboard. Returns false if it was busy.</summary>
    bool TrySetText(string text);
}

/// <summary>Raises an event whenever the clipboard changes.</summary>
public interface IClipboardWatcher : IDisposable
{
    /// <summary>Raised on a background thread. Marshal to the UI yourself.</summary>
    event Action? Changed;

    void Start();

    void Stop();

    /// <summary>
    /// Pauses watching while the clipboard is borrowed, for instance by selection
    /// capture. Whatever changes in the meantime, including the restore, is ignored.
    /// </summary>
    void Suspend();

    void Resume();
}

public interface IPointer
{
    /// <summary>Where the mouse is, in screen pixels.</summary>
    (int X, int Y) Position { get; }

    /// <summary>What the mouse pointer looks like right now.</summary>
    PointerShape Shape { get; }
}

/// <summary>Questions about the windows on the desktop, most of them other programs'.</summary>
public interface IDesktopWindows
{
    /// <summary>The window in front, or 0.</summary>
    nint Foreground { get; }

    /// <summary>The middle of a window in screen pixels, or null if it has gone.</summary>
    (int X, int Y)? Centre(nint window);

    /// <summary>The innermost window under a screen point, or 0.</summary>
    nint At(int x, int y);

    /// <summary>The top-level window under a screen point, or 0.</summary>
    nint TopLevelAt(int x, int y);

    /// <summary>True when the window belongs to this app.</summary>
    bool IsOurs(nint window);

    /// <summary>True when the point is on the window's title bar, border or scrollbar, not its contents.</summary>
    bool IsOnFrame(nint window, int x, int y);

    /// <summary>The name of the program that owns the window, without ".exe".</summary>
    string ProcessName(nint window);

    /// <summary>
    /// The window as somewhere to send text, or null when it has gone or is
    /// this app, the taskbar or the desktop, none of which can be sent to.
    /// </summary>
    ITargetWindow? Target(nint window);
}

public interface IGlobalHotkeys
{
    /// <summary>Raised on a background thread with the id <see cref="Register"/> returned.</summary>
    event Action<int>? Pressed;

    /// <summary>
    /// Registers a system-wide shortcut. Returns its id, or -1 when the key has
    /// no meaning here or the system refused, usually because another app owns it.
    /// </summary>
    int Register(HotkeyGesture gesture);

    void Unregister(int id);
}

/// <summary>
/// Mouse and key events from anywhere on the desktop. Key events carry no key
/// code: the app only needs to know that a key was pressed, and never logs what.
/// </summary>
public interface IInputEvents : IDisposable
{
    /// <summary>Button, screen point and timestamp. Raised on a background thread; handlers must return at once.</summary>
    event Action<PointerButton, GesturePoint, long>? MouseDown;

    event Action<PointerButton, GesturePoint, long>? MouseUp;

    event Action? Wheel;

    event Action? KeyPressed;

    bool IsInstalled { get; }

    /// <summary>Starts watching. Returns false if the system refused.</summary>
    bool Install();
}

/// <summary>Starting the app when the user signs in.</summary>
public interface IStartupRegistration
{
    bool IsEnabled();

    /// <summary>Registers or removes this copy. Returns false if the system refused.</summary>
    bool SetEnabled(bool enabled);
}

/// <summary>This copy's claim to be the only one running.</summary>
public interface ISingleInstance : IDisposable
{
    /// <summary>Raised on a background thread when another copy was started and has stepped aside.</summary>
    event Action? StartedAgain;

    /// <summary>Starts listening for later copies. Call once, after subscribing to <see cref="StartedAgain"/>.</summary>
    void Listen();
}
