using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Windows.Media.Control;

namespace LiquidGlassMonitor;

public sealed class MediaWindow : Window
{
    // TransparentModulesV2_2026
    private Border? transparencyBorder;
    private Brush? normalBackground;
    private Thickness normalBorderThickness;
    private Effect? normalEffect;
    private bool normalAppearanceSaved;
    private bool transparentAppearance;

    // SyncPanelBrushV4
    public void SynchronizePanelBrush(Brush brush)
    {
        normalBackground = brush;

        if (transparencyBorder != null && !transparentAppearance)
            transparencyBorder.Background = brush;
    }
    public void SetTransparentMode(bool enabled)
    {
        transparentAppearance = enabled;

        if (transparencyBorder == null)
            return;

        if (!normalAppearanceSaved)
        {
            normalBackground = transparencyBorder.Background;
            normalBorderThickness = transparencyBorder.BorderThickness;
            normalEffect = transparencyBorder.Effect;
            normalAppearanceSaved = true;
        }

        transparencyBorder.Background = enabled
            ? new SolidColorBrush(Color.FromArgb(1, 0, 0, 0))
            : normalBackground;

        transparencyBorder.BorderThickness = enabled
            ? new Thickness(0)
            : normalBorderThickness;

        transparencyBorder.Effect = enabled
            ? null
            : normalEffect;
    }

    private readonly Window parent;
    private readonly TextBlock titleText;
    private readonly TextBlock artistText;
    private readonly Button previousButton;
    private readonly Button playButton;
    private readonly Button nextButton;
    private readonly Button muteButton;

    private readonly DispatcherTimer timer = new()
    {
        Interval = TimeSpan.FromSeconds(2)
    };

    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private GlobalSystemMediaTransportControlsSession? session;

    private bool enabled = true;
    private bool compact;
    private bool sessionAvailable;
    private bool refreshing;

    private static readonly string settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LiquidGlassMonitor",
        "media-settings.json");

    private sealed class MediaSettings
    {
        public bool Enabled { get; set; } = true;
    }

    public bool Enabled => enabled;

    [DllImport("user32.dll")]
    private static extern void keybd_event(
        byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int flow, int mask, out IntPtr devices);
        int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id,
            out IMMDevice device);
        int RegisterEndpointNotificationCallback(IntPtr client);
        int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        int Activate(ref Guid iid, int context, IntPtr parameters,
            [MarshalAs(UnmanagedType.IUnknown)] out object instance);
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr notify);
        int UnregisterControlChangeNotify(IntPtr notify);
        int GetChannelCount(out uint count);
        int SetMasterVolumeLevel(float level, IntPtr context);
        int SetMasterVolumeLevelScalar(float level, IntPtr context);
        int GetMasterVolumeLevel(out float level);
        int GetMasterVolumeLevelScalar(out float level);
        int SetChannelVolumeLevel(uint channel, float level, IntPtr context);
        int SetChannelVolumeLevelScalar(uint channel, float level, IntPtr context);
        int GetChannelVolumeLevel(uint channel, out float level);
        int GetChannelVolumeLevelScalar(uint channel, out float level);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, IntPtr context);
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
        int GetVolumeStepInfo(out uint step, out uint count);
        int VolumeStepUp(IntPtr context);
        int VolumeStepDown(IntPtr context);
        int QueryHardwareSupport(out uint mask);
        int GetVolumeRange(out float min, out float max, out float increment);
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject
    {
    }

    private static (float level, bool muted)? ReadSystemVolume()
    {
        object? enumeratorObject = null;
        object? endpointObject = null;
        object? volumeObject = null;

        try
        {
            enumeratorObject = new MMDeviceEnumeratorComObject();

            var enumerator = (IMMDeviceEnumerator)enumeratorObject;

            Marshal.ThrowExceptionForHR(
                enumerator.GetDefaultAudioEndpoint(
                    0, 1, out IMMDevice endpoint));

            endpointObject = endpoint;

            Guid iid = typeof(IAudioEndpointVolume).GUID;

            Marshal.ThrowExceptionForHR(
                endpoint.Activate(
                    ref iid, 23, IntPtr.Zero, out object volume));

            volumeObject = volume;

            var audio = (IAudioEndpointVolume)volume;

            Marshal.ThrowExceptionForHR(
                audio.GetMasterVolumeLevelScalar(out float level));

            Marshal.ThrowExceptionForHR(
                audio.GetMute(out bool muted));

            return (level, muted);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (volumeObject != null &&
                Marshal.IsComObject(volumeObject))
                Marshal.ReleaseComObject(volumeObject);

            if (endpointObject != null &&
                Marshal.IsComObject(endpointObject))
                Marshal.ReleaseComObject(endpointObject);

            if (enumeratorObject != null &&
                Marshal.IsComObject(enumeratorObject))
                Marshal.ReleaseComObject(enumeratorObject);
        }
    }

    private static System.Windows.Shapes.Path IconPath(
        string data, double thickness = 1.8)
    {
        return new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse(data),
            Stroke = Brushes.White,
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Fill = Brushes.Transparent,
            Stretch = Stretch.None
        };
    }

    private static FrameworkElement CreateMediaIcon(string kind)
    {
        var canvas = new Canvas
        {
            Width = 26,
            Height = 26,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };

        string shape = kind switch
        {
            "previous" => "M 5,6 L 5,20 M 19,6 L 8,13 L 19,20 Z",
            "next" => "M 21,6 L 21,20 M 7,6 L 18,13 L 7,20 Z",
            "pause" => "M 9,6 L 9,20 M 17,6 L 17,20",
            "play" => "M 9,6 L 19,13 L 9,20 Z",
            _ => "M 5,10 L 9,10 L 15,5 L 15,21 L 9,16 L 5,16 Z"
        };

        var path = IconPath(shape, kind == "pause" ? 3.2 : 1.8);
        canvas.Children.Add(path);

        return canvas;
    }

    private static FrameworkElement CreateVolumeIcon(int bars, bool muted)
    {
        var canvas = new Canvas
        {
            Width = 30,
            Height = 26,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };

        canvas.Children.Add(
            IconPath("M 3,10 L 7,10 L 12,6 L 12,20 L 7,16 L 3,16 Z"));

        if (muted)
        {
            canvas.Children.Add(
                IconPath("M 17,9 L 25,17 M 25,9 L 17,17", 2.2));
        }
        else
        {
            if (bars >= 1)
                canvas.Children.Add(
                    IconPath("M 16,10 Q 20,13 16,16"));

            if (bars >= 2)
                canvas.Children.Add(
                    IconPath("M 19,7 Q 26,13 19,19"));

            if (bars >= 3)
                canvas.Children.Add(
                    IconPath("M 22,4 Q 32,13 22,22"));
        }

        return canvas;
    }

    // MediaWheelVolume_2026
    private void ChangeVolumeWithWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;

        var state = ReadSystemVolume();

        if (state == null)
            return;

        float oldLevel = state.Value.level;
        float increment = e.Delta > 0 ? 0.05f : -0.05f;
        float newLevel = Math.Clamp(oldLevel + increment, 0f, 1f);

        object? enumeratorObject = null;
        object? endpointObject = null;
        object? volumeObject = null;

        try
        {
            enumeratorObject = new MMDeviceEnumeratorComObject();

            var enumerator = (IMMDeviceEnumerator)enumeratorObject;

            Marshal.ThrowExceptionForHR(
                enumerator.GetDefaultAudioEndpoint(
                    0, 1, out IMMDevice endpoint));

            endpointObject = endpoint;

            Guid iid = typeof(IAudioEndpointVolume).GUID;

            Marshal.ThrowExceptionForHR(
                endpoint.Activate(
                    ref iid, 23, IntPtr.Zero, out object volume));

            volumeObject = volume;

            var audio = (IAudioEndpointVolume)volume;

            Marshal.ThrowExceptionForHR(
                audio.SetMasterVolumeLevelScalar(
                    newLevel, IntPtr.Zero));

            if (state.Value.muted && e.Delta > 0)
            {
                Marshal.ThrowExceptionForHR(
                    audio.SetMute(false, IntPtr.Zero));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                "Reglage volume molette : " + ex.Message);
        }
        finally
        {
            if (volumeObject != null &&
                Marshal.IsComObject(volumeObject))
                Marshal.ReleaseComObject(volumeObject);

            if (endpointObject != null &&
                Marshal.IsComObject(endpointObject))
                Marshal.ReleaseComObject(endpointObject);

            if (enumeratorObject != null &&
                Marshal.IsComObject(enumeratorObject))
                Marshal.ReleaseComObject(enumeratorObject);
        }

        UpdateVolumeIcon();
    }
    private void UpdateVolumeIcon()
    {
        var volume = ReadSystemVolume();

        if (volume == null)
            return;

        bool muted = volume.Value.muted;
        float level = volume.Value.level;

        int bars = level <= 0 ? 0 :
                   level <= 0.33f ? 1 :
                   level <= 0.66f ? 2 : 3;

        muteButton.Content = CreateVolumeIcon(bars, muted);

        int percent = (int)Math.Round(level * 100);

        muteButton.ToolTip = muted
            ? "Son coupe - cliquer pour retablir"
            : "Volume Windows : " + percent + " % - cliquer pour couper";
    }
    private const byte VK_VOLUME_MUTE = 0xAD;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    public MediaWindow(Window main)
    {
        parent = main;
        Owner = main;

        Title = "LiquidGlassMonitor - Media";
        Width = 358;
        Height = 154;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;

        LoadSettings();

        var background = main.Resources["Panel"] as SolidColorBrush
            ?? new SolidColorBrush(Color.FromRgb(42, 52, 67))
            {
                Opacity = 0.85
            };

        var border = new Border
        {
            Width = 340,
            Height = 136,
            Margin = new Thickness(9),
            Padding = new Thickness(18, 12, 18, 12),
            CornerRadius = new CornerRadius(27),
            Background = background,
            BorderBrush = new SolidColorBrush(
                Color.FromArgb(96, 126, 149, 170)),
            BorderThickness = new Thickness(1),
            Effect = new DropShadowEffect
            {
                BlurRadius = 25,
                ShadowDepth = 6,
                Color = Colors.Black,
                Opacity = 0.65
            }
        };

        var content = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center
        };

        titleText = new TextBlock
        {
            Text = "Aucun media en cours",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        artistText = new TextBlock
        {
            Text = "",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 14,
            Foreground = new SolidColorBrush(
                Color.FromRgb(180, 200, 215)),
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 3, 0, 10),
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var controls = new UniformGrid
        {
            Rows = 1,
            Columns = 4
        };

        previousButton = MakeButton("previous", "Media precedent");
        playButton = MakeButton("play", "Lecture / Pause");
        nextButton = MakeButton("next", "Media suivant");
        muteButton = MakeButton("volume", "Couper / retablir le son Windows");
        muteButton.PreviewMouseWheel += ChangeVolumeWithWheel;

        previousButton.Click += async (_, _) =>
        {
            try
            {
                if (session != null)
                    await session.TrySkipPreviousAsync();
            }
            catch { }
            await RefreshAsync();
        };

        playButton.Click += async (_, _) =>
        {
            try
            {
                if (session != null)
                    await session.TryTogglePlayPauseAsync();
            }
            catch { }
            await RefreshAsync();
        };

        nextButton.Click += async (_, _) =>
        {
            try
            {
                if (session != null)
                    await session.TrySkipNextAsync();
            }
            catch { }
            await RefreshAsync();
        };

        muteButton.Click += (_, _) =>
        {
            keybd_event(VK_VOLUME_MUTE, 0, 0, UIntPtr.Zero);
            keybd_event(VK_VOLUME_MUTE, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            Dispatcher.BeginInvoke(
                new Action(UpdateVolumeIcon),
                DispatcherPriority.Background);
        };

        controls.Children.Add(previousButton);
        controls.Children.Add(playButton);
        controls.Children.Add(nextButton);
        controls.Children.Add(muteButton);

        content.Children.Add(titleText);
        content.Children.Add(artistText);
        content.Children.Add(controls);

        transparencyBorder = border;
        border.Child = content;

        Content = new Viewbox
        {
            Stretch = Stretch.Uniform,
            Child = border
        };

        timer.Tick += async (_, _) =>
        {
            SetCompact(
                parent.WindowState != WindowState.Normal ||
                (parent is MainWindow mw && mw.IsMediaCompactMode()));

            FollowParent();
            UpdateVolumeIcon();
            await RefreshAsync();
        };

        Closed += (_, _) => timer.Stop();
    }

    private static Button MakeButton(string kind, string tooltip)
    {
        var style = new Style(typeof(Button));
        var template = new ControlTemplate(typeof(Button));

        var border = new FrameworkElementFactory(typeof(Border));

        border.SetValue(
            Border.BackgroundProperty,
            new TemplateBindingExtension(Button.BackgroundProperty));

        border.SetValue(
            Border.CornerRadiusProperty,
            new CornerRadius(8));

        var presenter = new FrameworkElementFactory(
            typeof(ContentPresenter));

        presenter.SetValue(
            FrameworkElement.HorizontalAlignmentProperty,
            HorizontalAlignment.Center);

        presenter.SetValue(
            FrameworkElement.VerticalAlignmentProperty,
            VerticalAlignment.Center);

        presenter.SetValue(
            ContentPresenter.HorizontalAlignmentProperty,
            HorizontalAlignment.Center);

        presenter.SetValue(
            ContentPresenter.VerticalAlignmentProperty,
            VerticalAlignment.Center);

        border.AppendChild(presenter);
        template.VisualTree = border;

        var disabled = new Trigger
        {
            Property = Button.IsEnabledProperty,
            Value = false
        };

        disabled.Setters.Add(
            new Setter(UIElement.OpacityProperty, 0.38));

        template.Triggers.Add(disabled);

        style.Setters.Add(
            new Setter(Button.TemplateProperty, template));

        return new Button
        {
            Content = kind == "volume"
                ? CreateVolumeIcon(2, false)
                : CreateMediaIcon(kind),
            ToolTip = tooltip,
            Height = 43,
            Margin = new Thickness(4, 0, 4, 0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Foreground = Brushes.White,
            Background = new SolidColorBrush(
                Color.FromArgb(85, 62, 82, 103)),
            BorderThickness = new Thickness(0),
            Style = style,
            Cursor = Cursors.Hand
        };
    }
    public void Start()
    {
        if (!timer.IsEnabled)
            timer.Start();

        _ = RefreshAsync();
    }

    public void SetEnabled(bool value)
    {
        enabled = value;
        SaveSettings();

        if (!enabled)
        {
            session = null;
            sessionAvailable = false;
        }

        UpdateVisibility();

        if (enabled)
            _ = RefreshAsync();
    }

    public void SetCompact(bool value)
    {
        if (compact == value)
            return;

        compact = value;
        UpdateVisibility();
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(settingsPath))
            {
                var settings = JsonSerializer.Deserialize<MediaSettings>(
                    File.ReadAllText(settingsPath));

                if (settings != null)
                    enabled = settings.Enabled;
            }
        }
        catch { }
    }

    private void SaveSettings()
    {
        try
        {
            string? directory = Path.GetDirectoryName(settingsPath);

            if (directory != null)
                Directory.CreateDirectory(directory);

            File.WriteAllText(
                settingsPath,
                JsonSerializer.Serialize(new MediaSettings
                {
                    Enabled = enabled
                }));
        }
        catch { }
    }

    private async Task RefreshAsync()
    {
        if (refreshing || !enabled)
            return;

        refreshing = true;

        try
        {
            manager ??=
                await GlobalSystemMediaTransportControlsSessionManager
                    .RequestAsync();

            var current = manager.GetCurrentSession();

            if (current == null)
            {
                var sessions = manager.GetSessions();

                if (sessions.Count > 0)
                    current = sessions[0];
            }

            session = current;
            sessionAvailable = current != null;

            if (current != null)
            {
                var properties = await current.TryGetMediaPropertiesAsync();
                var playback = current.GetPlaybackInfo();
                var capabilities = playback.Controls;

                titleText.Text = string.IsNullOrWhiteSpace(properties.Title)
                    ? "Media en cours"
                    : properties.Title;

                artistText.Text = properties.Artist ?? "";

                playButton.Content = CreateMediaIcon(
                    playback.PlaybackStatus ==
                    GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing
                        ? "pause"
                        : "play");

                previousButton.IsEnabled = capabilities.IsPreviousEnabled;
                nextButton.IsEnabled = capabilities.IsNextEnabled;

                playButton.IsEnabled =
                    capabilities.IsPlayPauseToggleEnabled ||
                    capabilities.IsPlayEnabled ||
                    capabilities.IsPauseEnabled;
            }
            else
            {
                titleText.Text = "Aucun media en cours";
                artistText.Text = "";

                previousButton.IsEnabled = false;
                playButton.IsEnabled = false;
                nextButton.IsEnabled = false;
            }
        }
        catch
        {
            session = null;
            sessionAvailable = false;
            manager = null;
        }
        finally
        {
            refreshing = false;
            UpdateVisibility();
        }
    }

    private void UpdateVisibility()
    {
        bool visible =
            enabled &&
            sessionAvailable &&
            !compact &&
            parent.WindowState == WindowState.Normal;

        if (visible)
        {
            FollowParent();

            if (!IsVisible)
                Show();
        }
        else if (IsVisible)
        {
            Hide();
        }
    }

    public void FollowParent()
    {
        if (parent.WindowState != WindowState.Normal)
            return;

        if (parent.Content is not Viewbox viewbox ||
            viewbox.Child is not Border border ||
            border.ActualWidth <= 0 ||
            border.ActualHeight <= 0)
            return;

        Point left = border.PointToScreen(new Point(0, 0));
        Point right = border.PointToScreen(
            new Point(border.ActualWidth, 0)
        );

        double visibleWidthPx = right.X - left.X;

        if (!double.IsFinite(visibleWidthPx) ||
            visibleWidthPx <= 0)
            return;

        double scalePx = visibleWidthPx / 340.0;
        double widthPx = 358.0 * scalePx;
        double heightPx = 154.0 * scalePx;

        var dpi = SatellitePositionV44.Dpi(this);

        Width = widthPx / dpi.x;
        Height = heightPx / dpi.y;

        double referenceTop = left.Y;

        foreach (Window owned in parent.OwnedWindows)
        {
            if (owned is WeatherWindow weather &&
                weather.IsVisible)
            {
                weather.FollowParent();

                Point weatherTop = weather.PointToScreen(
                    new Point(0, 0)
                );

                referenceTop =
                    weatherTop.Y + 9.0 * scalePx;

                break;
            }
        }

        SatellitePositionV44.Move(
            this,
            left.X - 9.0 * scalePx,
            referenceTop - heightPx
        );
    }}