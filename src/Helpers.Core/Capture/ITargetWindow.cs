namespace Helpers.Core.Capture;

public enum PasteOutcome
{
    /// <summary>The text was pasted. The user presses Enter themselves.</summary>
    Pasted,

    /// <summary>The window has closed since it was chosen.</summary>
    WindowGone,

    /// <summary>The window runs as administrator and this app doesn't, so Windows blocks the keys.</summary>
    Elevated,

    /// <summary>The window could not be brought to the front, so nothing was sent.</summary>
    CouldNotFocus,

    /// <summary>The clipboard was busy; the text is unchanged and so is the clipboard.</summary>
    ClipboardBusy,
}

/// <summary>
/// Another program's window that Compose sends text to. The platform project
/// remembers the window, brings it to the front and pastes. It never presses
/// Enter, and it leaves the clipboard as it found it.
/// </summary>
public interface ITargetWindow
{
    /// <summary>A short name for the title bar, such as "Visual Studio Code".</summary>
    string AppName { get; }

    /// <summary>The window's own title, for a tooltip.</summary>
    string Title { get; }

    bool IsAlive { get; }

    Task<PasteOutcome> PasteAsync(string text, CancellationToken cancellationToken);

    /// <summary>
    /// Pastes whatever is on the clipboard already, over the selection in this
    /// window. The clipboard is the user's, so it is left alone.
    /// </summary>
    Task<PasteOutcome> PasteClipboardAsync(CancellationToken cancellationToken);
}
