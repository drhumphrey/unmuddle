using System.Runtime.InteropServices;
using Helpers.Core.Input;

namespace Helpers.Windows;

/// <summary>
/// What the mouse pointer looks like right now, whichever app set it. Windows'
/// standard pointers are shared between every program, so comparing handles
/// tells a text I-beam from the ordinary arrow without asking the other app
/// anything.
/// </summary>
public static class CursorShape
{
    private const int IDC_ARROW = 32512;
    private const int IDC_IBEAM = 32513;
    private const int IDC_WAIT = 32514;
    private const int IDC_UPARROW = 32516;
    private const int IDC_SIZENWSE = 32642;
    private const int IDC_SIZENESW = 32643;
    private const int IDC_SIZEWE = 32644;
    private const int IDC_SIZENS = 32645;
    private const int IDC_SIZEALL = 32646;
    private const int IDC_NO = 32648;
    private const int IDC_HAND = 32649;
    private const int IDC_APPSTARTING = 32650;
    private const int IDC_HELP = 32651;

    private static readonly nint Text = LoadCursor(0, IDC_IBEAM);

    private static readonly HashSet<nint> Plain = new[]
    {
        IDC_ARROW, IDC_WAIT, IDC_UPARROW, IDC_SIZENWSE, IDC_SIZENESW, IDC_SIZEWE, IDC_SIZENS, IDC_SIZEALL,
        IDC_NO, IDC_HAND, IDC_APPSTARTING, IDC_HELP,
    }.Select(id => LoadCursor(0, id)).Where(handle => handle != 0).ToHashSet();

    /// <summary>
    /// Text for the I-beam, Plain for the arrow, the hand and the other standard
    /// pointers, Other for a pointer an app drew itself or a hidden one.
    /// </summary>
    public static PointerShape Current()
    {
        var info = new CURSORINFO { cbSize = Marshal.SizeOf<CURSORINFO>() };
        if (!GetCursorInfo(ref info) || info.hCursor == 0)
        {
            return PointerShape.Other;
        }

        if (info.hCursor == Text)
        {
            return PointerShape.Text;
        }

        return Plain.Contains(info.hCursor) ? PointerShape.Plain : PointerShape.Other;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CURSORINFO
    {
        public int cbSize;
        public int flags;
        public nint hCursor;
        public POINT ptScreenPos;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorInfo(ref CURSORINFO info);

    [DllImport("user32.dll")]
    private static extern nint LoadCursor(nint instance, nint cursorName);
}
