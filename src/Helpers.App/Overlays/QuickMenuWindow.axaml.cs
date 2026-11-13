using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Threading;

namespace Helpers.App.Overlays;

/// <summary>One entry in the quick menu. A null action is a separator.</summary>
public sealed record QuickMenuItem(string Text, Action? Action, Func<bool>? IsChecked = null)
{
    /// <summary>When this says true, the entry carries a small play mark: the voice is reading right now.</summary>
    public Func<bool>? IsPlaying { get; init; }

    public static QuickMenuItem Separator { get; } = new(string.Empty, null);
}

/// <summary>
/// The styled menu that opens on a left-click of the tray icon. The native
/// right-click menu stays for anyone who expects it; this one matches the vibe.
/// Closes on a choice, a click anywhere else, a key press, or after a while.
/// </summary>
public partial class QuickMenuWindow : OverlayWindow
{
    private readonly DispatcherTimer _lifeTimer;
    private IReadOnlyList<QuickMenuItem> _items = [];

    public QuickMenuWindow()
    {
        InitializeComponent();
        _lifeTimer = new DispatcherTimer(TimeSpan.FromSeconds(10), DispatcherPriority.Background, (_, _) => HideMenu());
    }

    public void SetItems(IReadOnlyList<QuickMenuItem> items)
    {
        _items = items;
        Rebuild();
    }

    /// <summary>Shows the menu with its bottom-left corner near a screen point, kept on that screen.</summary>
    public void ShowNear(PixelPoint point)
    {
        Rebuild();
        if (!IsVisible)
        {
            Show();
        }

        var screen = Screens.ScreenFromPoint(point) ?? Screens.Primary;
        if (screen is not null)
        {
            var area = screen.WorkingArea;
            var size = PixelSize.FromSize(ClientSize, screen.Scaling);
            var x = Math.Clamp(point.X - size.Width / 2, area.X, Math.Max(area.X, area.Right - size.Width));
            var y = Math.Clamp(point.Y - size.Height, area.Y, Math.Max(area.Y, area.Bottom - size.Height));
            Position = new PixelPoint(x, y);
        }

        _lifeTimer.Stop();
        _lifeTimer.Start();
    }

    public void HideMenu()
    {
        _lifeTimer.Stop();
        if (IsVisible)
        {
            Hide();
        }
    }

    private void Rebuild()
    {
        Items.Children.Clear();
        foreach (var item in _items)
        {
            if (item.Action is null)
            {
                Items.Children.Add(new Rectangle { Classes = { "sep" } });
                continue;
            }

            var content = new DockPanel();
            if (item.IsChecked is not null)
            {
                var tick = new PathIcon
                {
                    Classes = { "tick" },
                    Data = Avalonia.Media.Geometry.Parse("M9 16.2 4.8 12l-1.4 1.4L9 19 21 7l-1.4-1.4z"),
                    IsVisible = item.IsChecked(),
                    Margin = new Thickness(8, 0, 0, 0),
                };
                DockPanel.SetDock(tick, Dock.Right);
                content.Children.Add(tick);
            }

            if (item.IsPlaying is not null && item.IsPlaying())
            {
                var play = new PathIcon
                {
                    Classes = { "tick" },
                    Data = Avalonia.Media.Geometry.Parse("M8 5v14l11-7z"),
                    Margin = new Thickness(8, 0, 0, 0),
                };
                ToolTip.SetTip(play, "Reading aloud now");
                DockPanel.SetDock(play, Dock.Right);
                content.Children.Add(play);
            }

            content.Children.Add(new TextBlock { Text = item.Text, VerticalAlignment = VerticalAlignment.Center });

            var button = new Button { Classes = { "item" }, Content = content };
            button.Click += (_, _) =>
            {
                HideMenu();
                item.Action();
            };
            Items.Children.Add(button);
        }
    }
}
