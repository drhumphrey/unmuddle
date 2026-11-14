using System.Runtime.InteropServices;

namespace Helpers.Windows;

/// <summary>Reads plain text from the Windows clipboard without touching anything else on it.</summary>
public static class ClipboardText
{
    private const uint CF_UNICODETEXT = 13;

    /// <summary>The text on the clipboard, or null if there is none or it is busy.</summary>
    public static string? TryGet()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            if (OpenClipboard(0))
            {
                try
                {
                    var handle = GetClipboardData(CF_UNICODETEXT);
                    if (handle == 0)
                    {
                        return null;
                    }

                    var pointer = GlobalLock(handle);
                    if (pointer == 0)
                    {
                        return null;
                    }

                    try
                    {
                        return Marshal.PtrToStringUni(pointer);
                    }
                    finally
                    {
                        GlobalUnlock(handle);
                    }
                }
                finally
                {
                    CloseClipboard();
                }
            }

            Thread.Sleep(30);
        }

        return null;
    }

    /// <summary>
    /// Replaces the clipboard with plain text. Only Compose calls this, between
    /// a <see cref="ClipboardSnapshot"/> and its restore. Returns false if the
    /// clipboard was busy, in which case it is left as it was.
    /// </summary>
    public static bool TrySet(string text)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            if (OpenClipboard(0))
            {
                try
                {
                    var handle = GlobalAlloc(GMEM_MOVEABLE, (nuint)((text.Length + 1) * 2));
                    if (handle == 0)
                    {
                        return false;
                    }

                    var pointer = GlobalLock(handle);
                    if (pointer == 0)
                    {
                        GlobalFree(handle);
                        return false;
                    }

                    try
                    {
                        Marshal.Copy(text.ToCharArray(), 0, pointer, text.Length);
                        Marshal.WriteInt16(pointer, text.Length * 2, 0);
                    }
                    finally
                    {
                        GlobalUnlock(handle);
                    }

                    EmptyClipboard();
                    if (SetClipboardData(CF_UNICODETEXT, handle) == 0)
                    {
                        GlobalFree(handle);
                        return false;
                    }

                    return true;
                }
                finally
                {
                    CloseClipboard();
                }
            }

            Thread.Sleep(30);
        }

        return false;
    }

    /// <summary>A number Windows bumps every time the clipboard changes.</summary>
    public static uint SequenceNumber => GetClipboardSequenceNumber();

    private const uint GMEM_MOVEABLE = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(nint owner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint GetClipboardData(uint format);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetClipboardData(uint format, nint handle);

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GlobalAlloc(uint flags, nuint bytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GlobalFree(nint handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GlobalLock(nint handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(nint handle);
}

/// <summary>Raises an event whenever the clipboard changes, by polling its sequence number.</summary>
public sealed class ClipboardWatcher : Helpers.Core.Platform.IClipboardWatcher
{
    private readonly Timer _timer;
    private readonly object _sync = new();
    private uint _last;
    private bool _enabled;
    private int _suspended;

    public ClipboardWatcher()
    {
        _last = ClipboardText.SequenceNumber;
        _timer = new Timer(_ => Poll(), null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>Raised on a thread-pool thread. Marshal to the UI yourself.</summary>
    public event Action? Changed;

    public void Start()
    {
        lock (_sync)
        {
            _last = ClipboardText.SequenceNumber;
            _enabled = true;
            if (_suspended == 0)
            {
                _timer.Change(400, 400);
            }
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            _enabled = false;
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    /// <summary>
    /// Pauses watching while the clipboard is borrowed, for instance by selection
    /// capture. Whatever changes in the meantime, including the restore, is ignored.
    /// </summary>
    public void Suspend()
    {
        lock (_sync)
        {
            _suspended++;
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    public void Resume()
    {
        lock (_sync)
        {
            _suspended = Math.Max(0, _suspended - 1);
            if (_suspended == 0)
            {
                _last = ClipboardText.SequenceNumber;
                if (_enabled)
                {
                    _timer.Change(400, 400);
                }
            }
        }
    }

    public void Dispose() => _timer.Dispose();

    private void Poll()
    {
        uint now;
        lock (_sync)
        {
            if (!_enabled || _suspended > 0)
            {
                return;
            }

            now = ClipboardText.SequenceNumber;
            if (now == _last)
            {
                return;
            }

            _last = now;
        }

        Changed?.Invoke();
    }
}
