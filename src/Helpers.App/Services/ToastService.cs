using System.Collections.ObjectModel;
using Avalonia.Threading;
using Helpers.App.ViewModels;

namespace Helpers.App.Services;

public enum ToastKind
{
    Info,
    Success,
    Error,
    Progress,
}

/// <summary>One message on screen.</summary>
public sealed class ToastItem : ObservableObject
{
    private string _text = string.Empty;
    private double _fraction;
    private string _detail = string.Empty;

    public ToastItem(ToastKind kind, string text, string? actionLabel, Action? action)
    {
        Kind = kind;
        _text = text;
        ActionLabel = actionLabel ?? string.Empty;
        Action = action;
    }

    public ToastKind Kind { get; }

    public bool IsSuccess => Kind == ToastKind.Success;

    public bool IsError => Kind == ToastKind.Error;

    public bool IsProgress => Kind == ToastKind.Progress;

    public string Text
    {
        get => _text;
        set => Set(ref _text, value);
    }

    public string Detail
    {
        get => _detail;
        set
        {
            if (Set(ref _detail, value))
            {
                Raise(nameof(HasDetail));
            }
        }
    }

    public bool HasDetail => Detail.Length > 0;

    /// <summary>0 to 1 for progress toasts.</summary>
    public double Fraction
    {
        get => _fraction;
        set => Set(ref _fraction, value);
    }

    public string ActionLabel { get; }

    public bool HasAction => ActionLabel.Length > 0;

    public Action? Action { get; }

    internal DispatcherTimer? Timer { get; set; }
}

/// <summary>
/// The only way the app talks to the user outside the player and Compose.
/// Confirmations fade after a few seconds, errors stay until clicked,
/// progress stays until it is done. Never more than three at once.
/// </summary>
public sealed class ToastService
{
    public const int MaxVisible = 3;

    public static readonly TimeSpan ConfirmationLife = TimeSpan.FromSeconds(3);

    public ObservableCollection<ToastItem> Items { get; } = [];

    public event Action? Changed;

    public ToastItem Info(string text) => Show(ToastKind.Info, text, null, null, ConfirmationLife);

    public ToastItem Success(string text) => Show(ToastKind.Success, text, null, null, ConfirmationLife);

    public ToastItem Error(string text, string? actionLabel = null, Action? action = null) =>
        Show(ToastKind.Error, text, actionLabel, action, null);

    public ToastItem Progress(string text) => Show(ToastKind.Progress, text, null, null, null);

    /// <summary>A gentle offer with one button that fades if ignored.</summary>
    public ToastItem Offer(string text, string actionLabel, Action action) =>
        Show(ToastKind.Info, text, actionLabel, action, TimeSpan.FromSeconds(6));

    /// <summary>News worth a proper read, with one button: stays a good while, then fades. Not an error.</summary>
    public ToastItem Notice(string text, string actionLabel, Action action) =>
        Show(ToastKind.Info, text, actionLabel, action, TimeSpan.FromSeconds(20));

    public void Dismiss(ToastItem item) => Dispatcher.UIThread.Post(() => Remove(item));

    private ToastItem Show(ToastKind kind, string text, string? actionLabel, Action? action, TimeSpan? life)
    {
        var item = new ToastItem(kind, text, actionLabel, action);
        Dispatcher.UIThread.Post(() =>
        {
            while (Items.Count >= MaxVisible)
            {
                Remove(Items[0]);
            }

            Items.Add(item);
            if (life is { } span)
            {
                item.Timer = new DispatcherTimer(span, DispatcherPriority.Background, (_, _) => Remove(item));
                item.Timer.Start();
            }

            Changed?.Invoke();
        });

        return item;
    }

    private void Remove(ToastItem item)
    {
        item.Timer?.Stop();
        item.Timer = null;
        if (Items.Remove(item))
        {
            Changed?.Invoke();
        }
    }
}
