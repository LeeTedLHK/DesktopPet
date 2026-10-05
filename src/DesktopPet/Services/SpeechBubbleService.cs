using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace DesktopPet.Services;

public sealed class SpeechBubbleService : IDisposable
{
    private readonly Window _pet;
    private readonly Window _bubble;
    private readonly TextBlock _text;
    private readonly DispatcherTimer _timer;
    private bool _disposed;
    private readonly ScreenBoundsService? _screen;

    public SpeechBubbleService(Window pet, ScreenBoundsService? screen = null)
    {
        _pet = pet;
        _screen = screen;
        _text = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 13,
            FontFamily = new FontFamily("Microsoft YaHei UI"), Foreground = new SolidColorBrush(Color.FromRgb(36, 60, 54)) };
        _bubble = new Window
        {
            Title = "DesktopPet 气泡", AllowsTransparency = true, WindowStyle = WindowStyle.None,
            Background = Brushes.Transparent, ShowInTaskbar = false, ShowActivated = false,
            ResizeMode = ResizeMode.NoResize, SizeToContent = SizeToContent.Height, Width = 210,
            Topmost = pet.Topmost, IsHitTestVisible = false,
            Content = new Border { Background = new SolidColorBrush(Color.FromRgb(248, 252, 248)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(180, 205, 190)), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12), Padding = new Thickness(13, 10, 13, 10), Child = _text }
        };
        _bubble.SourceInitialized += OnSourceInitialized;
        _pet.LocationChanged += OnPositionChanged;
        _pet.SizeChanged += OnSizeChanged;
        _timer = new DispatcherTimer(DispatcherPriority.Background, pet.Dispatcher)
        { Interval = TimeSpan.FromSeconds(3) };
        _timer.Tick += OnTimeout;
    }

    private void OnSourceInitialized(object? sender, EventArgs e) => NativeWindowService.SetClickThrough(_bubble, true);

    public void Show(string text)
    {
        if (_disposed || !_pet.IsVisible) return;
        _timer.Stop();
        _text.Text = text;
        _bubble.Owner ??= _pet; // An owner must have been shown before WPF accepts it.
        _bubble.Topmost = _pet.Topmost;
        _bubble.Show();
        _bubble.UpdateLayout();
        Reposition();
        _timer.Start();
    }

    private void Reposition()
    {
        if (!_bubble.IsVisible) return;
        var area = _screen?.WorkArea ?? new WorkArea(SystemParameters.WorkArea.Left,
            SystemParameters.WorkArea.Top, SystemParameters.WorkArea.Width, SystemParameters.WorkArea.Height);
        var petPosition = _screen?.Position(_pet) ?? new PetPoint(_pet.Left, _pet.Top);
        var height = _bubble.ActualHeight;
        var y = petPosition.Y - height - 6;
        if (y < area.Top) y = petPosition.Y + _pet.Height + 6;
        var point = ScreenGeometry.Clamp(new(petPosition.X + (_pet.Width - _bubble.Width) / 2, y),
            new(_bubble.Width, height), new(area.Left, area.Top, area.Width, area.Height));
        if (_screen is not null)
        {
            _bubble.Height = height;
            NativeWindowService.Place(_bubble, _screen.CurrentMonitor.ToPixels(point), _screen.CurrentMonitor.Scale);
        }
        else { _bubble.Left = point.X; _bubble.Top = point.Y; }
    }

    public void Hide()
    {
        _timer.Stop();
        _bubble.Hide();
    }

    private void OnPositionChanged(object? sender, EventArgs e) => Reposition();
    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Reposition();
    private void OnTimeout(object? sender, EventArgs e) => Hide();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTimeout;
        _pet.LocationChanged -= OnPositionChanged;
        _pet.SizeChanged -= OnSizeChanged;
        _bubble.SourceInitialized -= OnSourceInitialized;
        _bubble.Close();
    }
}
