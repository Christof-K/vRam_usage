using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Options;
using VRamUsage.Configuration.HardwareMonitoring;
using VRamUsage.Controllers.HardwareMonitoring;

namespace VRamUsage.Views.HardwareMonitoring;

/// <summary>
/// Code-behind for the transparent hardware monitor overlay window.
/// </summary>
public sealed partial class OverlayWindow : Window
{
    private readonly OverlayViewModel _viewModel;

    public OverlayWindow(
        OverlayViewModel viewModel,
        IOptions<OverlayOptions> options)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(options);

        _viewModel = viewModel;
        DataContext = _viewModel;

        InitializeComponent();

        Left = options.Value.InitialLeft;
        Top = options.Value.InitialTop;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel.StartMonitoring();
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_viewModel.IsLocked && e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnOpacity100Click(object sender, RoutedEventArgs e) => _viewModel.Opacity = 1.0;

    private void OnOpacity88Click(object sender, RoutedEventArgs e) => _viewModel.Opacity = 0.88;

    private void OnOpacity75Click(object sender, RoutedEventArgs e) => _viewModel.Opacity = 0.75;

    private void OnOpacity60Click(object sender, RoutedEventArgs e) => _viewModel.Opacity = 0.60;

    private void OnHideClick(object sender, RoutedEventArgs e) => Hide();

    private void OnExitClick(object sender, RoutedEventArgs e) => System.Windows.Application.Current.Shutdown();
}
