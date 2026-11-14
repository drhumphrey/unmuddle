using Helpers.Core.Ai;
using Helpers.Core.Capture;
using Helpers.Core.Input;
using Helpers.Core.Platform;
using Helpers.Core.Speech;
using Helpers.Core.Spelling;
using Helpers.Windows.Spelling;

namespace Helpers.Windows;

/// <summary>
/// Windows' answers to everything the shared app asks of the operating system.
/// It adds nothing of its own: each member hands on to the class in this
/// project that does the work.
/// </summary>
public sealed class WindowsPlatform : IPlatform
{
    private readonly object _sync = new();
    private MessageWindow? _thread;
    private WindowsHotkeys? _hotkeys;

    public ISecretStore Secrets { get; } = new CredentialStore();

    public IClipboard Clipboard { get; } = new WindowsClipboard();

    public IPointer Pointer { get; } = new WindowsPointer();

    public IDesktopWindows Windows { get; } = new WindowsDesktopWindows();

    public IStartupRegistration Startup { get; } = new WindowsStartup();

    public IGlobalHotkeys Hotkeys
    {
        get
        {
            lock (_sync)
            {
                return _hotkeys ??= new WindowsHotkeys(MessageThread());
            }
        }
    }

    public bool ReducedMotion => SystemPreferences.ReducedMotion;

    public bool IsInstalled => InstallerRegistration.IsInstalled();

    public IAudioOutput CreateAudioOutput() => new NAudioOutput();

    public ISelectionSource CreateSelectionSource() => new SelectionCapture();

    public ISpellChecker CreateSpellChecker(string languageTag) => new WindowsSpellChecker(languageTag);

    public IClipboardWatcher CreateClipboardWatcher() => new ClipboardWatcher();

    /// <summary>The mouse and keyboard hooks share the hotkeys' message thread.</summary>
    public IInputEvents CreateInputEvents()
    {
        lock (_sync)
        {
            return new InputHooks(MessageThread());
        }
    }

    public ISingleInstance? TryClaimSingleInstance(string name) => SingleInstance.TryClaim(name);

    public void MoveFromPreviousName(string secretName)
    {
        CredentialStore.MovePrevious(secretName);
        StartupRegistration.MovePrevious();
    }

    public void MakeNonActivating(nint ownWindow) => NonActivatingWindows.Apply(ownWindow);

    public void Dispose()
    {
        lock (_sync)
        {
            _thread?.Dispose();
            _thread = null;
            _hotkeys = null;
        }
    }

    /// <summary>One hidden window, on its own thread, serves the hotkeys and the hooks. Call with the lock held.</summary>
    private MessageWindow MessageThread()
    {
        if (_thread is null)
        {
            _thread = new MessageWindow();
            _thread.Start();
        }

        return _thread;
    }

    private sealed class WindowsClipboard : IClipboard
    {
        public string? TryGetText() => ClipboardText.TryGet();

        public bool TrySetText(string text) => ClipboardText.TrySet(text);
    }

    private sealed class WindowsPointer : IPointer
    {
        public (int X, int Y) Position => CursorPosition.Get();

        public PointerShape Shape => CursorShape.Current();
    }

    private sealed class WindowsDesktopWindows : IDesktopWindows
    {
        public nint Foreground => ForegroundWindow.Handle;

        public (int X, int Y)? Centre(nint window) => ForegroundWindow.Centre(window);

        public nint At(int x, int y) => WindowAtPoint.Handle(x, y);

        public nint TopLevelAt(int x, int y) => WindowAtPoint.TopLevelHandle(x, y);

        public bool IsOurs(nint window) => WindowAtPoint.BelongsToThisProcess(window);

        public bool IsOnFrame(nint window, int x, int y) => WindowAtPoint.IsOnFrame(window, x, y);

        public string ProcessName(nint window) => WindowAtPoint.ProcessName(window);

        public ITargetWindow? Target(nint window) => TargetWindow.From(window);
    }

    private sealed class WindowsStartup : IStartupRegistration
    {
        public bool IsEnabled() => StartupRegistration.IsEnabled();

        public bool SetEnabled(bool enabled) => StartupRegistration.SetEnabled(enabled);
    }

    /// <summary>Global shortcuts through RegisterHotKey on the message window.</summary>
    private sealed class WindowsHotkeys : IGlobalHotkeys
    {
        private readonly MessageWindow _window;

        public WindowsHotkeys(MessageWindow window)
        {
            _window = window;
            _window.HotkeyPressed += id => Pressed?.Invoke(id);
        }

        public event Action<int>? Pressed;

        public int Register(HotkeyGesture gesture)
        {
            var key = VirtualKeys.From(gesture.Key);
            return key == 0 ? -1 : _window.RegisterHotkey(gesture.Ctrl, gesture.Alt, gesture.Shift, gesture.Win, key);
        }

        public void Unregister(int id) => _window.UnregisterHotkey(id);
    }
}
