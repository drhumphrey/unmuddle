using System.Runtime.InteropServices;
using Helpers.Core.Input;

namespace Helpers.Windows;

/// <summary>
/// Low-level mouse and keyboard hooks. The callbacks do nothing but read the
/// event and raise it; anything slow must happen elsewhere, or Windows will
/// silently remove the hook. Key events carry no key code: the app only needs
/// to know that a key was pressed, and never logs what.
/// </summary>
public sealed class InputHooks : Helpers.Core.Platform.IInputEvents
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WH_MOUSE_LL = 14;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_MBUTTONUP = 0x0208;
    private const int WM_MOUSEWHEEL = 0x020A;
    private const int WM_MOUSEHWHEEL = 0x020E;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    private readonly MessageWindow _thread;
    private readonly HookProc _mouseProc;
    private readonly HookProc _keyboardProc;
    private nint _mouseHook;
    private nint _keyboardHook;

    public InputHooks(MessageWindow thread)
    {
        _thread = thread;
        _mouseProc = MouseProc;
        _keyboardProc = KeyboardProc;
    }

    private delegate nint HookProc(int code, nint wParam, nint lParam);

    /// <summary>Raised on the hook thread. Handlers must return at once.</summary>
    public event Action<PointerButton, GesturePoint, long>? MouseDown;

    public event Action<PointerButton, GesturePoint, long>? MouseUp;

    public event Action? Wheel;

    public event Action? KeyPressed;

    public bool IsInstalled => _mouseHook != 0;

    /// <summary>Installs both hooks on the message thread. Returns false if Windows refused.</summary>
    public bool Install()
    {
        if (_mouseHook != 0)
        {
            return true;
        }

        var ok = false;
        _thread.Invoke(() =>
        {
            var module = GetModuleHandle(null);
            _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, module, 0);
            _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, module, 0);
            ok = _mouseHook != 0;
        });

        return ok;
    }

    public void Uninstall()
    {
        _thread.Invoke(() =>
        {
            if (_mouseHook != 0)
            {
                UnhookWindowsHookEx(_mouseHook);
                _mouseHook = 0;
            }

            if (_keyboardHook != 0)
            {
                UnhookWindowsHookEx(_keyboardHook);
                _keyboardHook = 0;
            }
        });
    }

    public void Dispose()
    {
        try
        {
            Uninstall();
        }
        catch (InvalidOperationException)
        {
            // The message thread has already gone; the hooks died with it.
        }
    }

    private nint MouseProc(int code, nint wParam, nint lParam)
    {
        if (code >= 0)
        {
            var info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            var point = new GesturePoint(info.pt.X, info.pt.Y);
            var time = (long)info.time;
            switch ((int)wParam)
            {
                case WM_LBUTTONDOWN:
                    MouseDown?.Invoke(PointerButton.Left, point, time);
                    break;
                case WM_LBUTTONUP:
                    MouseUp?.Invoke(PointerButton.Left, point, time);
                    break;
                case WM_RBUTTONDOWN:
                    MouseDown?.Invoke(PointerButton.Right, point, time);
                    break;
                case WM_RBUTTONUP:
                    MouseUp?.Invoke(PointerButton.Right, point, time);
                    break;
                case WM_MBUTTONDOWN:
                    MouseDown?.Invoke(PointerButton.Middle, point, time);
                    break;
                case WM_MBUTTONUP:
                    MouseUp?.Invoke(PointerButton.Middle, point, time);
                    break;
                case WM_MOUSEWHEEL:
                case WM_MOUSEHWHEEL:
                    Wheel?.Invoke();
                    break;
            }
        }

        return CallNextHookEx(_mouseHook, code, wParam, lParam);
    }

    private nint KeyboardProc(int code, nint wParam, nint lParam)
    {
        if (code >= 0 && (int)wParam is WM_KEYDOWN or WM_SYSKEYDOWN)
        {
            KeyPressed?.Invoke();
        }

        return CallNextHookEx(_keyboardHook, code, wParam, lParam);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public nint dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int hookType, HookProc proc, nint module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(nint hook);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? moduleName);
}
