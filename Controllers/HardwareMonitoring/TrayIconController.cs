using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows;
using Microsoft.Extensions.Logging;

namespace VRamUsage.Controllers.HardwareMonitoring;

/// <summary>
/// Controller for managing the system tray notification icon and context menu.
/// </summary>
public sealed class TrayIconController : IDisposable
{
    private readonly OverlayViewModel _viewModel;
    private readonly Window _window;
    private readonly ILogger<TrayIconController> _logger;
    private readonly System.Windows.Forms.NotifyIcon _notifyIcon;
    private bool _isDisposed;

    public TrayIconController(
        OverlayViewModel viewModel,
        Window window,
        ILogger<TrayIconController> logger)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(logger);

        _viewModel = viewModel;
        _window = window;
        _logger = logger;

        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "AMD Radeon 890M & RAM Monitor",
            Icon = GenerateTrayIcon(),
            Visible = true
        };

        InitializeContextMenu();

        _notifyIcon.DoubleClick += (_, _) => ToggleWindowVisibility();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void InitializeContextMenu()
    {
        var contextMenu = new System.Windows.Forms.ContextMenuStrip();

        var titleItem = new System.Windows.Forms.ToolStripMenuItem("AMD Radeon 890M & RAM Monitor")
        {
            Enabled = false,
            Font = new Font(System.Drawing.SystemFonts.DefaultFont, System.Drawing.FontStyle.Bold)
        };
        contextMenu.Items.Add(titleItem);
        contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        var visibilityItem = new System.Windows.Forms.ToolStripMenuItem("Ausblenden / Einblenden", null, (_, _) => ToggleWindowVisibility());
        contextMenu.Items.Add(visibilityItem);

        var lockItem = new System.Windows.Forms.ToolStripMenuItem("Position sperren", null, (_, _) =>
        {
            _viewModel.IsLocked = !_viewModel.IsLocked;
        })
        {
            Checked = _viewModel.IsLocked
        };
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(OverlayViewModel.IsLocked))
            {
                lockItem.Checked = _viewModel.IsLocked;
            }
        };
        contextMenu.Items.Add(lockItem);

        var topmostItem = new System.Windows.Forms.ToolStripMenuItem("Immer im Vordergrund", null, (_, _) =>
        {
            _viewModel.IsAlwaysOnTop = !_viewModel.IsAlwaysOnTop;
        })
        {
            Checked = _viewModel.IsAlwaysOnTop
        };
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(OverlayViewModel.IsAlwaysOnTop))
            {
                topmostItem.Checked = _viewModel.IsAlwaysOnTop;
            }
        };
        contextMenu.Items.Add(topmostItem);

        // Opacity Submenu
        var opacityMenu = new System.Windows.Forms.ToolStripMenuItem("Deckkraft / Transparenz");
        var opacityLevels = new (string Label, double Value)[]
        {
            ("100% (Undurchsichtig)", 1.0),
            ("88% (Standard)", 0.88),
            ("75% (Dezent)", 0.75),
            ("60% (Transparent)", 0.60),
            ("40% (Sehr transparent)", 0.40)
        };

        foreach (var (label, val) in opacityLevels)
        {
            var item = new System.Windows.Forms.ToolStripMenuItem(label, null, (_, _) => _viewModel.Opacity = val)
            {
                Checked = Math.Abs(_viewModel.Opacity - val) < 0.05
            };
            _viewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(OverlayViewModel.Opacity))
                {
                    item.Checked = Math.Abs(_viewModel.Opacity - val) < 0.05;
                }
            };
            opacityMenu.DropDownItems.Add(item);
        }
        contextMenu.Items.Add(opacityMenu);

        // Refresh Rate Submenu
        var refreshMenu = new System.Windows.Forms.ToolStripMenuItem("Aktualisierungsrate");
        var refreshIntervals = new (string Label, int Ms)[]
        {
            ("500 ms (Schnell)", 500),
            ("1000 ms (Standard)", 1000),
            ("2000 ms (Sparsam)", 2000)
        };

        foreach (var (label, ms) in refreshIntervals)
        {
            var item = new System.Windows.Forms.ToolStripMenuItem(label, null, (_, _) => _viewModel.RefreshIntervalMs = ms)
            {
                Checked = _viewModel.RefreshIntervalMs == ms
            };
            _viewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(OverlayViewModel.RefreshIntervalMs))
                {
                    item.Checked = _viewModel.RefreshIntervalMs == ms;
                }
            };
            refreshMenu.DropDownItems.Add(item);
        }
        contextMenu.Items.Add(refreshMenu);

        contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        var exitItem = new System.Windows.Forms.ToolStripMenuItem("Beenden", null, (_, _) =>
        {
            _logger.LogInformation("Exit requested via tray icon menu");
            _notifyIcon.Visible = false;
            System.Windows.Application.Current.Shutdown();
        });
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;
    }

    private void ToggleWindowVisibility()
    {
        if (_window.Visibility == Visibility.Visible)
        {
            _window.Hide();
        }
        else
        {
            _window.Show();
            _window.Activate();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(OverlayViewModel.GpuLoadText)
            or nameof(OverlayViewModel.VramSummaryText)
            or nameof(OverlayViewModel.RamSummaryText)
            or nameof(OverlayViewModel.CpuLoadText))
        {
            var summary = $"GPU: {_viewModel.GpuLoadText} | VRAM: {_viewModel.VramSummaryText}\nRAM: {_viewModel.RamSummaryText} | CPU: {_viewModel.CpuLoadText}";
            if (summary.Length >= 128)
            {
                summary = summary[..127];
            }
            _notifyIcon.Text = summary;
        }
    }

    private static Icon GenerateTrayIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bitmap);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        // Dark rounded square background
        using var bgBrush = new SolidBrush(Color.FromArgb(240, 20, 24, 33));
        using var borderPen = new Pen(Color.FromArgb(200, 56, 189, 248), 2f);

        g.FillRoundedRectangle(bgBrush, new Rectangle(2, 2, 28, 28), 6);
        g.DrawRoundedRectangle(borderPen, new Rectangle(2, 2, 28, 28), 6);

        // Draw "V" in cyan / violet
        using var textBrush = new SolidBrush(Color.FromArgb(255, 56, 189, 248));
        using var font = new Font("Segoe UI", 13f, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);

        var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        g.DrawString("V", font, textBrush, new RectangleF(0, 0, 32, 32), format);

        var hIcon = bitmap.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _isDisposed = true;
    }
}

file static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics g, Brush brush, Rectangle bounds, int cornerRadius)
    {
        using var path = CreateRoundedRectanglePath(bounds, cornerRadius);
        g.FillPath(brush, path);
    }

    public static void DrawRoundedRectangle(this Graphics g, Pen pen, Rectangle bounds, int cornerRadius)
    {
        using var path = CreateRoundedRectanglePath(bounds, cornerRadius);
        g.DrawPath(pen, path);
    }

    private static GraphicsPath CreateRoundedRectanglePath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        var arc = new Rectangle(bounds.Location, new System.Drawing.Size(diameter, diameter));

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}
