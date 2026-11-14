using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Reactive;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Helpers.App.Services;
using Helpers.App.ViewModels;

namespace Helpers.App.Overlays;

/// <summary>The floating player: a compact bar that expands into a resizable reading view.</summary>
public partial class PlayerWindow : OverlayWindow
{
    private const double DefaultExpandedWidth = 900;
    private const double MinExpandedWidth = 640;
    private const double MinExpandedHeight = 260;

    private readonly PlayerViewModel _viewModel;
    private readonly DispatcherTimer _motion;
    private LinearGradientBrush? _borderMotion;
    private LinearGradientBrush? _highlightMotion;
    private double _phase;
    private bool _resizing;
    private Point _resizeStart;
    private Size _resizeFrom;
    private int _renderedListIndex = -1;
    private readonly List<Fleck> _flecks = [];
    private readonly Random _random = new();
    private Color[] _fleckColours = [];
    private int _fleckDensity = 35;
    private bool _reducedMotion;

    public PlayerWindow(PlayerViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        viewModel.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(PlayerViewModel.CurrentIndex):
                    BringCurrentIntoView();
                    RenderListWord();
                    break;
                case nameof(PlayerViewModel.CurrentText):
                case nameof(PlayerViewModel.CurrentWordIndex):
                    RenderSentence();
                    RenderListWord();
                    break;
            }
        };

        // Own copies of the gradient brushes, so they can drift while reading.
        this.GetResourceObservable("OverlayBorderBrush").Subscribe(new AnonymousObserver<object?>(value =>
        {
            _borderMotion = CloneGradient(value);
            Card.BorderBrush = _borderMotion ?? value as IBrush;

            // Flecks take their colours from the gradient. A flat brush (Calm) means none.
            _fleckColours = value is LinearGradientBrush gradient
                ? [.. gradient.GradientStops.Select(s => s.Color), Color.Parse("#B6FF3B")]
                : [];
            BuildFlecks();
        }));
        Stage.SizeChanged += (_, _) => PlaceFlecks();
        _reducedMotion = AppPlatform.Current.ReducedMotion;
        this.GetResourceObservable("HighlightBrush").Subscribe(new AnonymousObserver<object?>(value =>
        {
            _highlightMotion = CloneGradient(value);
            Highlight.Background = _highlightMotion ?? value as IBrush;
        }));

        _motion = new DispatcherTimer(TimeSpan.FromMilliseconds(40), DispatcherPriority.Render, (_, _) => Drift());
        _motion.Start();
        Closed += (_, _) => _motion.Stop();
    }

    /// <summary>Raised when the user drags the window somewhere else, so the place can be remembered.</summary>
    public event Action<PixelPoint>? Moved;

    /// <summary>Raised by the cog button.</summary>
    public event Action? SettingsRequested;

    /// <summary>Raised after the reading view has been resized by hand, in device-independent pixels.</summary>
    public event Action<double, double>? ReadingViewResized;

    /// <summary>Raised when the reading view is opened or closed.</summary>
    public event Action<bool>? ExpandedChanged;

    public bool AnimateWhileReading { get; set; } = true;

    /// <summary>0 to 100: how many flecks drift around the card.</summary>
    public int FleckDensity
    {
        get => _fleckDensity;
        set
        {
            _fleckDensity = Math.Clamp(value, 0, 100);
            BuildFlecks();
        }
    }

    /// <summary>The size to use for the reading view, or null for the default.</summary>
    public Size? ExpandedSize { get; set; }

    /// <summary>Puts the window at a remembered place if that place is still on a screen, else bottom-right of the mouse's screen.</summary>
    public void PlaceAt(PixelPoint? remembered)
    {
        if (remembered is { } place && Screens.ScreenFromPoint(place) is not null)
        {
            Position = place;
            KeepOnScreen();
            return;
        }

        if (TargetScreen() is { } screen)
        {
            PlaceBottomRight(screen, 8, 120);
        }
    }

    /// <summary>Opens or closes the reading view. The sentence box moves to its own line when open.</summary>
    public void SetExpanded(bool expand)
    {
        if (expand == _viewModel.IsExpanded)
        {
            return;
        }

        if (expand)
        {
            var size = ExpandedSize ?? DefaultExpandedSize();
            SizeToContent = SizeToContent.Manual;
            Width = Math.Max(MinExpandedWidth, size.Width);
            Height = Math.Max(MinExpandedHeight, size.Height);

            Grid.SetRow(Highlight, 1);
            Grid.SetColumn(Highlight, 0);
            Grid.SetColumnSpan(Highlight, 11);
            Highlight.Width = double.NaN;
            Highlight.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            Highlight.Margin = new Thickness(0, 10, 0, 0);

            _viewModel.IsExpanded = true;
            BringCurrentIntoView();
            RenderListWord();
        }
        else
        {
            _viewModel.IsExpanded = false;

            Grid.SetRow(Highlight, 0);
            Grid.SetColumn(Highlight, 4);
            Grid.SetColumnSpan(Highlight, 1);
            Highlight.Width = 480;
            Highlight.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
            Highlight.Margin = new Thickness(10, 0);

            Width = double.NaN;
            Height = double.NaN;
            SizeToContent = SizeToContent.WidthAndHeight;
        }

        ExpandedChanged?.Invoke(expand);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        PositionChanged += (_, args) => Moved?.Invoke(args.Point);
    }

    private void OnPlayPause(object? sender, RoutedEventArgs e) => _viewModel.TogglePause();

    private void OnBack(object? sender, RoutedEventArgs e) => _viewModel.SkipBack();

    private void OnForward(object? sender, RoutedEventArgs e) => _viewModel.SkipForward();

    private void OnStop(object? sender, RoutedEventArgs e) => _viewModel.Stop();

    private void OnSettings(object? sender, RoutedEventArgs e) => SettingsRequested?.Invoke();

    private void OnToggleExpand(object? sender, RoutedEventArgs e) => SetExpanded(!_viewModel.IsExpanded);

    /// <summary>Closing the player stops the voice: with the player gone there's nothing left on screen to stop it with.</summary>
    private void OnHide(object? sender, RoutedEventArgs e)
    {
        _viewModel.Stop();
        Hide();
    }

    private void OnSegmentPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { DataContext: SegmentItem item })
        {
            _viewModel.JumpTo(item.Index);
            e.Handled = true;
        }
    }

    private void OnCardPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        // Dragging starts anywhere on the card that isn't a control.
        if (e.Source is Visual source && source.GetSelfAndVisualAncestors().Any(v => v is Button or Slider or ComboBox or Thumb || v == Grip))
        {
            return;
        }

        BeginMoveDrag(e);
    }

    private void OnGripPressed(object? sender, PointerPressedEventArgs e)
    {
        _resizing = true;
        _resizeStart = e.GetPosition(this);
        _resizeFrom = new Size(Width, Height);
        e.Pointer.Capture(Grip);
        e.Handled = true;
    }

    private void OnGripMoved(object? sender, PointerEventArgs e)
    {
        if (!_resizing)
        {
            return;
        }

        var now = e.GetPosition(this);
        Width = Math.Max(MinExpandedWidth, _resizeFrom.Width + (now.X - _resizeStart.X));
        Height = Math.Max(MinExpandedHeight, _resizeFrom.Height + (now.Y - _resizeStart.Y));
        e.Handled = true;
    }

    private void OnGripReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_resizing)
        {
            return;
        }

        _resizing = false;
        e.Pointer.Capture(null);
        ExpandedSize = new Size(Width, Height);
        ReadingViewResized?.Invoke(Width, Height);
        e.Handled = true;
    }

    /// <summary>About half the height of the screen the player is on, at the designed width.</summary>
    private Size DefaultExpandedSize()
    {
        var height = 520.0;
        if (HomeScreen() is { } screen)
        {
            height = Math.Max(MinExpandedHeight, screen.WorkingArea.Height / screen.Scaling * 0.55);
        }

        return new Size(DefaultExpandedWidth, height);
    }

    private void BringCurrentIntoView()
    {
        if (!_viewModel.IsExpanded || _viewModel.CurrentIndex < 0)
        {
            return;
        }

        var container = SegmentList.ContainerFromIndex(_viewModel.CurrentIndex);
        container?.BringIntoView();
    }

    /// <summary>
    /// Draws the current sentence word by word in the bar, marks the word being
    /// spoken, and scrolls so that word's line is in view. Nothing is ever cut off.
    /// </summary>
    private void RenderSentence()
    {
        Sentence.Inlines = BuildInlines(_viewModel.Words, _viewModel.CurrentWordIndex, _viewModel.CurrentText, out var currentStart);

        if (currentStart < 0)
        {
            SentenceScroller.Offset = new Vector(0, 0);
            return;
        }

        // Scroll so the current word's line is the top line of the two shown.
        Dispatcher.UIThread.Post(() =>
        {
            var layout = Sentence.TextLayout;
            if (layout is null)
            {
                return;
            }

            var rect = layout.HitTestTextPosition(currentStart);
            var lineTop = Math.Max(0, rect.Top);
            var visible = SentenceScroller.Viewport.Height;
            var offset = SentenceScroller.Offset.Y;
            if (lineTop < offset || rect.Bottom > offset + visible)
            {
                SentenceScroller.Offset = new Vector(0, lineTop);
            }
        }, DispatcherPriority.Loaded);
    }

    /// <summary>Marks the spoken word on the current line of the reading view as well, and clears the line it left.</summary>
    private void RenderListWord()
    {
        if (!_viewModel.IsExpanded)
        {
            return;
        }

        var index = _viewModel.CurrentIndex;
        if (_renderedListIndex >= 0 && _renderedListIndex != index)
        {
            ResetListLine(_renderedListIndex);
            _renderedListIndex = -1;
        }

        if (index < 0 || FindListTextBlock(index) is not { } block)
        {
            return;
        }

        block.Inlines = BuildInlines(_viewModel.Words, _viewModel.CurrentWordIndex, _viewModel.CurrentText, out _);
        _renderedListIndex = index;
    }

    private void ResetListLine(int index)
    {
        if (FindListTextBlock(index) is { } block && index < _viewModel.Segments.Count)
        {
            block.Inlines = new InlineCollection { new Run(_viewModel.Segments[index].Display) };
        }
    }

    private TextBlock? FindListTextBlock(int index) =>
        SegmentList.ContainerFromIndex(index)?.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault();

    /// <summary>Words as runs, the current one marked. <paramref name="currentStart"/> is its character index, or -1.</summary>
    private InlineCollection BuildInlines(IReadOnlyList<string> words, int current, string fallback, out int currentStart)
    {
        var inlines = new InlineCollection();
        currentStart = -1;
        if (words.Count == 0)
        {
            inlines.Add(new Run(fallback));
            return inlines;
        }

        var position = 0;
        var wordBrush = this.FindResource("WordBrush") as IBrush;
        for (var i = 0; i < words.Count; i++)
        {
            var run = new Run(words[i]);
            if (i == current)
            {
                run.Background = wordBrush;
                run.FontWeight = FontWeight.SemiBold;
                currentStart = position;
            }

            inlines.Add(run);
            position += words[i].Length;
            if (i < words.Count - 1)
            {
                inlines.Add(new Run(" "));
                position += 1;
            }
        }

        return inlines;
    }

    /// <summary>One fleck of colour on the ground around the card.</summary>
    private sealed class Fleck
    {
        public required Border Visual { get; init; }

        public double X { get; set; }

        public double Y { get; set; }

        public double Phase { get; init; }

        public double Speed { get; init; }
    }

    /// <summary>Creates the flecks for the current colours and density. Nothing in Calm or at zero.</summary>
    private void BuildFlecks()
    {
        FleckLayer.Children.Clear();
        _flecks.Clear();
        if (_fleckColours.Length == 0 || _fleckDensity <= 0)
        {
            return;
        }

        var count = (int)Math.Round(_fleckDensity / 100.0 * 28);
        for (var i = 0; i < count; i++)
        {
            var size = _random.Next(3, 7);
            var colour = _fleckColours[i % _fleckColours.Length];
            var visual = new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(size),
                Background = new SolidColorBrush(colour),
                BoxShadow = BoxShadows.Parse($"0 0 {size * 2} 0 #99{colour.R:X2}{colour.G:X2}{colour.B:X2}"),
                Opacity = 0.85,
                IsHitTestVisible = false,
            };

            FleckLayer.Children.Add(visual);
            _flecks.Add(new Fleck
            {
                Visual = visual,
                Phase = _random.NextDouble() * Math.Tau,
                Speed = 0.3 + _random.NextDouble() * 0.7,
            });
        }

        PlaceFlecks();
    }

    /// <summary>Scatters the flecks in the band of ground around the card, where they can be seen.</summary>
    private void PlaceFlecks()
    {
        var bounds = Stage.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var band = Card.Margin.Left;
        foreach (var fleck in _flecks)
        {
            var along = _random.NextDouble();
            var depth = _random.NextDouble() * band;
            switch (_random.Next(4))
            {
                case 0: // top
                    fleck.X = along * bounds.Width;
                    fleck.Y = depth;
                    break;
                case 1: // bottom
                    fleck.X = along * bounds.Width;
                    fleck.Y = bounds.Height - depth;
                    break;
                case 2: // left
                    fleck.X = depth;
                    fleck.Y = along * bounds.Height;
                    break;
                default: // right
                    fleck.X = bounds.Width - depth;
                    fleck.Y = along * bounds.Height;
                    break;
            }

            Canvas.SetLeft(fleck.Visual, fleck.X);
            Canvas.SetTop(fleck.Visual, fleck.Y);
        }
    }

    /// <summary>Moves the gradients a little each tick while reading, and the flecks whenever the window shows.</summary>
    private void Drift()
    {
        if (!IsVisible)
        {
            return;
        }

        if (!_reducedMotion && _flecks.Count > 0)
        {
            var bounds = Stage.Bounds;
            var t = Environment.TickCount64 / 1000.0;
            foreach (var fleck in _flecks)
            {
                // A slow wander that never leaves the stage; the card hides any that cross it.
                fleck.X += Math.Cos(fleck.Phase + t * fleck.Speed) * 0.2;
                fleck.Y += Math.Sin(fleck.Phase * 1.3 + t * fleck.Speed * 0.8) * 0.15;
                fleck.X = Math.Clamp(fleck.X, 0, bounds.Width);
                fleck.Y = Math.Clamp(fleck.Y, 0, bounds.Height);
                Canvas.SetLeft(fleck.Visual, fleck.X);
                Canvas.SetTop(fleck.Visual, fleck.Y);
            }
        }

        if (!AnimateWhileReading || _reducedMotion || !_viewModel.ShowPause)
        {
            return;
        }

        _phase += 0.015;
        if (_borderMotion is not null)
        {
            var x = 0.5 + 0.5 * Math.Cos(_phase);
            var y = 0.5 + 0.5 * Math.Sin(_phase);
            _borderMotion.StartPoint = new RelativePoint(x, y, RelativeUnit.Relative);
            _borderMotion.EndPoint = new RelativePoint(1 - x, 1 - y, RelativeUnit.Relative);
        }

        if (_highlightMotion is not null)
        {
            var x = 0.5 + 0.5 * Math.Cos(_phase * 0.6);
            _highlightMotion.StartPoint = new RelativePoint(x, 0, RelativeUnit.Relative);
            _highlightMotion.EndPoint = new RelativePoint(1 - x, 1, RelativeUnit.Relative);
        }
    }

    private static LinearGradientBrush? CloneGradient(object? value)
    {
        if (value is not LinearGradientBrush source)
        {
            return null;
        }

        var clone = new LinearGradientBrush
        {
            StartPoint = source.StartPoint,
            EndPoint = source.EndPoint,
        };

        foreach (var stop in source.GradientStops)
        {
            clone.GradientStops.Add(new GradientStop(stop.Color, stop.Offset));
        }

        return clone;
    }
}
