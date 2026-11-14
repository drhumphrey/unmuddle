using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.Win32;
using Helpers.App.Services;

namespace Helpers.App.Overlays;

/// <summary>
/// The base for every surface that must never take focus: the pill, the
/// player, toasts and the AI result card. Borderless, topmost, transparent
/// outside its card, kept off the taskbar, told by Windows not to activate
/// when clicked, always kept inside the screen it is on, and scaled by the
/// user's size setting through a LayoutTransformControl named "Scaler".
/// </summary>
public class OverlayWindow : Window
{
    public OverlayWindow()
    {
        WindowDecorations = WindowDecorations.None;
        ShowActivated = false;
        ShowInTaskbar = false;
        Topmost = true;
        CanResize = false;
        Background = Brushes.Transparent;

        // Plain transparency, not acrylic: Windows applies acrylic blur to the whole
        // window rectangle, which turns the shadow margin into a frosted box.
        TransparencyLevelHint =
        [
            WindowTransparencyLevel.Transparent,
            WindowTransparencyLevel.None,
        ];

        if (OperatingSystem.IsWindows())
        {
            Win32Properties.AddWndProcHookCallback(this, RefuseActivation);
        }

        SizeChanged += (_, _) => KeepOnScreen();
        ScalingChanged += (_, _) => Dispatcher.UIThread.Post(OnScalingChanged, DispatcherPriority.Background);
        UiScale.Changed += ApplyScale;
        Closed += (_, _) => UiScale.Changed -= ApplyScale;
    }

    /// <summary>The screen the mouse is on, falling back to the primary one.</summary>
    public Screen? TargetScreen()
    {
        var (x, y) = AppPlatform.Current.Pointer.Position;
        return Screens.ScreenFromPoint(new PixelPoint(x, y)) ?? Screens.Primary;
    }

    /// <summary>
    /// The screen the user is most likely looking at: the one holding the window
    /// that has focus, falling back to the mouse's screen. Messages go here.
    /// </summary>
    public Screen? AttentionScreen()
    {
        if (AppPlatform.Current.Windows is var windows && windows.Centre(windows.Foreground) is var (x, y))
        {
            var screen = Screens.ScreenFromPoint(new PixelPoint(x, y));
            if (screen is not null)
            {
                return screen;
            }
        }

        return TargetScreen();
    }

    /// <summary>
    /// The screen this window belongs to: the one holding its top-left corner.
    /// Growing to the right or down never changes it, so a window that grows
    /// across a monitor edge is pulled back rather than pushed over.
    /// </summary>
    public Screen? HomeScreen() =>
        Screens.ScreenFromPoint(Position) ?? Screens.ScreenFromWindow(this) ?? Screens.Primary;

    /// <summary>The window's size in screen pixels on a given screen.</summary>
    public PixelSize PixelSizeOn(Screen screen) => PixelSize.FromSize(ClientSize, screen.Scaling);

    /// <summary>
    /// Places the window at the bottom-right of a screen's working area, with a
    /// margin. Moving between monitors with different scaling makes Windows
    /// resize the window after the move, so the placement is settled twice.
    /// </summary>
    public void PlaceBottomRight(Screen screen, int marginRight, int marginBottom)
    {
        var area = screen.WorkingArea;
        var size = PixelSizeOn(screen);
        Position = new PixelPoint(area.Right - size.Width - marginRight, area.Bottom - size.Height - marginBottom);

        Dispatcher.UIThread.Post(() =>
        {
            if (!IsVisible)
            {
                return;
            }

            var actual = PixelSize.FromSize(ClientSize, RenderScaling);
            var settled = new PixelPoint(area.Right - actual.Width - marginRight, area.Bottom - actual.Height - marginBottom);
            if (settled != Position)
            {
                Position = settled;
            }
        }, DispatcherPriority.Background);
    }

    /// <summary>Called when the window lands on a monitor with different scaling. Overlays re-place themselves.</summary>
    protected virtual void OnScalingChanged() => KeepOnScreen();

    /// <summary>Nudges the window back inside its home screen's working area if any edge has drifted off.</summary>
    public void KeepOnScreen()
    {
        if (!IsVisible || HomeScreen() is not { } screen)
        {
            return;
        }

        var area = screen.WorkingArea;
        var size = PixelSizeOn(screen);
        var x = Math.Clamp(Position.X, area.X, Math.Max(area.X, area.Right - size.Width));
        var y = Math.Clamp(Position.Y, area.Y, Math.Max(area.Y, area.Bottom - size.Height));
        if (x != Position.X || y != Position.Y)
        {
            Position = new PixelPoint(x, y);
        }
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (TryGetPlatformHandle() is { } handle)
        {
            AppPlatform.Current.MakeNonActivating(handle.Handle);
        }

        ApplyScale(UiScale.Current);
    }

    private void ApplyScale(double scale)
    {
        if (this.FindControl<LayoutTransformControl>("Scaler") is { } scaler)
        {
            scaler.LayoutTransform = new ScaleTransform(scale, scale);
        }
    }

    /// <summary>The message Windows sends before activating a window on a mouse click.</summary>
    private const uint WM_MOUSEACTIVATE = 0x0021;

    /// <summary>The reply to it that means "handle the click but don't activate me".</summary>
    private const nint MA_NOACTIVATE = 3;

    private static nint RefuseActivation(nint hWnd, uint msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WM_MOUSEACTIVATE)
        {
            handled = true;
            return MA_NOACTIVATE;
        }

        return 0;
    }
}
