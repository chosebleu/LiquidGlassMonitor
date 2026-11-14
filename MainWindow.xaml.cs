using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

namespace LiquidGlassMonitor;

public partial class MainWindow : Window
{
    // HeaderVisibilityV51
    private bool showHeaderTime = true;
    private bool showHeaderDate = true;

    private string HeaderVisibilitySettingsPath =>
        System.IO.Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "LiquidGlassMonitor",
            "header_visibility_v51.json");

    private void LoadHeaderVisibility()
    {
        try
        {
            if (System.IO.File.Exists(HeaderVisibilitySettingsPath))
            {
                var data = System.Text.Json.JsonSerializer.Deserialize<
                    System.Collections.Generic.Dictionary<string, bool>>(
                    System.IO.File.ReadAllText(HeaderVisibilitySettingsPath));

                if (data != null)
                {
                    if (data.TryGetValue("Time", out bool time))
                        showHeaderTime = time;

                    if (data.TryGetValue("Date", out bool date))
                        showHeaderDate = date;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }

        ApplyHeaderVisibility();
    }

    private void SaveHeaderVisibility()
    {
        try
        {
            string? directory =
                System.IO.Path.GetDirectoryName(HeaderVisibilitySettingsPath);

            if (directory != null)
                System.IO.Directory.CreateDirectory(directory);

            var data = new System.Collections.Generic.Dictionary<string, bool>
            {
                ["Time"] = showHeaderTime,
                ["Date"] = showHeaderDate
            };

            System.IO.File.WriteAllText(
                HeaderVisibilitySettingsPath,
                System.Text.Json.JsonSerializer.Serialize(data));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private void ApplyHeaderVisibility()
    {
        HeaderClockText.Visibility = showHeaderTime
            ? Visibility.Visible : Visibility.Collapsed;

        HeaderDateText.Visibility = showHeaderDate
            ? Visibility.Visible : Visibility.Collapsed;

        HeaderDateText.Margin = showHeaderTime
            ? new Thickness(12, 0, 0, 0)
            : new Thickness(0);
    }

    private void SetupHeaderVisibilityOptions()
    {
        if (SettingsButton.ContextMenu is not { } menu)
            return;

        System.Windows.Controls.StackPanel? panel = null;

        foreach (object item in menu.Items)
        {
            if (item is System.Windows.Controls.MenuItem mi &&
                mi.Header is System.Windows.Controls.StackPanel stack)
            {
                panel = stack;
                break;
            }
        }

        if (panel == null)
            throw new InvalidOperationException(
                "Panneau des parametres introuvable.");

        var timeCheck = new System.Windows.Controls.CheckBox
        {
            Content = "Afficher l'heure",
            IsChecked = showHeaderTime,
            Margin = new Thickness(0, 5, 0, 3),
            Foreground = System.Windows.Media.Brushes.White
        };

        var dateCheck = new System.Windows.Controls.CheckBox
        {
            Content = "Afficher la date",
            IsChecked = showHeaderDate,
            Margin = new Thickness(0, 3, 0, 5),
            Foreground = System.Windows.Media.Brushes.White
        };

        timeCheck.Checked += (_, _) =>
        {
            showHeaderTime = true;
            ApplyHeaderVisibility();
            SaveHeaderVisibility();
        };

        timeCheck.Unchecked += (_, _) =>
        {
            showHeaderTime = false;
            ApplyHeaderVisibility();
            SaveHeaderVisibility();
        };

        dateCheck.Checked += (_, _) =>
        {
            showHeaderDate = true;
            ApplyHeaderVisibility();
            SaveHeaderVisibility();
        };

        dateCheck.Unchecked += (_, _) =>
        {
            showHeaderDate = false;
            ApplyHeaderVisibility();
            SaveHeaderVisibility();
        };

        panel.Children.Add(timeCheck);
        panel.Children.Add(dateCheck);
    }
    private void UpdateHeaderClock()
    {
        HeaderClockText.Text = DateTime.Now.ToString("HH:mm");
        HeaderDateText.Text = DateTime.Now.ToString(
            currentLanguage == "en" ? "MM/dd/yy" : "dd/MM/yy",
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Queue<double> history = new();
    private readonly HardwareSensors hardwareSensors = new();
    private readonly Dictionary<string, (long total, long free)> drives = new();
    private bool locked;
    private bool manualResize;
    private bool compactMode;
    private double normalWidth;
    private double normalHeight;
    private double normalMinHeight;
    private readonly Dictionary<string, Ellipse> compactRings = new();
    private readonly Dictionary<string, TextBlock> compactValues = new();
    private readonly List<MtpStorageInfo> compactMtpStorages = new();
    private readonly List<string> compactMtpDevices = new();
    private string compactTopology = "";
    private readonly string windowStateFile = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LiquidGlassMonitor", "window-state.json");

    private readonly DispatcherTimer windowStateTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(700)
    };

    private bool windowStateReady;

    private sealed class SavedWindowState
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool ManualResize { get; set; }
    }

    private bool restoringCompactStartup;
    private bool compactGeometryKnown;
    private double compactLeft, compactTop, compactWidth, compactHeight;
    private bool compactManualResize;
    private double normalLeft, normalTop;
    private bool normalManualResize;

    private readonly string compactStateFile = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LiquidGlassMonitor", "compact-window-state.json");

    private sealed class CompactState
    {
        public bool IsCompactMode { get; set; }
        public bool HasGeometry { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool ManualResize { get; set; }
    }

    private void LoadCompactState()
    {
        try
        {
            if (!System.IO.File.Exists(compactStateFile)) return;
            var s = System.Text.Json.JsonSerializer.Deserialize<CompactState>(
                System.IO.File.ReadAllText(compactStateFile));
            if (s == null) return;

            restoringCompactStartup = s.IsCompactMode;
            compactGeometryKnown = s.HasGeometry &&
                double.IsFinite(s.Left) && double.IsFinite(s.Top) &&
                double.IsFinite(s.Width) && double.IsFinite(s.Height) &&
                s.Width > 0 && s.Height > 0;

            if (compactGeometryKnown)
            {
                compactLeft = s.Left;
                compactTop = s.Top;
                compactWidth = s.Width;
                compactHeight = s.Height;
                compactManualResize = s.ManualResize;
            }

            if (!compactGeometryKnown)
                restoringCompactStartup = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                "Chargement compact : " + ex.Message);
        }
    }

    private void CaptureCompactGeometry()
    {
        if (!compactMode || WindowState != WindowState.Normal) return;
        if (!double.IsFinite(Width) || !double.IsFinite(Height) ||
            !double.IsFinite(Left) || !double.IsFinite(Top)) return;

        compactLeft = Left;
        compactTop = Top;
        compactWidth = Width;
        compactHeight = Height;
        compactManualResize = manualResize;
        compactGeometryKnown = true;
    }

    private void SaveCompactState()
    {
        if (!windowStateReady || restoringCompactStartup) return;
        try
        {
            if (compactMode && !restoringCompactStartup)
                CaptureCompactGeometry();

            var s = new CompactState
            {
                IsCompactMode = compactMode,
                HasGeometry = compactGeometryKnown,
                Left = compactLeft,
                Top = compactTop,
                Width = compactWidth,
                Height = compactHeight,
                ManualResize = compactManualResize
            };

            string? dir = System.IO.Path.GetDirectoryName(compactStateFile);
            if (dir != null) System.IO.Directory.CreateDirectory(dir);

            string json = System.Text.Json.JsonSerializer.Serialize(
                s, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

            System.IO.File.WriteAllText(compactStateFile + ".tmp", json);
            System.IO.File.Move(
                compactStateFile + ".tmp", compactStateFile, true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                "Sauvegarde compacte : " + ex.Message);
        }
    }

    // CleanupBottomAlignmentV55 : anciennes methodes supprimees.

    private void RestorePosition(double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y))
            return;

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = x;
        Top = y;
    }

    // DpiTransitionSync_V45
    // Resynchronisation apres une transition entre deux moniteurs.
    private readonly DispatcherTimer dpiTransitionTimerV45 = new()
    {
        Interval = TimeSpan.FromMilliseconds(40)
    };

    private int dpiTransitionPassesV45;

    private void SynchronizeSatellitesV45()
    {
        if (!IsLoaded || WindowState != WindowState.Normal)
            return;

        weatherWindow?.FollowParent();
        mediaWindow?.FollowParent();
        PositionSettingsWindow();
    }

    private void ScheduleDpiTransitionV45()
    {
        dpiTransitionPassesV45 = 0;
        dpiTransitionTimerV45.Stop();

        Dispatcher.BeginInvoke(
            new Action(SynchronizeSatellitesV45),
            DispatcherPriority.Render
        );

        dpiTransitionTimerV45.Start();
    }

    private IntPtr HandleDpiTransitionV45(
        IntPtr hwnd,
        int msg,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        const int WM_DPICHANGED = 0x02E0;
        const int WM_EXITSIZEMOVE = 0x0232;

        if (msg == WM_DPICHANGED ||
            msg == WM_EXITSIZEMOVE)
        {
            Dispatcher.BeginInvoke(
                new Action(ScheduleDpiTransitionV45),
                DispatcherPriority.Loaded
            );
        }

        // Laisser WPF traiter le changement DPI normalement.
        return IntPtr.Zero;
    }
    private IntPtr PreventMaximize(
        IntPtr hwnd, int msg, IntPtr wParam,
        IntPtr lParam, ref bool handled)
    {
        const int WM_SYSCOMMAND = 0x0112;
        const long SC_MAXIMIZE = 0xF030;

        if (msg == WM_SYSCOMMAND &&
            (wParam.ToInt64() & 0xFFF0) == SC_MAXIMIZE)
        {
            handled = true;
            return IntPtr.Zero;
        }

        return IntPtr.Zero;
    }
    // ScreenRecovery_V48
    // Recuperation des fenetres dont la position est devenue inaccessible.
    private System.Windows.Point RecoverWindowPositionV48(
        double x, double y, double width, double height)
    {
        var workArea = SystemParameters.WorkArea;

        double fallbackX = workArea.Left +
            Math.Max(0, (workArea.Width - width) / 2);

        double fallbackY = workArea.Top +
            Math.Max(0, (workArea.Height - height) / 2);

        var fallback = new System.Windows.Point(fallbackX, fallbackY);

        if (!double.IsFinite(x) || !double.IsFinite(y) ||
            !double.IsFinite(width) || !double.IsFinite(height) ||
            width <= 0 || height <= 0)
            return fallback;

        double left = SystemParameters.VirtualScreenLeft;
        double top = SystemParameters.VirtualScreenTop;
        double right = left + SystemParameters.VirtualScreenWidth;
        double bottom = top + SystemParameters.VirtualScreenHeight;

        const double margin = 60;

        double visibleWidth = Math.Max(0,
            Math.Min(x + width, right) - Math.Max(x, left));

        double visibleHeight = Math.Max(0,
            Math.Min(y + height, bottom) - Math.Max(y, top));

        if (visibleWidth >= Math.Min(margin, width) &&
            visibleHeight >= Math.Min(margin, height))
        {
            return new System.Windows.Point(x, y);
        }

        return fallback;
    }
    private void LoadWindowState()
    {
        try
        {
            if (!System.IO.File.Exists(windowStateFile))
                return;

            var state = System.Text.Json.JsonSerializer.Deserialize<SavedWindowState>(
                System.IO.File.ReadAllText(windowStateFile));

            if (state == null)
                return;

            if (!double.IsFinite(state.Left) ||
                !double.IsFinite(state.Top) ||
                !double.IsFinite(state.Width) ||
                !double.IsFinite(state.Height))
                return;

            double virtualLeft = SystemParameters.VirtualScreenLeft;
            double virtualTop = SystemParameters.VirtualScreenTop;
            double virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
            double virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;

            if (virtualRight <= virtualLeft || virtualBottom <= virtualTop)
                return;

            double width = Math.Clamp(state.Width, MinWidth,
                Math.Max(MinWidth, SystemParameters.VirtualScreenWidth));
            double height = Math.Clamp(state.Height, MinHeight,
                Math.Max(MinHeight, SystemParameters.VirtualScreenHeight));

            Width = width;
            Height = height;

            // Conserver au moins une partie visible si les ecrans changent.
            const double visibleMargin = 60;

            var recoveredPosition = RecoverWindowPositionV48(
                state.Left, state.Top, width, height);

            Left = recoveredPosition.X;
            Top = recoveredPosition.Y;

            WindowStartupLocation = WindowStartupLocation.Manual;
            manualResize = state.ManualResize;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                "Lecture de la position impossible : " + ex.Message);
        }
    }

    private void SaveWindowState()
    {
        if (!windowStateReady || compactMode || WindowState != WindowState.Normal)
            return;

        try
        {
            if (!double.IsFinite(Left) || !double.IsFinite(Top) ||
                !double.IsFinite(Width) || !double.IsFinite(Height))
                return;

            var state = new SavedWindowState
            {
                Left = Left,
                Top = Top,
                Width = Width,
                Height = Height,
                ManualResize = manualResize
            };

            string? directory = System.IO.Path.GetDirectoryName(windowStateFile);
            if (directory != null)
                System.IO.Directory.CreateDirectory(directory);

            string json = System.Text.Json.JsonSerializer.Serialize(
                state, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

            string temporary = windowStateFile + ".tmp";
            System.IO.File.WriteAllText(temporary, json);
            System.IO.File.Move(temporary, windowStateFile, true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                "Sauvegarde de la position impossible : " + ex.Message);
        }
    }

    private void ScheduleWindowStateSave()
    {
        if (!windowStateReady)
            return;

        windowStateTimer.Stop();
        windowStateTimer.Start();
    }

    private ulong previousIdle, previousKernel, previousUser;
    private bool cpuReady;
    private int tick;
    private int? nvmeTemperature;
    private bool nvmeReading;
    private readonly HashSet<string> nvmeVolumes =
        new(StringComparer.OrdinalIgnoreCase) { "C:" };

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);
    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME { public uint Low; public uint High; public ulong Value => ((ulong)High << 32) | Low; }
    [DllImport("kernel32.dll")]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public uint Length, MemoryLoad;
        public ulong TotalPhys, AvailPhys, TotalPageFile, AvailPageFile, TotalVirtual, AvailVirtual, AvailExtendedVirtual;
    }

    // MainCarouselV1_2026
    // Page 1 : modules historiques.
    // Pages 2 a 5 : emplacements reserves.
    private int carouselPage = 0;
    // Nombre de pages determine par ActiveCarouselPages()

    private void MainCarousel_MouseWheel(
        object sender, MouseWheelEventArgs e)
    {
        if (compactMode)
            return;

        // Conserver le defilement normal de la liste des disques.
        if (StorageScroll.IsMouseOver && carouselPage == 0)
            return;

        if (e.Delta == 0)
            return;

        e.Handled = true;

        int direction = e.Delta > 0 ? -1 : 1;

        int[] activePages = ActiveCarouselPages();
        if (activePages.Length < 2) return;
        int currentIndex = Array.IndexOf(activePages, carouselPage);
        if (currentIndex < 0) currentIndex = 0;
        carouselPage = activePages[
            (currentIndex + direction + activePages.Length) % activePages.Length];

        ApplyCarouselPage();
        if (carouselPage == 1) RefreshNetworkPage();

        var transform = new TranslateTransform();
        CarouselEmptyPage.RenderTransform = transform;

        if (carouselPage != 0)
        {
            var duration = TimeSpan.FromMilliseconds(180);

            transform.BeginAnimation(
                TranslateTransform.YProperty,
                new System.Windows.Media.Animation.DoubleAnimation(
                    direction > 0 ? 9 : -9,
                    0,
                    new Duration(duration)));

            CarouselEmptyPage.BeginAnimation(
                UIElement.OpacityProperty,
                new System.Windows.Media.Animation.DoubleAnimation(
                    0.25,
                    1.0,
                    new Duration(duration)));
        }
    }

    private void ApplyCarouselPage()
    {
        bool showOriginal = carouselPage == 0 || compactMode;

        if (!compactMode)
        {
            Visibility original = showOriginal
                ? Visibility.Visible
                : Visibility.Hidden;

            NormalGauges.Visibility = original;
            CpuGraphPanel.Visibility = original;
            StorageHeading.Visibility = original;
            StorageScroll.Visibility = original;
            FooterPanel.Visibility = original;
        }

        NetworkPage.Visibility = carouselPage == 1
            ? Visibility.Visible : Visibility.Collapsed;

        CarouselEmptyPage.Visibility =
            compactMode || carouselPage == 0
                ? Visibility.Collapsed
                : Visibility.Visible;

        UpdateCarouselDots();
    }
    public MainWindow()
    {
        InitializeComponent();
        MainContentGrid.AddHandler(
            UIElement.PreviewMouseWheelEvent,
            new MouseWheelEventHandler(MainCarousel_MouseWheel),
            true);
        InitializeNetworkPage();
        LoadCarouselPages();
        ScaleContainer.VerticalAlignment = VerticalAlignment.Top;
        Loaded += (_, _) =>
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                InitializeWeatherModule();
                InitializeMediaModule();

                // WeatherGeometryTracking_2026
                // Suivre les changements reels de geometrie du panneau.
                double lastPanelX = double.NaN;
                double lastPanelY = double.NaN;
                double lastPanelWidth = double.NaN;
                double lastPanelHeight = double.NaN;

                ScaleContainer.LayoutUpdated += (_, _) =>
                {
                    if (weatherWindow == null ||
                        !IsLoaded ||
                        WindowState != WindowState.Normal)
                        return;

                    if (ScaleContainer.Child is not Border panel ||
                        panel.ActualWidth <= 0 ||
                        panel.ActualHeight <= 0)
                        return;

                    Point origin = panel.TranslatePoint(
                        new Point(0, 0), this);

                    Point opposite = panel.TranslatePoint(
                        new Point(
                            panel.ActualWidth,
                            panel.ActualHeight), this);

                    double width = opposite.X - origin.X;
                    double height = opposite.Y - origin.Y;

                    if (!double.IsFinite(origin.X) ||
                        !double.IsFinite(origin.Y) ||
                        !double.IsFinite(width) ||
                        !double.IsFinite(height))
                        return;

                    const double epsilon = 0.25;

                    bool changed =
                        Math.Abs(origin.X - lastPanelX) > epsilon ||
                        Math.Abs(origin.Y - lastPanelY) > epsilon ||
                        Math.Abs(width - lastPanelWidth) > epsilon ||
                        Math.Abs(height - lastPanelHeight) > epsilon ||
                        double.IsNaN(lastPanelX);

                    if (!changed)
                        return;

                    lastPanelX = origin.X;
                    lastPanelY = origin.Y;
                    lastPanelWidth = width;
                    lastPanelHeight = height;

                    Dispatcher.BeginInvoke(
                        new Action(() =>
                        {
                            if (IsLoaded &&
                                WindowState == WindowState.Normal)
                                weatherWindow?.FollowParent();
                        }),
                        System.Windows.Threading.DispatcherPriority.ContextIdle);
                };
                // WeatherResizeSync_2026
                SizeChanged += (_, _) =>
                {
                    if (weatherWindow != null)
                    {
                        Dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                weatherWindow?.FollowParent();
                            }),
                            System.Windows.Threading.DispatcherPriority.Render
                        );
                    }
                };
            }), System.Windows.Threading.DispatcherPriority.Background);
        };
        LoadWindowState();
        normalLeft = Left;
        normalTop = Top;
        normalManualResize = manualResize;
        LoadCompactState();
        normalWidth = Width;
        normalHeight = Height;
        normalMinHeight = MinHeight;

        windowStateTimer.Tick += (_, _) =>
        {
            windowStateTimer.Stop();
            SaveWindowState();
            SaveCompactState();
        };

        Loaded += (_, _) =>
        {
            windowStateReady = true;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    startupStorageAnchorReady = true;
                }), DispatcherPriority.ApplicationIdle);
            }), DispatcherPriority.ApplicationIdle);

            if (restoringCompactStartup)
            {
                Compact_Click(CompactButton, new RoutedEventArgs());

                Width = Math.Clamp(compactWidth, MinWidth,
                    Math.Max(MinWidth, SystemParameters.VirtualScreenWidth));
                Height = Math.Clamp(compactHeight, MinHeight,
                    Math.Max(MinHeight, SystemParameters.VirtualScreenHeight));
                RestorePosition(
                    RecoverWindowPositionV48(
                        compactLeft, compactTop, Width, Height).X,
                    RecoverWindowPositionV48(
                        compactLeft, compactTop, Width, Height).Y);

                manualResize = compactManualResize;
                restoringCompactStartup = false;
                SaveCompactState();
            }
        };

        // DpiTransitionSync_V45
        dpiTransitionTimerV45.Tick += (_, _) =>
        {
            SynchronizeSatellitesV45();

            dpiTransitionPassesV45++;

            if (dpiTransitionPassesV45 >= 8)
                dpiTransitionTimerV45.Stop();
        };

        Closed += (_, _) => dpiTransitionTimerV45.Stop();
        SourceInitialized += (_, _) =>
        {
            var handle =
                new System.Windows.Interop.WindowInteropHelper(this).Handle;

            System.Windows.Interop.HwndSource.FromHwnd(handle)
                ?.AddHook(PreventMaximize);

            // DpiTransitionSync_V45
            System.Windows.Interop.HwndSource.FromHwnd(handle)
                ?.AddHook(HandleDpiTransitionV45);
        };

        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
        };

        LocationChanged += (_, _) => ScheduleWindowStateSave();
        SizeChanged += (_, _) => ScheduleWindowStateSave();

        Closing += (_, _) =>
        {
            windowStateTimer.Stop();
            SaveWindowState();
            SaveCompactState();
        };
        Opacity = 1.0;
        LoadOpacity();
        LoadGlassHue();
        LoadTransparentMode();
        Loaded += (_, _) => { UpdateHeaderClock(); UpdateMetrics(); timer.Start(); };
        timer.Tick += (_, _) => { UpdateHeaderClock(); UpdateMetrics(); };
        InitializeBatteryMonitor();
        Closed += (_, _) => hardwareSensors.Close();
        StartNvmeRead();
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is System.Windows.Controls.Slider slider)
        {
            SetBackgroundOpacity(slider.Value / 100.0);
        }
    }
    private readonly string glassHueFile = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LiquidGlassMonitor", "glass-hue.txt");

    private double glassHue = 210;

    private static Color GlassTint(double hue, double saturation, double lightness)
    {
        hue = ((hue % 360) + 360) % 360;
        saturation = Math.Clamp(saturation, 0, 1);
        lightness = Math.Clamp(lightness, 0, 1);

        double chroma = (1 - Math.Abs(2 * lightness - 1)) * saturation;
        double segment = hue / 60;
        double secondary = chroma * (1 - Math.Abs(segment % 2 - 1));
        double offset = lightness - chroma / 2;

        double r = 0, g = 0, b = 0;

        if (segment < 1) { r = chroma; g = secondary; }
        else if (segment < 2) { r = secondary; g = chroma; }
        else if (segment < 3) { g = chroma; b = secondary; }
        else if (segment < 4) { g = secondary; b = chroma; }
        else if (segment < 5) { r = secondary; b = chroma; }
        else { r = chroma; b = secondary; }

        return Color.FromRgb(
            (byte)Math.Round((r + offset) * 255),
            (byte)Math.Round((g + offset) * 255),
            (byte)Math.Round((b + offset) * 255));
    }

    private Color GlassSurfaceColor(byte alpha)
    {
        Color tint = GlassTint(glassHue, 0.23, 0.31);
        return Color.FromArgb(alpha, tint.R, tint.G, tint.B);
    }

    // EnsureMutableGlassBrushes_2026
    private void EnsureMutableGlassBrushes_2026()
    {
        foreach (string key in new[] { "Panel", "SubPanel" })
        {
            if (Resources[key] is not SolidColorBrush original ||
                !original.IsFrozen)
                continue;

            var replacement = original.CloneCurrentValue();
            Resources[key] = replacement;

            // Reconnecter les references StaticResource deja chargees.
            if (key == "Panel")
            {
                if (MainGlassBorder != null &&
                    ReferenceEquals(MainGlassBorder.Background, original))
                    MainGlassBorder.Background = replacement;

                if (SettingsButton != null &&
                    ReferenceEquals(SettingsButton.Tag, original))
                    SettingsButton.Tag = replacement;
            }
            else
            {
                if (CpuGraphPanel != null &&
                    ReferenceEquals(CpuGraphPanel.Background, original))
                    CpuGraphPanel.Background = replacement;

                // Les autres elements XAML peuvent partager SubPanel.
                if (ContentRootGrid != null)
                {
                    var pending = new System.Collections.Generic.Stack<System.Windows.DependencyObject>();
                    pending.Push(ContentRootGrid);

                    while (pending.Count > 0)
                    {
                        var current = pending.Pop();

                        if (current is System.Windows.Controls.Border border &&
                            ReferenceEquals(border.Background, original))
                            border.Background = replacement;

                        int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(current);

                        for (int i = 0; i < count; i++)
                            pending.Push(System.Windows.Media.VisualTreeHelper.GetChild(current, i));
                    }
                }
            }
        }
    }
    // SynchronizeSatelliteBrushesV4
    private void SynchronizeSatelliteBrushesV4()
    {
        if (Resources["Panel"] is not SolidColorBrush brush)
            return;

        weatherWindow?.SynchronizePanelBrush(brush);
        mediaWindow?.SynchronizePanelBrush(brush);
    }
    private void ApplyGlassHue(double hue)
    {
        EnsureMutableGlassBrushes_2026();
        glassHue = Math.Clamp(hue, 0, 360);

        if (Resources["Panel"] is SolidColorBrush panel)
        {
            // Ne modifier que la couleur, jamais l'opacite.
            panel.Color = GlassTint(glassHue, 0.23, 0.215);

            // OptionsLiveColorV4
            // Actualiser uniquement la fenetre des options.
            // Un nouveau pinceau opaque est cree a chaque changement.
            if (settingsWindow?.IsVisible == true)
            {
                RefreshOpaqueSettingsBackground();
            }
            
        }

        if (Resources["SubPanel"] is SolidColorBrush subPanel)
        {
            Color tint = GlassTint(glassHue, 0.23, 0.31);
            subPanel.Color = Color.FromArgb(
                104, tint.R, tint.G, tint.B);
        }

        SynchronizeSatelliteBrushesV4();

        // Mettre a jour les surfaces des disques et appareils deja affiches.
        if (DrivesPanel != null)
        {
            foreach (UIElement child in DrivesPanel.Children)
            {
                if (child is Border border &&
                    border.Tag is string tag &&
                    (tag == "DRIVE" || tag == "MTP") &&
                    border.Background is SolidColorBrush brush)
                {
                    brush.Color = transparentMode ? Colors.Transparent : GlassSurfaceColor(105);
                }
            }
        }
    }

    private void HueSlider_ValueChanged(
        object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is Slider slider)
            ApplyGlassHue(slider.Value);
    }

    private void ResetGlassHue_Click(object sender, RoutedEventArgs e)
    {
        if (HueSlider != null)
            HueSlider.Value = 210;

        ApplyGlassHue(210);
        SaveGlassHue();
    }

    private void LoadGlassHue()
    {
        double hue = 210;

        try
        {
            if (System.IO.File.Exists(glassHueFile) &&
                double.TryParse(
                    System.IO.File.ReadAllText(glassHueFile),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double savedHue) &&
                double.IsFinite(savedHue) &&
                savedHue >= 0 && savedHue <= 360)
            {
                hue = savedHue;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex.Message);
        }

        ApplyGlassHue(hue);

        if (HueSlider != null)
            HueSlider.Value = hue;
    }

    private void SaveGlassHue()
    {
        try
        {
            string? directory = System.IO.Path.GetDirectoryName(glassHueFile);

            if (directory != null)
                System.IO.Directory.CreateDirectory(directory);

            System.IO.File.WriteAllText(
                glassHueFile,
                glassHue.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex.Message);
        }
    }
    private readonly string opacityFile = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LiquidGlassMonitor",
        "opacity.txt"
    );

    private System.Windows.Controls.CheckBox? windowsStartupCheckBox;
    private bool updatingWindowsStartupCheckBox;

    private static string WindowsStartupShortcutPath =>
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            "LiquidGlassMonitor.lnk");

    private static bool IsWindowsStartupEnabled()
    {
        return System.IO.File.Exists(WindowsStartupShortcutPath);
    }

    private static void SetWindowsStartupEnabled(bool enabled)
    {
        string shortcut = WindowsStartupShortcutPath;

        if (!enabled)
        {
            if (System.IO.File.Exists(shortcut))
                System.IO.File.Delete(shortcut);
            return;
        }

        string executable = Environment.ProcessPath
            ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
            ?? throw new InvalidOperationException(
                "Impossible de trouver l'executable.");

        string? directory = System.IO.Path.GetDirectoryName(executable);

        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException(
                "Dossier de l'executable introuvable.");

        System.IO.Directory.CreateDirectory(
            System.IO.Path.GetDirectoryName(shortcut)!);

        Type shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException(
                "Windows Script Host indisponible.");

        object shell = Activator.CreateInstance(shellType)
            ?? throw new InvalidOperationException(
                "Creation du raccourci impossible.");

        try
        {
            dynamic link = ((dynamic)shell).CreateShortcut(shortcut);
            link.TargetPath = executable;
            link.WorkingDirectory = directory;
            link.Description = "Demarrage automatique de LiquidGlassMonitor";
            link.Save();
        }
        finally
        {
            if (System.Runtime.InteropServices.Marshal.IsComObject(shell))
                System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);
        }
    }

    private void SetupWindowsStartupOption()
    {
        if (SettingsButton.ContextMenu is not { } menu)
            return;

        if (windowsStartupCheckBox == null)
        {
            System.Windows.Controls.StackPanel? panel = null;

            foreach (object item in menu.Items)
            {
                if (item is System.Windows.Controls.MenuItem menuItem &&
                    menuItem.Header is System.Windows.Controls.StackPanel stack)
                {
                    panel = stack;
                    break;
                }
            }

            if (panel == null)
            {
                System.Diagnostics.Debug.WriteLine(
                    "Panneau des parametres introuvable.");
                return;
            }

            var checkbox = new System.Windows.Controls.CheckBox
            {
                Content = "Demarrer avec Windows",
                Margin = new Thickness(0, 18, 0, 4),
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 12,
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "Lancer LiquidGlassMonitor a l'ouverture de session"
            };

            checkbox.Checked += WindowsStartupCheckBox_Changed;
            checkbox.Unchecked += WindowsStartupCheckBox_Changed;

            panel.Children.Add(checkbox);
            windowsStartupCheckBox = checkbox;
        }

        updatingWindowsStartupCheckBox = true;
        try
        {
            windowsStartupCheckBox.IsChecked = IsWindowsStartupEnabled();
        }
        finally
        {
            updatingWindowsStartupCheckBox = false;
        }
    }

    private void WindowsStartupCheckBox_Changed(
        object sender, RoutedEventArgs e)
    {
        if (updatingWindowsStartupCheckBox ||
            windowsStartupCheckBox == null)
            return;

        bool enabled = windowsStartupCheckBox.IsChecked == true;

        try
        {
            SetWindowsStartupEnabled(enabled);
        }
        catch (Exception ex)
        {
            updatingWindowsStartupCheckBox = true;
            windowsStartupCheckBox.IsChecked = IsWindowsStartupEnabled();
            updatingWindowsStartupCheckBox = false;

            System.Windows.MessageBox.Show(
                "Impossible de modifier le demarrage automatique :\n" +
                ex.Message,
                "LiquidGlassMonitor",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
    private WeatherWindow? weatherWindow;
    private System.Windows.Controls.CheckBox? weatherCheckBox;
    private bool updatingWeatherCheckBox;

    private void InitializeWeatherModule()
    {
        weatherWindow = new WeatherWindow(this);
        weatherWindow.SetTransparentMode(transparentMode);

        // WeatherStartupFix_2026
        // Appliquer les preferences des la creation du module.
        // Ne pas attendre un nouvel evenement Loaded.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (weatherWindow == null)
                return;

            weatherWindow.SetCompact(
                compactMode || WindowState != WindowState.Normal);

            weatherWindow.SetEnabled(weatherWindow.Enabled);

            weatherWindow.FollowParent();
        }), System.Windows.Threading.DispatcherPriority.Loaded);

        LocationChanged += (_, _) => weatherWindow?.FollowParent();
        SizeChanged += (_, _) => weatherWindow?.FollowParent();

        StateChanged += (_, _) =>
        {
            weatherWindow?.SetCompact(
                compactMode || WindowState != WindowState.Normal);
        };

        Loaded += (_, _) =>
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                weatherWindow?.SetCompact(compactMode);
                if (weatherWindow != null)
                    weatherWindow.SetEnabled(weatherWindow.Enabled);
            }), System.Windows.Threading.DispatcherPriority.Background);
        };

        Closed += (_, _) => weatherWindow?.Close();
    }

    private void SetupWeatherOption()
    {
        if (SettingsButton.ContextMenu is not { } menu ||
            weatherWindow == null)
            return;

        if (weatherCheckBox == null)
        {
            System.Windows.Controls.StackPanel? panel = null;

            foreach (object item in menu.Items)
            {
                if (item is System.Windows.Controls.MenuItem mi &&
                    mi.Header is System.Windows.Controls.StackPanel stack)
                {
                    panel = stack;
                    break;
                }
            }

            if (panel == null) return;

            var checkbox = new System.Windows.Controls.CheckBox
            {
                Content = "Afficher la meteo",
                Margin = new Thickness(0, 12, 0, 4),
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 12,
                Cursor = System.Windows.Input.Cursors.Hand
            };

            checkbox.Checked += WeatherOptionChanged;
            checkbox.Unchecked += WeatherOptionChanged;

            panel.Children.Add(checkbox);
            weatherCheckBox = checkbox;
        }

        updatingWeatherCheckBox = true;
        weatherCheckBox.IsChecked = weatherWindow.Enabled;
        updatingWeatherCheckBox = false;
    }

    private void WeatherOptionChanged(object sender, RoutedEventArgs e)
    {
        if (updatingWeatherCheckBox ||
            weatherWindow == null ||
            weatherCheckBox == null)
            return;

        weatherWindow.SetEnabled(
            weatherCheckBox.IsChecked == true);
    }
    // MediaPhase2_2026
    private MediaWindow? mediaWindow;
    private System.Windows.Controls.CheckBox? mediaCheckBox;
    private bool updatingMediaCheckBox;

    public bool IsMediaCompactMode()
    {
        return compactMode;
    }

    private void InitializeMediaModule()
    {
        if (mediaWindow != null)
            return;

        mediaWindow = new MediaWindow(this);
        mediaWindow.SetTransparentMode(transparentMode);

        LocationChanged += (_, _) => mediaWindow?.FollowParent();

        SizeChanged += (_, _) =>
        {
            Dispatcher.BeginInvoke(
                new Action(() => mediaWindow?.FollowParent()),
                System.Windows.Threading.DispatcherPriority.Render);
        };

        StateChanged += (_, _) =>
        {
            mediaWindow?.SetCompact(
                compactMode || WindowState != WindowState.Normal);
        };

        Closed += (_, _) => mediaWindow?.Close();

        mediaWindow.SetCompact(
            compactMode || WindowState != WindowState.Normal);

        mediaWindow.Start();
    }

    private void SetupMediaOption()
    {
        if (SettingsButton.ContextMenu is not { } menu ||
            mediaWindow == null)
            return;

        if (mediaCheckBox == null)
        {
            System.Windows.Controls.StackPanel? panel = null;

            foreach (object item in menu.Items)
            {
                if (item is System.Windows.Controls.MenuItem mi &&
                    mi.Header is System.Windows.Controls.StackPanel stack)
                {
                    panel = stack;
                    break;
                }
            }

            if (panel == null)
                return;

            var checkbox = new System.Windows.Controls.CheckBox
            {
                Content = "Afficher le lecteur multimedia",
                Margin = new Thickness(0, 8, 0, 4),
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 12,
                Cursor = System.Windows.Input.Cursors.Hand
            };

            checkbox.Checked += MediaOptionChanged;
            checkbox.Unchecked += MediaOptionChanged;

            panel.Children.Add(checkbox);
            mediaCheckBox = checkbox;
        }

        updatingMediaCheckBox = true;
        mediaCheckBox.IsChecked = mediaWindow.Enabled;
        updatingMediaCheckBox = false;
    }

    private void MediaOptionChanged(object sender, RoutedEventArgs e)
    {
        if (updatingMediaCheckBox ||
            mediaWindow == null ||
            mediaCheckBox == null)
            return;

        mediaWindow.SetEnabled(mediaCheckBox.IsChecked == true);
    }
    // TransparentModeOption_2026
    private bool transparentMode;
    private Effect? originalGlassEffect;

    private readonly string transparentModeFile = System.IO.Path.Combine(
        Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),
        "LiquidGlassMonitor", "transparent-mode.txt");

    // TransparentModulesV2_2026
    private void AnimateTransparentHeader(bool hovering)
    {
        // HeaderHoverFinal_2026
        if (HeaderActions == null) return;

        double target = hovering ? 1.0 : 0.0;

        HeaderActions.IsHitTestVisible = true;

        HeaderActions.BeginAnimation(
            UIElement.OpacityProperty,
            new System.Windows.Media.Animation.DoubleAnimation
            {
                To = target,
                Duration = TimeSpan.FromMilliseconds(hovering ? 150 : 200),
                FillBehavior = System.Windows.Media.Animation.FillBehavior.HoldEnd
            });
    }

    // GlobalHeaderHoverWPF_2026

    [System.Runtime.InteropServices.StructLayout(
        System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct HeaderCursorPoint
    {
        public int X;
        public int Y;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(
        System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetCursorPos(
        out HeaderCursorPoint point);

    private readonly System.Windows.Threading.DispatcherTimer
        globalHeaderHoverTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(80)
        };

    private bool globalHeaderHoverInitialized = false;

    private void InitializeGlobalHeaderHover()
    {
        if (globalHeaderHoverInitialized)
            return;

        globalHeaderHoverInitialized = true;

        globalHeaderHoverTimer.Tick += (_, _) =>
        {
            if (HeaderActions == null ||
                !HeaderActions.IsVisible ||
                HeaderActions.ActualWidth <= 0 ||
                HeaderActions.ActualHeight <= 0)
                return;

            if (!GetCursorPos(out HeaderCursorPoint mouse))
                return;

            var topLeft = HeaderActions.PointToScreen(
                new System.Windows.Point(0, 0));

            var bottomRight = HeaderActions.PointToScreen(
                new System.Windows.Point(
                    HeaderActions.ActualWidth,
                    HeaderActions.ActualHeight));

            bool inside =
                mouse.X >= topLeft.X &&
                mouse.X <= bottomRight.X &&
                mouse.Y >= topLeft.Y &&
                mouse.Y <= bottomRight.Y;

            if (inside == headerButtonsHovering)
                return;

            headerButtonsHovering = inside;
            AnimateTransparentHeader(inside);
        };

        globalHeaderHoverTimer.Start();
    }
    // HeaderPositionHover_2026
    private bool headerButtonsHovering = false;

    private void Header_PositionMouseMove(
        object sender, MouseEventArgs e)
    {
        if (HeaderActions == null || MainContentGrid == null)
            return;

        var position = e.GetPosition(HeaderActions);

        bool inside =
            position.X >= 0 &&
            position.Y >= 0 &&
            position.X <= HeaderActions.ActualWidth &&
            position.Y <= HeaderActions.ActualHeight;

        if (inside == headerButtonsHovering)
            return;

        headerButtonsHovering = inside;
        AnimateTransparentHeader(inside);
    }

    private void Header_PositionMouseLeave(
        object sender, MouseEventArgs e)
    {
        if (!headerButtonsHovering)
            return;

        headerButtonsHovering = false;
        AnimateTransparentHeader(false);
    }
    private void Header_TransparentMouseEnter(
        object sender, MouseEventArgs e)
    {
        AnimateTransparentHeader(true);
    }

    private void Header_TransparentMouseLeave(
        object sender, MouseEventArgs e)
    {
        AnimateTransparentHeader(false);
    }
    private void ApplyTransparentMode()
    {
        if (MainGlassBorder == null || CpuGraphPanel == null)
            return;

        originalGlassEffect ??= MainGlassBorder.Effect;

        InitializeGlobalHeaderHover();
        AnimateTransparentHeader(headerButtonsHovering);

        weatherWindow?.SetTransparentMode(transparentMode);
        mediaWindow?.SetTransparentMode(transparentMode);

        // TransparentDragSurface_2026
        // Surface presque invisible mais detectee par Windows.
        MainGlassBorder.Background = transparentMode
            ? new SolidColorBrush(Color.FromArgb(1, 0, 0, 0))
            : (Brush)Resources["Panel"];

        MainGlassBorder.BorderThickness = transparentMode
            ? new Thickness(0)
            : new Thickness(1);

        MainGlassBorder.Effect = transparentMode
            ? null : originalGlassEffect;

        CpuGraphPanel.Background = transparentMode
            ? Brushes.Transparent
            : (Brush)Resources["SubPanel"];

        if (DrivesPanel != null)
        {
            foreach (UIElement child in DrivesPanel.Children)
            {
                if (child is Border border &&
                    border.Tag is string tag &&
                    (tag == "DRIVE" || tag == "MTP"))
                {
                    border.Background = new SolidColorBrush(
                        transparentMode
                            ? Colors.Transparent
                            : GlassSurfaceColor(105));
                }
            }
        }
    }

    private void LoadTransparentMode()
    {
        try
        {
            transparentMode =
                System.IO.File.Exists(transparentModeFile) &&
                System.IO.File.ReadAllText(transparentModeFile)
                    .Trim()
                    .Equals("true",
                        StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            transparentMode = false;
        }

        ApplyTransparentMode();
    }

    private void SaveTransparentMode()
    {
        try
        {
            string? dir =
                System.IO.Path.GetDirectoryName(transparentModeFile);

            if (dir != null)
                System.IO.Directory.CreateDirectory(dir);

            System.IO.File.WriteAllText(
                transparentModeFile,
                transparentMode ? "true" : "false");
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }
    }

    private void SetupTransparentModeOption()
    {
        if (SettingsButton.ContextMenu is not { } menu)
            return;

        StackPanel? panel = null;

        foreach (object item in menu.Items)
        {
            if (item is MenuItem mi &&
                mi.Header is StackPanel stack)
            {
                panel = stack;
                break;
            }
        }

        if (panel == null)
            return;

        foreach (UIElement child in panel.Children)
        {
            if (child is CheckBox cb &&
                cb.Name == "TransparentModeCheckBox")
            {
                cb.IsChecked = transparentMode;
                return;
            }
        }

        var option = new CheckBox
        {
            Name = "TransparentModeCheckBox",
            Content = "Transparence totale (sans fond)",
            IsChecked = transparentMode,
            Foreground = Brushes.White,
            FontSize = 12,
            Margin = new Thickness(0, 15, 0, 4),
            Cursor = Cursors.Hand,
            ToolTip = "Afficher uniquement les informations et jauges"
        };

        option.Checked += (_, _) =>
        {
            transparentMode = true;
            ApplyTransparentMode();
            SaveTransparentMode();
        };

        option.Unchecked += (_, _) =>
        {
            transparentMode = false;
            ApplyTransparentMode();
            SaveTransparentMode();
        };

        panel.Children.Add(option);
    }
    // LanguageSelector_2026
    private string currentLanguage = "fr";

    private string LanguageFilePath =>
        System.IO.Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "LiquidGlassMonitor",
            "language.txt");

    private void LoadLanguage()
    {
        try
        {
            string path = LanguageFilePath;
            if (System.IO.File.Exists(path))
            {
                string value = System.IO.File.ReadAllText(path).Trim();
                currentLanguage = value == "en" ? "en" : "fr";
            }
        }
        catch
        {
            currentLanguage = "fr";
        }
    }

    private void SaveLanguage()
    {
        try
        {
            string path = LanguageFilePath;
            System.IO.Directory.CreateDirectory(
                System.IO.Path.GetDirectoryName(path)!);
            System.IO.File.WriteAllText(path, currentLanguage);
        }
        catch
        {
        }
    }

    private string Translate(string french, string english)
    {
        return currentLanguage == "en" ? english : french;
    }

    // ApplyMainPanelLanguage_2026
    private void ApplyMainPanelLanguage()
    {
        // DynamicLanguage_2026
        NetworkConnectionLanguageLabel.Text =
            Translate("CONNEXION ACTIVE : ", "ACTIVE CONNECTION: ");

        NetworkDownloadLanguageLabel.Text =
            Translate("↓ RÉCEPTION", "↓ DOWNLOAD");

        NetworkUploadLanguageLabel.Text =
            Translate("↑ ENVOI", "↑ UPLOAD");

        NetworkDownloadLegendLanguageLabel.Text =
            Translate("● Réception", "● Download");

        NetworkUploadLegendLanguageLabel.Text =
            Translate("● Envoi", "● Upload");

        NetworkLocalIpLanguageLabel.Text =
            Translate("IP locale", "Local IP");

        NetworkLatencyLanguageLabel.Text =
            Translate("Latence Internet", "Internet latency");
        CpuActivityLanguageLabel.Text =
            Translate("ACTIVITÉ CPU", "CPU ACTIVITY");

        CpuDurationLanguageLabel.Text =
            Translate("60 secondes", "60 seconds");

        StorageLanguageLabel.Text =
            Translate("STOCKAGE", "STORAGE");

        DrivesLanguageLabel.Text =
            Translate("Volumes détectés", "Detected drives");

        RefreshLanguageLabel.Text =
            Translate("Actualisation : 1 s", "Refresh: 1 s");

        NetworkLanguageLabel.Text =
            Translate("RÉSEAU", "NETWORK");

        NetworkTrafficLanguageLabel.Text =
            Translate("TRAFIC — 60 SECONDES", "TRAFFIC — 60 SECONDS");

        if (settingsWindow?.Content is Border settingsBorder &&
            settingsBorder.Child is StackPanel settingsPanel)
        {
            foreach (UIElement element in settingsPanel.Children)
            {
                if (element is TextBlock heading &&
                    (heading.Text == "PAGES DU CARROUSEL" ||
                     heading.Text == "CAROUSEL PAGES"))
                {
                    heading.Text =
                        Translate("PAGES DU CARROUSEL", "CAROUSEL PAGES");
                }

                if (element is CheckBox checkbox)
                {
                    if (checkbox.Name == "CarouselNetworkCheckBox")
                    {
                        checkbox.Content =
                            Translate("Réseau", "Network");
                    }
                    else if (checkbox.Content is string content &&
                             (content == "Système (toujours actif)" ||
                              content == "System (always active)"))
                    {
                        checkbox.Content =
                            Translate(
                                "Système (toujours actif)",
                                "System (always active)");
                    }
                }
            }
        }
    }
    // ApplySettingsLanguage_2026
    private void ApplySettingsLanguage()
    {
        UpdateHeaderClock(); // DateLanguageV52
        ApplyMainPanelLanguage();
        weatherWindow?.SetLanguage(currentLanguage);
        OpacityLanguageLabel.Text =
            Translate("OPACITE", "OPACITY");

        GlassTintLanguageLabel.Text =
            Translate("TEINTE DU VERRE", "GLASS TINT");

        ResetGlassHueLanguageButton.Content =
            Translate("Couleur d'origine", "Original color");

        SettingsButton.ToolTip =
            Translate("Personnalisation", "Customization");

        if (windowsStartupCheckBox != null)
        {
            windowsStartupCheckBox.Content =
                Translate("Demarrer avec Windows", "Start with Windows");

            windowsStartupCheckBox.ToolTip =
                Translate(
                    "Lancer LiquidGlassMonitor a l'ouverture de session",
                    "Launch LiquidGlassMonitor when signing in");
        }

        if (weatherCheckBox != null)
        {
            weatherCheckBox.Content =
                Translate("Afficher la meteo", "Show weather");
        }

        if (mediaCheckBox != null)
        {
            mediaCheckBox.Content =
                Translate(
                    "Afficher le lecteur multimedia",
                    "Show media player");
        }

        if (settingsWindow?.Content is Border border &&
            border.Child is StackPanel panel)
        {
            foreach (var checkbox in panel.Children.OfType<CheckBox>())
            {
                if (checkbox.Name == "TransparentModeCheckBox")
                {
                    checkbox.Content =
                        Translate(
                            "Transparence totale (sans fond)",
                            "Full transparency (no background)");

                    checkbox.ToolTip =
                        Translate(
                            "Afficher uniquement les informations et jauges",
                            "Show only information and gauges");
                }
            }
        }
    }
    private void AddLanguageSelector(StackPanel panel)
    {
var container = new Border
        {
            Width = 110,
            Height = 25,
            Background = new SolidColorBrush(
                Color.FromArgb(85, 38, 53, 69)),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(3),
            Margin = new Thickness(0, 5, 0, 0)
        };

        var grid = new Grid();

        grid.ColumnDefinitions.Add(
            new ColumnDefinition());

        grid.ColumnDefinitions.Add(
            new ColumnDefinition());

        var frenchButton = new Button
        {
            Content = "Français",
            Tag = "fr",
            FontSize = 10,
            FontWeight = FontWeights.Medium,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand
        };

        var englishButton = new Button
        {
            Content = "English",
            Tag = "en",
            FontSize = 10,
            FontWeight = FontWeights.Medium,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand
        };

        var buttonStyle = new Style(typeof(Button));

        var template = new ControlTemplate(typeof(Button));

        var border = new FrameworkElementFactory(typeof(Border));

        border.SetBinding(
            Border.BackgroundProperty,
            new System.Windows.Data.Binding("Background")
            {
                RelativeSource =
                    new System.Windows.Data.RelativeSource(
                        System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });

        border.SetValue(
            Border.CornerRadiusProperty,
            new CornerRadius(7));

        var content = new FrameworkElementFactory(
            typeof(ContentPresenter));

        content.SetValue(
            ContentPresenter.HorizontalAlignmentProperty,
            HorizontalAlignment.Center);

        content.SetValue(
            ContentPresenter.VerticalAlignmentProperty,
            VerticalAlignment.Center);

        border.AppendChild(content);
        template.VisualTree = border;

        buttonStyle.Setters.Add(
            new Setter(Button.TemplateProperty, template));

        frenchButton.Style = buttonStyle;
        englishButton.Style = buttonStyle;

        void RefreshButtons()
        {
            var active = new SolidColorBrush(
                Color.FromArgb(125, 73, 99, 123));

            var inactive = Brushes.Transparent;

            frenchButton.Background =
                currentLanguage == "fr" ? active : inactive;

            englishButton.Background =
                currentLanguage == "en" ? active : inactive;

            frenchButton.Foreground =
                currentLanguage == "fr"
                    ? Brushes.White
                    : new SolidColorBrush(
                        Color.FromRgb(170, 188, 205));

            englishButton.Foreground =
                currentLanguage == "en"
                    ? Brushes.White
                    : new SolidColorBrush(
                        Color.FromRgb(170, 188, 205));
        }

        frenchButton.Click += (_, _) =>
        {
            currentLanguage = "fr";
            SaveLanguage();
            RefreshButtons();
            ApplySettingsLanguage();
        };

        englishButton.Click += (_, _) =>
        {
            currentLanguage = "en";
            SaveLanguage();
            RefreshButtons();
            ApplySettingsLanguage();
        };

        Grid.SetColumn(frenchButton, 0);
        Grid.SetColumn(englishButton, 1);

        grid.Children.Add(frenchButton);
        grid.Children.Add(englishButton);

        container.Child = grid;


        panel.Children.Add(container);

        RefreshButtons();
    }
    private Window? settingsWindow;
    private bool settingsWindowClosing;

        // GlassSettingsOpaqueV3
    private void RefreshOpaqueSettingsBackground()
    {
        if (settingsWindow?.Content is not Border settingsBorder)
            return;

        if (Resources["Panel"] is not SolidColorBrush panel)
            return;

        Color color = panel.Color;

        settingsBorder.Background = new SolidColorBrush(
            Color.FromRgb(color.R, color.G, color.B))
        {
            Opacity = 1.0
        };
    }
    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (locked) return;

        if (settingsWindow != null)
        {
            if (settingsWindow.IsVisible)
                settingsWindow.Hide();
            else
            {
                PositionSettingsWindow();
                RefreshOpaqueSettingsBackground();
                settingsWindow.Show();
                PositionSettingsWindow();
            }
            return;
        }

        SetupWindowsStartupOption();
        SetupWeatherOption();
        SetupMediaOption();
        LoadHeaderVisibility();
        SetupHeaderVisibilityOptions();
        SetupTransparentModeOption();
        SetupCarouselPagesOption();
        LoadLanguage();

        if (SettingsButton.ContextMenu is not { } menu)
            return;

        StackPanel? panel = null;
        MenuItem? container = null;

        foreach (object item in menu.Items)
        {
            if (item is MenuItem mi &&
                mi.Header is StackPanel stack)
            {
                container = mi;
                panel = stack;
                break;
            }
        }

        if (panel == null || container == null)
            return;

        // Detacher les controles existants sans les recreer.
        // CorrectionCouleursParametres_2026
        // Les TextBlock deplaces hors du ContextMenu ne
        // recuperent plus automatiquement le style du widget.
        var couleurTexteParametres =
            new SolidColorBrush(Color.FromRgb(240, 245, 252));

        foreach (TextBlock texte in panel.Children.OfType<TextBlock>())
        {
            if (texte.ReadLocalValue(TextBlock.ForegroundProperty)
                == DependencyProperty.UnsetValue)
            {
                texte.Foreground = couleurTexteParametres;
            }
        }
        AddLanguageSelector(panel);
        container.Header = null;
        SettingsButton.ContextMenu = null;

        var border = new Border
        {
            Background = new SolidColorBrush(
                (Resources["Panel"] as SolidColorBrush)?.Color
                    ?? Color.FromRgb(42, 52, 67))
            {
                Opacity = 1.0
            },
            BorderBrush = new SolidColorBrush(
                Color.FromArgb(96, 126, 149, 170)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(17),
            Padding = new Thickness(15),
            Child = panel
        };

        settingsWindow = new Window
        {
            Owner = this,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            ShowActivated = false,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Content = border
        };

        settingsWindow.Closing += (_, args) =>
        {
            if (!settingsWindowClosing)
            {
                args.Cancel = true;
                settingsWindow.Hide();
            }
        };

        LocationChanged += (_, _) => PositionSettingsWindow();
        SizeChanged += (_, _) => PositionSettingsWindow();

        Closed += (_, _) =>
        {
            settingsWindowClosing = true;
            settingsWindow?.Close();
        };

        RefreshOpaqueSettingsBackground();
                settingsWindow.Show();
        ApplySettingsLanguage();
        settingsWindow.UpdateLayout();
        PositionSettingsWindow();
    }

    // OptionsMultiDpi_V42
    private void PositionSettingsWindow()
    {
        if (settingsWindow == null ||
            !settingsWindow.IsVisible ||
            !MainGlassBorder.IsLoaded ||
            MainGlassBorder.ActualWidth <= 0 ||
            MainGlassBorder.ActualHeight <= 0 ||
            settingsWindow.ActualWidth <= 0 ||
            settingsWindow.ActualHeight <= 0)
            return;

        var topLeft = MainGlassBorder.PointToScreen(
            new System.Windows.Point(0, 0)
        );

        var bottomRight = MainGlassBorder.PointToScreen(
            new System.Windows.Point(
                MainGlassBorder.ActualWidth,
                MainGlassBorder.ActualHeight
            )
        );

        var dpi = SatellitePositionV44.Dpi(settingsWindow);

        double widthPx =
            settingsWindow.ActualWidth * dpi.x;

        double heightPx =
            settingsWindow.ActualHeight * dpi.y;

        double x = topLeft.X - widthPx - 12;
        double y = bottomRight.Y - heightPx;

        var safe = SatellitePositionV44.KeepOnScreen(
            x,
            y,
            widthPx,
            heightPx,
            topLeft.X,
            topLeft.Y
        );

        SatellitePositionV44.Move(
            settingsWindow,
            safe.x,
            safe.y
        );
    }    private void SetBackgroundOpacity(double value)
    {
        EnsureMutableGlassBrushes_2026();
        if (Resources["Panel"] is SolidColorBrush brush)
        {
            brush.Opacity = Math.Clamp(value, 0.30, 1.0);
        SynchronizeSatelliteBrushesV4();
        }
    }
    private void LoadOpacity()
    {
        try
        {
            if (System.IO.File.Exists(opacityFile) &&
                double.TryParse(
                    System.IO.File.ReadAllText(opacityFile),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double value))
            {
                SetBackgroundOpacity(value);
            }
            else
            {
                SetBackgroundOpacity(0.85);
            }
        }
        catch
        {
            SetBackgroundOpacity(0.85);
        }
    }

    private void SaveOpacity()
    {
        try
        {
            string? directory = System.IO.Path.GetDirectoryName(opacityFile);

            if (directory != null)
                System.IO.Directory.CreateDirectory(directory);

            System.IO.File.WriteAllText(
                opacityFile,
                ((SolidColorBrush)Resources["Panel"]).Opacity.ToString(System.Globalization.CultureInfo.InvariantCulture)
            );
        }
        catch
        {
            // Le widget reste utilisable si la sauvegarde échoue.
        }
    }
    private void DeviceChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            RefreshStorageWithAnchoredBottom();
        }), System.Windows.Threading.DispatcherPriority.Background);
    }
    private void Widget_PreviewMouseLeftButtonDown(
        object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        if (locked ||
            e.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
            return;

        if (e.OriginalSource is not System.Windows.DependencyObject source)
            return;

        System.Windows.DependencyObject? current = source;

        while (current != null)
        {
            if (current is System.Windows.Controls.Button ||
                current is System.Windows.Controls.Primitives.RangeBase ||
                current is System.Windows.Controls.TextBlock ||
                current is System.Windows.Controls.TextBox ||
                current is System.Windows.Controls.Primitives.Thumb ||
                current is System.Windows.Controls.Primitives.ScrollBar ||
                current is System.Windows.Controls.ScrollViewer)
            {
                return;
            }

            if (current is System.Windows.FrameworkElement element)
            {
                if (element.Tag is string tag &&
                    (tag == "DRIVE" || tag == "MTP"))
                    return;

                if (element.Name == "ResizeHandle")
                    return;
            }

            if (current == this)
                break;

            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        MoveWindowFreely();
        e.Handled = true;
    }
    // FreeWindowDrag_2026
    // Laisser Windows conserver la position finale du deplacement.
    private void MoveWindowFreely()
    {
        if (locked || WindowState != WindowState.Normal)
            return;

        try
        {
            DragMove();
        }
        finally
        {
            // Enregistrer la position choisie par l'utilisateur.
            ScheduleWindowStateSave();

            // Repositionner les modules satellites sans modifier
            // les coordonnees du panneau principal.
            weatherWindow?.FollowParent();
            mediaWindow?.FollowParent();
        }
    }
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!locked && e.LeftButton == MouseButtonState.Pressed && e.OriginalSource is not Button) MoveWindowFreely();
    }
    private void Lock_Click(object sender, RoutedEventArgs e)
    {
        locked = !locked;

        if (locked && SettingsButton.ContextMenu is { } menu)
            menu.IsOpen = false;

        // Seul le bouton de verrouillage reste visible en mode verrouille.
        SettingsButton.Visibility = locked
            ? Visibility.Collapsed
            : Visibility.Visible;

        CompactButton.Visibility = locked
            ? Visibility.Collapsed
            : Visibility.Visible;

        CloseButton.Visibility = locked
            ? Visibility.Collapsed
            : Visibility.Visible;

        LockButton.Content = locked ? "🔒" : "🔓";
        LockButton.ToolTip = locked ? "Déverrouiller la position" : "Verrouiller la position";
        // Masquer la poignee lorsque le widget est verrouille.
        ResizeHandle.Visibility = locked
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(
        IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private void ResizeHandle_MouseLeftButtonDown(
        object sender, MouseButtonEventArgs e)
    {
        if (locked) return;
        if (e.LeftButton != MouseButtonState.Pressed)
            return;

        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;

        manualResize = true;
        ReleaseCapture();

        const uint WM_NCLBUTTONDOWN = 0x00A1;
        const int HTBOTTOMRIGHT = 17;

        SendMessage(
            handle,
            WM_NCLBUTTONDOWN,
            new IntPtr(HTBOTTOMRIGHT),
            IntPtr.Zero);

        e.Handled = true;
    }
    private void Close_Click(object sender, RoutedEventArgs e)
    {
        if (locked) return;
        Close();
    }
    protected override void OnClosed(EventArgs e) { SaveGlassHue(); SaveOpacity(); base.OnClosed(e); }
    private void Graph_SizeChanged(object sender, SizeChangedEventArgs e) => RenderGraph();
    private static void SetRing(Ellipse ring, double percent)
    {
        // WPF ellipse dash lengths are multiples of stroke width; circumference ~= 2*pi*44/9 = 30.72.
        double perimeter = 2 * Math.PI * 44 / 9;
        ring.StrokeDashArray = new DoubleCollection { perimeter * Math.Clamp(percent, 0, 100) / 100, perimeter };
    }
    private double ReadCpu()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user)) return 0;
        ulong i = idle.Value, k = kernel.Value, u = user.Value;
        double result = 0;
        if (cpuReady)
        {
            ulong total = (k - previousKernel) + (u - previousUser);
            ulong idleDelta = i - previousIdle;
            if (total > 0) result = 100.0 * (1.0 - Math.Min(1.0, (double)idleDelta / total));
        }
        previousIdle = i; previousKernel = k; previousUser = u; cpuReady = true;
        return Math.Clamp(result, 0, 100);
    }
    private void UpdateMetrics()
    {
        try
        {
            double cpu = ReadCpu();
            CpuValue.Text = $"{cpu:0}%";
            SetRing(CpuRing, cpu);

            history.Enqueue(cpu);
            while (history.Count > 60)
                history.Dequeue();

            RenderGraph();

            var memory = new MEMORYSTATUSEX
            {
                Length = (uint)Marshal.SizeOf<MEMORYSTATUSEX>()
            };

            if (GlobalMemoryStatusEx(ref memory) && memory.TotalPhys > 0)
            {
                double used = 100.0 *
                    (1.0 - (double)memory.AvailPhys / memory.TotalPhys);

                RamValue.Text = $"{used:0}%";
                SetRing(RamRing, used);
            }

            if (++tick % 3 == 1)
{
    RefreshStorageWithAnchoredBottom();
}

            double? gpu = hardwareSensors.ReadGpu();

            if (gpu.HasValue)
            {
                GpuValue.Text = $"{gpu.Value:0}%";
                SetRing(GpuRing, gpu.Value);
            }
            else
            {
                GpuValue.Text = "N/D";
                SetRing(GpuRing, 0);
            }
        }
        catch (Exception ex)
        {
            StatusText.Text =
                $"Mesure indisponible : {ex.GetType().Name}";
        }
    }
    private void RenderGraph()
    {
        if (CpuGraph == null || GraphCanvas == null) return;
        double width = GraphCanvas.ActualWidth;
        double height = GraphCanvas.ActualHeight;
        if (width <= 0 || height <= 0) return;
        var values = history.ToArray();
        var points = new PointCollection();
        for (int i = 0; i < values.Length; i++)
            points.Add(new Point(i * width / 59.0, height - values[i] * (height - 5) / 100.0));
        CpuGraph.Points = points;
    }
    private string LocalizedSize(long bytes)
    {
        var culture = currentLanguage == "en"
            ? System.Globalization.CultureInfo.GetCultureInfo("en-US")
            : System.Globalization.CultureInfo.GetCultureInfo("fr-FR");

        if (bytes >= (1L << 40))
        {
            return (bytes / (double)(1L << 40)).ToString("0.##", culture)
                + Translate(" To", " TB");
        }

        return (bytes / (double)(1L << 30)).ToString("0.#", culture)
            + Translate(" Go", " GB");
    }
    private static string Size(long bytes) => bytes >= 1L << 40 ? $"{bytes / (double)(1L << 40):0.##} To" : $"{bytes / (double)(1L << 30):0.#} Go";
    private static void OpenStorageInExplorer(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "\"" + path + "\"",
                    UseShellExecute = true
                });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                "Impossible d'ouvrir ce stockage : " + ex.Message,
                "LiquidGlassMonitor",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }

    private static string? FindMtpExplorerPath(string deviceName)
    {
        Type? shellType = Type.GetTypeFromProgID(
            "Shell.Application");

        if (shellType == null)
            return null;

        object? shellObject = Activator.CreateInstance(shellType);

        if (shellObject == null)
            return null;

        try
        {
            dynamic shell = shellObject;
            dynamic computer = shell.NameSpace(17);

            if (computer == null)
                return null;

            foreach (dynamic device in computer.Items())
            {
                try
                {
                    string name =
                        Convert.ToString(device.Name) ?? "";

                    string path =
                        Convert.ToString(device.Path) ?? "";

                    if (string.Equals(
                            name,
                            deviceName,
                            StringComparison.OrdinalIgnoreCase)
                        && path.Contains(
                            "ms_comp_mtp",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return path;
                    }
                }
                catch
                {
                    // Ignorer les peripheriques inaccessibles.
                }
            }
        }
        finally
        {
            if (System.Runtime.InteropServices.Marshal.IsComObject(
                    shellObject))
            {
                System.Runtime.InteropServices.Marshal
                    .FinalReleaseComObject(shellObject);
            }
        }

        return null;
    }
    private static Color GetVolumeColor(string volumeName)
    {
        Color[] palette =
        {
            Color.FromRgb(112, 221, 229), // Cyan
            Color.FromRgb(144, 168, 255), // Bleu
            Color.FromRgb(198, 160, 249), // Violet
            Color.FromRgb(232, 172, 112), // Orange
            Color.FromRgb(118, 201, 164), // Vert
            Color.FromRgb(230, 141, 174), // Rose
            Color.FromRgb(245, 203, 115), // Jaune
            Color.FromRgb(139, 192, 238)  // Bleu clair
        };

        uint hash = 2166136261;

        foreach (char c in volumeName.ToUpperInvariant())
        {
            hash ^= c;
            hash = unchecked(hash * 16777619);
        }

        return palette[(int)(hash % (uint)palette.Length)];
    }

    private void RefreshDrives()
    {
        var current = new Dictionary<string, (long total, long free)>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.IsReady && drive.TotalSize > 0)
                    current[drive.Name] = (drive.TotalSize, drive.TotalFreeSpace);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        // Ne reconstruire les lignes que si les donnees ou noms changent.
        bool volumesChanged =
            current.Count != drives.Count ||
            current.Any(x =>
                !drives.TryGetValue(x.Key, out var previous) ||
                !previous.Equals(x.Value));

        if (!volumesChanged)
        {
            var displayedNames = DrivesPanel.Children
                .OfType<Border>()
                .Where(b => b.Tag is string tag && tag == "DRIVE")
                .Select(b => b.Child)
                .OfType<StackPanel>()
                .SelectMany(p => p.Children.OfType<DockPanel>())
                .SelectMany(p => p.Children.OfType<TextBlock>())
                .Select(t => t.Text)
                .ToHashSet();

            foreach (var item in current)
            {
                string letter = item.Key.TrimEnd((char)92);
                string label = "";

                try
                {
                    label = new DriveInfo(item.Key).VolumeLabel?.Trim() ?? "";
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }

                string expected = string.IsNullOrWhiteSpace(label)
                    ? letter
                    : $"{label} ({letter})";

                if (!displayedNames.Contains(expected))
                {
                    volumesChanged = true;
                    break;
                }
            }
        }

        if (!volumesChanged) return;
        drives.Clear();
        foreach (var item in current) drives[item.Key] = item.Value;
        // Supprimer les lignes des disques sans effacer les appareils MTP.
        for (int i = DrivesPanel.Children.Count - 1; i >= 0; i--)
        {
            if (DrivesPanel.Children[i] is FrameworkElement element &&
                element.Tag is string tag && tag == "DRIVE")
            {
                DrivesPanel.Children.RemoveAt(i);
            }
        }
        foreach (var item in current.OrderBy(x => x.Key))
        {
            string name = item.Key.TrimEnd('\\');
            string volumeLabel = "";
            try
            {
                volumeLabel = new DriveInfo(item.Key).VolumeLabel?.Trim() ?? "";
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            string displayName = string.IsNullOrWhiteSpace(volumeLabel)
                ? name
                : $"{volumeLabel} ({name})";
            Color volumeColor = GetVolumeColor(name);
            long total = item.Value.total, free = item.Value.free;
            // StorageCompactTypography_2026
            // Deux zones independantes, sans suppression d'informations.
            var panel = new StackPanel
            {
                Margin = new Thickness(0, 0, 0, 1)
            };

            var header = new Grid
            {
                ClipToBounds = true
            };

            header.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });

            header.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = GridLength.Auto
            });

            var compactFont = new FontFamily("Arial Narrow");

            var nameText = new TextBlock
            {
                Text = displayName,
                FontFamily = compactFont,
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(volumeColor),
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.NoWrap
            };

            var nameBox = new Viewbox
            {
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 3, 0),
                Child = nameText
            };

            Grid.SetColumn(nameBox, 0);
            header.Children.Add(nameBox);

            var spaceText = new TextBlock
            {
                Text = $"{LocalizedSize(free)} {Translate("libres", "free")} / {LocalizedSize(total)}",
                FontFamily = compactFont,
                FontSize = 12,
                Foreground = new SolidColorBrush(
                    Color.FromRgb(180, 200, 215)),
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.NoWrap
            };

            var spaceBox = new Viewbox
            {
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 122,
                Child = spaceText
            };

            Grid.SetColumn(spaceBox, 1);
            header.Children.Add(spaceBox);

            var bar = new ProgressBar
            {
                Height = 4,
                Minimum = 0,
                Maximum = 100,
                Value = 100.0 * (total - free) / total,
                Margin = new Thickness(0, 3, 0, 0),
                Foreground = new SolidColorBrush(volumeColor),
                Background = new SolidColorBrush(
                    Color.FromRgb(48, 65, 80)),
                BorderThickness = new Thickness(0)
            };

            panel.Children.Add(header);
            panel.Children.Add(bar);
            if (nvmeVolumes.Contains(name))
            {
                var temperature = new TextBlock
                {
                    Text = nvmeTemperature.HasValue
                        ? $"SSD : {nvmeTemperature.Value} °C"
                        : "SSD : N/D",
                    FontSize = 12,
                    Margin = new Thickness(0, 2, 0, 0),
                    Foreground = new SolidColorBrush(
                        Color.FromRgb(180, 200, 215))
                };
                panel.Children.Add(temperature);
            }
                        var driveBorder = new Border
            {
                Tag = "DRIVE",
                Background = new SolidColorBrush(
                    transparentMode ? Colors.Transparent : GlassSurfaceColor(105)),
                CornerRadius = new CornerRadius(13),
                Padding = new Thickness(7, 4, 7, 4),
                Child = panel,
                Margin = new Thickness(0, 0, 0, 4),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            string drivePath = item.Key;

            driveBorder.MouseLeftButtonUp += (_, e) =>
            {
                OpenStorageInExplorer(drivePath);
                e.Handled = true;
            };

            // Menu d'ejection pour les lecteurs amovibles uniquement.
            try
            {
                var driveInfo = new DriveInfo(drivePath);

                if (driveInfo.DriveType == DriveType.Removable)
                {
                    var ejectMenu = new ContextMenu
                    {
                        Background = System.Windows.Media.Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        Padding = new Thickness(0),
                        Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint
                    };

                    var menuTemplate = new ControlTemplate(typeof(ContextMenu));
                    var glassBorder = new FrameworkElementFactory(typeof(Border));

                    glassBorder.SetValue(
                        Border.BackgroundProperty,
                        SettingsButton.Tag as System.Windows.Media.Brush
                            ?? new SolidColorBrush(Color.FromRgb(35, 48, 61)));

                    glassBorder.SetValue(
                        Border.CornerRadiusProperty,
                        new CornerRadius(17));

                    glassBorder.SetValue(
                        Border.BorderBrushProperty,
                        new SolidColorBrush(Color.FromArgb(96, 126, 149, 170)));

                    glassBorder.SetValue(
                        Border.BorderThicknessProperty,
                        new Thickness(1));

                    glassBorder.SetValue(
                        Border.PaddingProperty,
                        new Thickness(12));

                    var shadow = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        BlurRadius = 20,
                        ShadowDepth = 5,
                        Color = Colors.Black,
                        Opacity = 0.65
                    };

                    glassBorder.SetValue(Border.EffectProperty, shadow);

                    var presenter = new FrameworkElementFactory(
                        typeof(ItemsPresenter));

                    glassBorder.AppendChild(presenter);
                    menuTemplate.VisualTree = glassBorder;
                    ejectMenu.Template = menuTemplate;

                    var ejectItem = new MenuItem
                    {
                        Header = "Éjecter en toute sécurité",
                        Foreground = System.Windows.Media.Brushes.White,
                        FontSize = 13,
                        Padding = new Thickness(9, 7, 9, 7)
                    };

                    ejectItem.Click += async (_, args) =>
                    {
                        ejectMenu.IsOpen = false;
                        ejectItem.IsEnabled = false;

                        try
                        {
                            var result = await SafeUsbEject.EjectAsync(drivePath);

                            System.Windows.MessageBox.Show(
                                result.Message,
                                result.Success
                                    ? "Périphérique USB"
                                    : "Éjection impossible",
                                System.Windows.MessageBoxButton.OK,
                                result.Success
                                    ? System.Windows.MessageBoxImage.Information
                                    : System.Windows.MessageBoxImage.Warning);
                        }
                        finally
                        {
                            ejectItem.IsEnabled = true;
                        }
                    };

                    ejectMenu.Items.Add(ejectItem);
                    driveBorder.ContextMenu = ejectMenu;

                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            DrivesPanel.Children.Add(driveBorder);
        }

        if (compactMode)
        {
            RefreshCompactDrives();
            AnimateCompactSize();
        }
    }

    private string portableDevicesSignature = "";

    private void RefreshPortableDevices()
    {
        var devices = PortableDeviceDetector.GetConnectedDevices();

        // Lecture des capacites fournies par Windows pour les appareils MTP.
        var storages = MtpStorageReader.Read();

        compactMtpDevices.Clear();
        compactMtpDevices.AddRange(devices);

        compactMtpStorages.Clear();
        compactMtpStorages.AddRange(storages);

        string signature = string.Join("|", devices) + "|" +
            string.Join("|", storages.Select(s =>
                s.DeviceName + ":" + s.StorageName + ":" +
                s.TotalBytes + ":" + s.FreeBytes));

        if (signature == portableDevicesSignature)
            return;

        portableDevicesSignature = signature;

        // Ne retirer que les lignes des appareils MTP.
        for (int i = DrivesPanel.Children.Count - 1; i >= 0; i--)
        {
            if (DrivesPanel.Children[i] is FrameworkElement element &&
                element.Tag is string tag && tag == "MTP")
            {
                DrivesPanel.Children.RemoveAt(i);
            }
        }

        foreach (string name in devices)
        {
            var deviceStorages = storages
                .Where(s => string.Equals(
                    s.DeviceName, name,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            var content = new StackPanel();

            content.Children.Add(new TextBlock
            {
                Text = name + "  -  Telephone USB",
                FontSize = 15,
                FontWeight = FontWeights.SemiBold
            });

            foreach (var storage in deviceStorages)
            {
                double total = storage.TotalBytes;
                double free = storage.FreeBytes;
                double used = Math.Max(0, total - free);
                double percentage = total > 0
                    ? 100.0 * used / total
                    : 0;

                var storageHeader = new DockPanel
                {
                    Margin = new Thickness(0, 3, 0, 2),
                    LastChildFill = false
                };

                var storageName = new TextBlock
                {
                    Text = storage.StorageName,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center
                };

                var storageSpace = new TextBlock
                {
                    Text = string.Format(
                        System.Globalization.CultureInfo.GetCultureInfo(currentLanguage == "en" ? "en-US" : "fr-FR"),
                        Translate("{0:N1} Go libres / {1:N1} Go", "{0:N1} GB free / {1:N1} GB"),
                        free / 1073741824.0,
                        total / 1073741824.0),
                    FontSize = 11.5,
                    Foreground = new SolidColorBrush(
                        Color.FromRgb(180, 200, 215)),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                };

                DockPanel.SetDock(storageSpace, Dock.Right);
                storageHeader.Children.Add(storageSpace);
                DockPanel.SetDock(storageName, Dock.Left);
                storageHeader.Children.Add(storageName);

                content.Children.Add(storageHeader);

                content.Children.Add(new ProgressBar
                {
                    Minimum = 0,
                    Maximum = 100,
                    Value = Math.Clamp(percentage, 0, 100),
                    Height = 4,
                    BorderThickness = new Thickness(0),
                    Background = new SolidColorBrush(
                        Color.FromArgb(90, 20, 30, 45)),
                    Foreground = new SolidColorBrush(
                        Color.FromRgb(112, 221, 229))
                });
            }

            var border = new Border
            {
                Background = new SolidColorBrush(
                    transparentMode ? Colors.Transparent : GlassSurfaceColor(105)),
                CornerRadius = new CornerRadius(13),
                Padding = new Thickness(7, 4, 7, 4),
                Margin = new Thickness(0, 0, 0, 4),
                Child = content,
                Tag = "MTP"
            };

            
            border.Cursor = System.Windows.Input.Cursors.Hand;

            string mtpDeviceName = name;

            border.MouseLeftButtonUp += (_, e) =>
            {
                string? mtpPath =
                    FindMtpExplorerPath(mtpDeviceName);

                if (!string.IsNullOrWhiteSpace(mtpPath))
                    OpenStorageInExplorer(mtpPath);

                e.Handled = true;
            };

            DrivesPanel.Children.Add(border);
        }
    }

    private readonly Dictionary<Button, Thickness>
        compactOriginalButtonMargins = new();

        private bool compactFadeConfigured = false;

    private void ConfigureCompactButtonsFade()
    {
        if (compactFadeConfigured)
            return;

        compactFadeConfigured = true;

        CompactActionsPanel.Opacity = 0;

        CompactActionsPanel.MouseEnter += (_, _) =>
        {
            CompactActionsPanel.BeginAnimation(
                UIElement.OpacityProperty,
                new System.Windows.Media.Animation.DoubleAnimation(
                    1,
                    TimeSpan.FromSeconds(0.15)));
        };

        CompactActionsPanel.MouseLeave += (_, _) =>
        {
            CompactActionsPanel.BeginAnimation(
                UIElement.OpacityProperty,
                new System.Windows.Media.Animation.DoubleAnimation(
                    0,
                    TimeSpan.FromSeconds(0.20)));
        };
    }

    private void MoveCompactButtons(bool toCompact)
    {
        if (HeaderActions.Children.Count == 0 ||
            HeaderActions.Children[0] is not Grid originalGrid)
            throw new InvalidOperationException(
                "Structure des boutons inattendue.");

        Button[] buttons =
        {
            CompactButton,
            SettingsButton,
            LockButton,
            CloseButton
        };

        if (toCompact)
        {
            ConfigureCompactButtonsFade();
            foreach (var button in buttons)
            {
                if (!compactOriginalButtonMargins.ContainsKey(button))
                    compactOriginalButtonMargins[button] = button.Margin;

                originalGrid.Children.Remove(button);

                button.Margin = new Thickness(0, 0, 0, 2);
                button.Width = 30;
                button.Height = 27;
                button.Padding = new Thickness(2, 1, 2, 1);
                button.HorizontalAlignment = HorizontalAlignment.Center;

                CompactActionsPanel.Children.Add(button);
            }

            if (HeaderActions.Parent is Grid header)
                header.Visibility = Visibility.Collapsed;
        }
        else
        {
            foreach (var button in buttons)
            {
                CompactActionsPanel.Children.Remove(button);

                if (compactOriginalButtonMargins.TryGetValue(
                    button, out var margin))
                    button.Margin = margin;

                button.ClearValue(FrameworkElement.WidthProperty);
                button.ClearValue(FrameworkElement.HeightProperty);
                button.ClearValue(Control.PaddingProperty);

                button.HorizontalAlignment = HorizontalAlignment.Stretch;

                originalGrid.Children.Add(button);
            }

            Grid.SetColumn(CompactButton, 0);
            Grid.SetColumn(SettingsButton, 1);
            Grid.SetColumn(LockButton, 2);
            Grid.SetColumn(CloseButton, 3);

            if (HeaderActions.Parent is Grid header)
                header.Visibility = Visibility.Visible;
        }
    }
private void Compact_Click(object sender, RoutedEventArgs e)
    {
        if (locked) return;

        if (!compactMode)
        {
            normalWidth = Width;
            normalHeight = Height;
            normalLeft = Left;
            normalTop = Top;
            normalManualResize = manualResize;
            SaveWindowState();
        }
        else
        {
            CaptureCompactGeometry();
        }

        compactMode = !compactMode;
        if (compactMode) carouselPage = 0;
        weatherWindow?.SetCompact(compactMode);

        manualResize = compactMode
            ? compactManualResize
            : normalManualResize;

        ContentRootGrid.Margin = compactMode
            ? new Thickness(20, 17, 8, 10)
            : new Thickness(20, 17, 20, 16);

        MainContentGrid.RowDefinitions[4].Height = compactMode
            ? new GridLength(0)
            : new GridLength(1, GridUnitType.Star);

        MoveCompactButtons(compactMode);

        NormalGauges.Visibility = compactMode
            ? Visibility.Collapsed : Visibility.Visible;

        CpuGraphPanel.Visibility = compactMode
            ? Visibility.Collapsed : Visibility.Visible;

        StorageHeading.Visibility = compactMode
            ? Visibility.Collapsed : Visibility.Visible;

        StorageScroll.Visibility = compactMode
            ? Visibility.Collapsed : Visibility.Visible;

        FooterPanel.Visibility = compactMode
            ? Visibility.Collapsed : Visibility.Visible;

        CompactLayout.Visibility = compactMode ? Visibility.Visible : Visibility.Collapsed;

        CompactButton.ToolTip = compactMode
            ? "Mode normal" : "Mode compact";

        CompactButton.Content = compactMode ? "\u2921" : "\u2922";

        if (compactMode)
        {
            MinHeight = 150;
            MainContentGrid.Width = 436;
            compactTopology = "";
            RefreshCompactDrives();

            if (compactGeometryKnown && !restoringCompactStartup)
            {
                Width = compactWidth;
                Height = compactHeight;
                RestorePosition(
                    RecoverWindowPositionV48(
                        compactLeft, compactTop, Width, Height).X,
                    RecoverWindowPositionV48(
                        compactLeft, compactTop, Width, Height).Y);
            }
        }
        else
        {
            MainContentGrid.Width = 254.4; // MainPanelsWidth20_2026
            MinHeight = normalMinHeight;

                        // NormalWidthReduction_2026
            Width = Math.Max(MinWidth, normalWidth);
            Height = Math.Max(MinHeight, normalHeight);

            RestorePosition(
                RecoverWindowPositionV48(
                    normalLeft, normalTop, Width, Height).X,
                RecoverWindowPositionV48(
                    normalLeft, normalTop, Width, Height).Y);
        }

        ApplyCarouselPage();
        ScheduleWindowStateSave();
    }

    private void UpdateCompactGauge(
        string key, double? percent, string? display = null)
    {
        if (!compactRings.TryGetValue(key, out var ring))
            return;

        SetRing(ring, percent ?? 0);

        if (compactValues.TryGetValue(key, out var value))
        {
            value.Text = display ??
                (percent.HasValue
                    ? $"{percent.Value:0}%"
                    : "N/D");
        }
    }

    private void AddCompactGauge(
        string key,
        string label,
        double? percent,
        Color color,
        Action? open = null)
    {
        var cell = new StackPanel
        {
            Width = 112,
            Height = 166,
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            ToolTip = label
        };

        var circle = new Grid
        {
            Width = 106,
            Height = 106,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        circle.Children.Add(new Ellipse
        {
            Stroke = new SolidColorBrush(
                Color.FromRgb(55, 78, 97)),
            StrokeThickness = 9,
            Margin = new Thickness(7)
        });

        var ring = new Ellipse
        {
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 9,
            Margin = new Thickness(7),
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(-90)
        };

        circle.Children.Add(ring);

        var value = new TextBlock
        {
            Text = percent.HasValue
                ? $"{percent.Value:0}%"
                : "N/D",
            FontSize = 26,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };

        circle.Children.Add(value);

        var title = new TextBlock
        {
            Text = label,
            Width = 106,
            Height = 36,
            Margin = new Thickness(0, 4, 0, 0),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 32,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            LineHeight = 16,
            LineStackingStrategy =
                LineStackingStrategy.BlockLineHeight
        };

        // Espace reserve identique pour toutes les jauges.
        var iconArea = new Grid
        {
            Width = 106,
            Height = 16,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        bool isDisk = key.StartsWith("D:",
            StringComparison.OrdinalIgnoreCase);
        bool isPhone = key.StartsWith("M:",
            StringComparison.OrdinalIgnoreCase) ||
            key.StartsWith("P:",
            StringComparison.OrdinalIgnoreCase);

        if (isDisk || isPhone)
        {
            string geometry = isPhone
                ? "M 4,1 L 12,1 Q 13,1 13,2 L 13,14 Q 13,15 12,15 L 4,15 Q 3,15 3,14 L 3,2 Q 3,1 4,1 Z M 6,12 L 10,12"
                : "M 2,4 L 14,4 L 15,12 L 1,12 Z M 3,9 L 13,9 M 11,11 L 12,11";

            var icon = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(geometry),
                Stroke = new SolidColorBrush(
                    Color.FromRgb(167, 189, 205)),
                StrokeThickness = 1.35,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Fill = Brushes.Transparent,
                Width = 16,
                Height = 16,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };

            iconArea.Children.Add(icon);
        }

        cell.Children.Add(iconArea);
        cell.Children.Add(circle);
        cell.Children.Add(title);

        if (open != null)
        {
            cell.Cursor = System.Windows.Input.Cursors.Hand;
            cell.MouseLeftButtonUp += (_, _) => open();
        }

        compactRings[key] = ring;
        compactValues[key] = value;

        SetRing(ring, percent ?? 0);

        CompactDrivesPanel.Children.Add(cell);
    }

    private void RefreshCompactDrives()
    {
        if (!compactMode) return;

        var items = drives.OrderBy(x => x.Key).ToList();

        string topology = string.Join("|",
            items.Select(x => "D:" + x.Key));

        topology += "|" + string.Join("|",
            compactMtpStorages.Select(x =>
                "M:" + x.DeviceName + ":" + x.StorageName));

        topology += "|" + string.Join("|",
            compactMtpDevices.Select(x => "P:" + x));

        if (topology != compactTopology)
        {
            compactTopology = topology;

            CompactDrivesPanel.Children.Clear();
            compactRings.Clear();
            compactValues.Clear();

            AddCompactGauge(
                "CPU", "CPU", 0,
                Color.FromRgb(112, 221, 229));

            AddCompactGauge(
                "RAM", "RAM", 0,
                Color.FromRgb(144, 168, 255));

            AddCompactGauge(
                "GPU", "GPU", 0,
                Color.FromRgb(198, 160, 249));

            foreach (var item in items)
            {
                string name = item.Key.TrimEnd('\\');
                string path = item.Key;

                double percent = 100.0 *
                    (item.Value.total - item.Value.free) /
                    item.Value.total;

                AddCompactGauge(
                    "D:" + item.Key,
                    name,
                    percent,
                    Color.FromRgb(111, 216, 185),
                    () => OpenStorageInExplorer(path));
            }

            foreach (var storage in compactMtpStorages)
            {
                string key = "M:" + storage.DeviceName +
                    ":" + storage.StorageName;

                string device = storage.DeviceName;

                double? percent = storage.TotalBytes > 0
                    ? 100.0 * (storage.TotalBytes -
                        storage.FreeBytes) / storage.TotalBytes
                    : null;

                AddCompactGauge(
                    key,
                    (string.IsNullOrWhiteSpace(storage.DeviceName) ? storage.StorageName : storage.DeviceName),
                    percent,
                    Color.FromRgb(112, 221, 229),
                    () =>
                    {
                        string? path = FindMtpExplorerPath(device);

                        if (!string.IsNullOrWhiteSpace(path))
                            OpenStorageInExplorer(path);
                    });
            }

            foreach (string device in compactMtpDevices)
            {
                bool hasStorage = compactMtpStorages.Any(s =>
                    string.Equals(
                        s.DeviceName,
                        device,
                        StringComparison.OrdinalIgnoreCase) ||
                    s.DeviceName.Contains(
                        device,
                        StringComparison.OrdinalIgnoreCase) ||
                    device.Contains(
                        s.DeviceName,
                        StringComparison.OrdinalIgnoreCase));

                if (hasStorage)
                    continue;

                string deviceName = device;

                AddCompactGauge(
                    "P:" + device,
                    device,
                    null,
                    Color.FromRgb(112, 221, 229),
                    () =>
                    {
                        string? path =
                            FindMtpExplorerPath(deviceName);

                        if (!string.IsNullOrWhiteSpace(path))
                            OpenStorageInExplorer(path);
                    });
            }
            AnimateCompactSize();
        }

        // Actualisation des trois mesures principales.
        UpdateCompactGauge(
            "CPU",
            double.TryParse(
                CpuValue.Text.TrimEnd('%'),
                out double cpu) ? cpu : null,
            CpuValue.Text);

        UpdateCompactGauge(
            "RAM",
            double.TryParse(
                RamValue.Text.TrimEnd('%'),
                out double ram) ? ram : null,
            RamValue.Text);

        UpdateCompactGauge(
            "GPU",
            double.TryParse(
                GpuValue.Text.TrimEnd('%'),
                out double gpu) ? gpu : null,
            GpuValue.Text);

        foreach (var item in items)
        {
            double percent = 100.0 *
                (item.Value.total - item.Value.free) /
                item.Value.total;

            UpdateCompactGauge("D:" + item.Key, percent);
        }

        foreach (var storage in compactMtpStorages)
        {
            double? percent = storage.TotalBytes > 0
                ? 100.0 * (storage.TotalBytes -
                    storage.FreeBytes) / storage.TotalBytes
                : null;

            UpdateCompactGauge(
                "M:" + storage.DeviceName +
                    ":" + storage.StorageName,
                percent);
        }
    }

        private void AnimateCompactSize()
    {
        if (!compactMode)
            return;

        int count = Math.Max(1, CompactDrivesPanel.Children.Count);

        const double cellWidth = 112;
        const double buttonsWidth = 40;
        const double outerWidth = 48;

        double gaugesWidth = count * cellWidth;

        CompactDrivesPanel.Width = gaugesWidth;
        CompactGaugeScroll.Width = gaugesWidth;
        CompactActionsPanel.Width = 36;

        MainContentGrid.Width = gaugesWidth + buttonsWidth;

        if (manualResize || restoringCompactStartup ||
            compactGeometryKnown)
            return;

        double targetWidth = gaugesWidth + buttonsWidth + outerWidth;
        double targetHeight = 160;

        if (Math.Abs(Width - targetWidth) > 2 ||
            Math.Abs(Height - targetHeight) > 2)
        {
            AnimateWindowSize(targetWidth, targetHeight);
        }
    }
private void AnimateWindowSize(
        double targetWidth,
        double targetHeight,
        Action? completed = null)
    {
        targetWidth = Math.Max(MinWidth, targetWidth);
        targetHeight = Math.Max(MinHeight, targetHeight);

        var storyboard = new System.Windows.Media.Animation.Storyboard();

        var widthAnimation =
            new System.Windows.Media.Animation.DoubleAnimation(
                Width, targetWidth,
                TimeSpan.FromMilliseconds(250));

        var heightAnimation =
            new System.Windows.Media.Animation.DoubleAnimation(
                Height, targetHeight,
                TimeSpan.FromMilliseconds(250));

        widthAnimation.EasingFunction =
            new System.Windows.Media.Animation.CubicEase
            {
                EasingMode =
                    System.Windows.Media.Animation.EasingMode.EaseInOut
            };

        heightAnimation.EasingFunction =
            new System.Windows.Media.Animation.CubicEase
            {
                EasingMode =
                    System.Windows.Media.Animation.EasingMode.EaseInOut
            };

        storyboard.Children.Add(widthAnimation);
        storyboard.Children.Add(heightAnimation);

        System.Windows.Media.Animation.Storyboard.SetTarget(
            widthAnimation, this);
        System.Windows.Media.Animation.Storyboard.SetTarget(
            heightAnimation, this);

        System.Windows.Media.Animation.Storyboard.SetTargetProperty(
            widthAnimation, new PropertyPath(Window.WidthProperty));
        System.Windows.Media.Animation.Storyboard.SetTargetProperty(
            heightAnimation, new PropertyPath(Window.HeightProperty));

        storyboard.FillBehavior =
            System.Windows.Media.Animation.FillBehavior.Stop;

        bool animationCompactMode = compactMode;

        storyboard.Completed += (_, _) =>
        {
            if (animationCompactMode != compactMode)
                return;

            if (animationCompactMode && manualResize)
                return;

            Width = targetWidth;
            Height = targetHeight;
            completed?.Invoke();
        };

        storyboard.Begin();
    }
    private double storageAutomaticExtraHeight = 0;

    private int storageRefreshGeneration = 0;

    // StartupStorageAnchorFix_2026
    private bool startupStorageAnchorReady = false;

    // StartupStoragePositionGuard_2026
    private bool firstStorageRefreshPending = true;

    private void RefreshStorageWithAnchoredBottom()
    {
        bool anchor = IsLoaded &&
                      startupStorageAnchorReady &&
                      !compactMode &&
                      WindowState == WindowState.Normal &&
                      ScaleContainer.Child is FrameworkElement;

        FrameworkElement? panel =
            ScaleContainer.Child as FrameworkElement;

        double bottomBefore = double.NaN;

        if (anchor && panel != null)
        {
            UpdateLayout();

            bottomBefore = panel.PointToScreen(
                new System.Windows.Point(
                    0, panel.ActualHeight)).Y;
        }

        int generation = ++storageRefreshGeneration;

        bool protectStartupPosition =
            firstStorageRefreshPending &&
            true &&
            !compactMode;

        double startupTop = Top;

        firstStorageRefreshPending = false;

        RefreshDrives();
        RefreshPortableDevices();
        RefreshCompactDrives();

        if (protectStartupPosition)
        {
            // Premiere actualisation : conserver la position libre.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!IsLoaded || compactMode ||
                    false ||
                    WindowState != WindowState.Normal)
                    return;

                Top = startupTop;
                ScheduleWindowStateSave();
            }), DispatcherPriority.ContextIdle);
        }
        else
        {
            UpdateStorageWindowHeight();
        }

        if (!anchor || panel == null ||
            !double.IsFinite(bottomBefore))
            return;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (generation != storageRefreshGeneration ||
                !IsLoaded ||
                compactMode ||
                WindowState != WindowState.Normal)
                return;

            UpdateLayout();

            if (true)
            {
                double bottomAfter = panel.PointToScreen(
                    new System.Windows.Point(
                        0, panel.ActualHeight)).Y;

                double dpi = PresentationSource.FromVisual(this)?
                    .CompositionTarget?
                    .TransformToDevice.M22 ?? 1.0;

                if (dpi <= 0 || !double.IsFinite(dpi))
                    dpi = 1.0;

                double correction =
                    (bottomBefore - bottomAfter) / dpi;

                if (double.IsFinite(correction) &&
                    Math.Abs(correction) > 0.5)
                {
                    Top += correction;
                }
            }

            weatherWindow?.FollowParent();
            mediaWindow?.FollowParent();
            ScheduleWindowStateSave();

        }), DispatcherPriority.ContextIdle);
    }

private void UpdateStorageWindowHeight()
{
    if (!IsLoaded || compactMode) return;

    Dispatcher.BeginInvoke(new Action(() =>
    {
        if (!IsLoaded || compactMode ||
            WindowState != WindowState.Normal)
            return;

        if (ScaleContainer.Child is not FrameworkElement panel)
            return;

        UpdateLayout();

        double storageHeight = 0;

        foreach (UIElement child in DrivesPanel.Children)
        {
            if (child is FrameworkElement element)
            {
                element.Measure(
                    new Size(254.4, double.PositiveInfinity));

                storageHeight += element.DesiredSize.Height;
            }
        }

        const double baselineStorageHeight = 95;

        double requiredExtra =
            Math.Max(0, storageHeight - baselineStorageHeight);

        double referenceHeight =
            Math.Max(MinHeight, Height - storageAutomaticExtraHeight);

        // StorageTopPreservation_2026
        // Conserver la position verticale en mode libre.

        double originalTop = Top;
        double originalLeft = Left;

        double targetBottom = Top + Height;

        double maximumHeight = Math.Max(
            referenceHeight,
            targetBottom - SystemParameters.WorkArea.Top);

        double targetHeight = Math.Min(
            referenceHeight + requiredExtra,
            maximumHeight);

        bool heightChanged =
            Math.Abs(targetHeight - Height) >= 1;

        if (heightChanged)
        {
            Height = targetHeight;

            if (false)
            {
                Top = targetBottom - targetHeight;
            }
            else
            {
                Top = originalTop;
            }
        }

        storageAutomaticExtraHeight = Math.Max(
            0, targetHeight - referenceHeight);

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!IsLoaded || compactMode ||
                WindowState != WindowState.Normal)
                return;

            UpdateLayout();

            if (false)
            {
                Top = SystemParameters.WorkArea.Bottom - ActualHeight;
            }

            Left = originalLeft;

            weatherWindow?.FollowParent();
            mediaWindow?.FollowParent();

            ScheduleWindowStateSave();

        }), DispatcherPriority.Loaded);

    }), DispatcherPriority.Loaded);
}
private void StartNvmeRead()
    {
        if (nvmeReading)
            return;

        nvmeReading = true;

        _ = System.Threading.Tasks.Task.Run(
            () => NvmeTemperatureReader.Read(0))
            .ContinueWith(task =>
            {
                if (Dispatcher.HasShutdownStarted ||
                    Dispatcher.HasShutdownFinished)
                    return;

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    nvmeReading = false;

                    int? value = task.IsCompletedSuccessfully
                        ? task.Result
                        : null;

                    if (nvmeTemperature != value)
                    {
                        nvmeTemperature = value;
                        drives.Clear();
                        RefreshDrives();
                    }
                }));
            });
    }
}
