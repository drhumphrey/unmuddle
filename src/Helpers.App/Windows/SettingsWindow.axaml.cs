using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Helpers.App.ViewModels;
using Helpers.Core;
using Helpers.Core.Text;

namespace Helpers.App.Windows;

/// <summary>A normal window; it takes focus because it holds controls to type into.</summary>
public partial class SettingsWindow : ShellWindow
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    /// <summary>Opens the page with this header, such as "AI". Unknown names are ignored.</summary>
    public void SelectTab(string header)
    {
        foreach (var item in Tabs.Items)
        {
            if (item is TabItem tab && string.Equals(tab.Header?.ToString(), header, StringComparison.OrdinalIgnoreCase))
            {
                Tabs.SelectedItem = tab;
                return;
            }
        }
    }

    private void OnPreview(object? sender, RoutedEventArgs e) => _viewModel.PreviewVoice();

    private void OnCheckForUpdates(object? sender, RoutedEventArgs e) => _ = _viewModel.CheckForUpdatesNowAsync();

    private void OnOpenRepo(object? sender, RoutedEventArgs e) => Services.Links.Open(HelpersInfo.RepositoryUrl);

    private void OnOpenPrivacy(object? sender, RoutedEventArgs e) => Services.Links.Open(HelpersInfo.RepositoryUrl + "/blob/main/docs/PRIVACY.md");

    private void OnOpenAnthropicPrivacy(object? sender, RoutedEventArgs e) => Services.Links.Open("https://www.anthropic.com/legal/privacy");

    private void OnOpenComponent(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: Helpers.Core.ThirdPartyComponent component })
        {
            Services.Links.Open(component.Url);
        }
    }

    private void OnSaveKey(object? sender, RoutedEventArgs e) => _viewModel.SaveCloudKey();

    private void OnRemoveKey(object? sender, RoutedEventArgs e) => _viewModel.RemoveCloudKey();

    private void OnDownloadModel(object? sender, RoutedEventArgs e) => _ = _viewModel.DownloadLocalModelAsync();

    private void OnCancelDownload(object? sender, RoutedEventArgs e) => _viewModel.CancelDownload();

    private void OnResetPrompt(object? sender, RoutedEventArgs e) => _viewModel.ResetPrompt();

    private void OnAddPronunciation(object? sender, RoutedEventArgs e) => _viewModel.AddPronunciation();

    private void OnPronunciationKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _viewModel.AddPronunciation();
            e.Handled = true;
        }
    }

    private void OnRemovePronunciation(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: PronunciationEntry entry })
        {
            _viewModel.RemovePronunciation(entry);
        }
    }

    /// <summary>Turns the keys the user presses in the shortcut box into "Ctrl+Alt+Space" text.</summary>
    private void OnHotkeyKeyDown(object? sender, KeyEventArgs e)
    {
        e.Handled = true;

        var key = e.Key switch
        {
            Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin => null,
            Key.Space => "Space",
            Key.Insert => "Insert",
            Key.Delete => "Delete",
            Key.Escape => "Escape",
            Key.Enter => "Enter",
            Key.Tab => "Tab",
            Key.Back => "Backspace",
            Key.Home => "Home",
            Key.End => "End",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            Key.Up => "Up",
            Key.Down => "Down",
            Key.Left => "Left",
            Key.Right => "Right",
            Key.Pause => "Pause",
            Key.Scroll => "ScrollLock",
            >= Key.A and <= Key.Z => e.Key.ToString(),
            >= Key.D0 and <= Key.D9 => e.Key.ToString()[1..],
            >= Key.F1 and <= Key.F24 => e.Key.ToString(),
            >= Key.NumPad0 and <= Key.NumPad9 => e.Key.ToString(),
            _ => null,
        };

        if (key is null)
        {
            return;
        }

        var parts = new List<string>();
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        if (e.KeyModifiers.HasFlag(KeyModifiers.Meta)) parts.Add("Win");
        parts.Add(key);

        var text = string.Join("+", parts);
        if (sender is TextBox { Name: "ComposeHotkeyBox" })
        {
            _viewModel.ComposeHotkeyText = text;
        }
        else if (sender is TextBox { Name: "ReadButtonHotkeyBox" })
        {
            _viewModel.ReadButtonHotkeyText = text;
        }
        else
        {
            _viewModel.HotkeyText = text;
        }
    }
}
