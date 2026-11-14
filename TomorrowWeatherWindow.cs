// SunSpacingV60 : ecartement solaire +6 pixels
// TomorrowWeatherLayoutV58
// TomorrowWeatherLayoutV57
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace LiquidGlassMonitor;

public sealed class TomorrowWeatherWindow : Window
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

    private readonly TextBlock headingText;
    private readonly Viewbox weatherIcon;
    private readonly TextBlock minText;
    private readonly TextBlock maxText;
    private readonly TextBlock descriptionText;
    private readonly TextBlock sunriseText;
    private readonly TextBlock sunsetText;

    public TomorrowWeatherWindow(Window parent)
    {
        Owner = parent;
        Title = "LiquidGlassMonitor - Demain";
        Width = 188;
        Height = 184;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;

        var background = parent.Resources["Panel"] as SolidColorBrush
            ?? new SolidColorBrush(Color.FromRgb(42, 52, 67)) { Opacity = 0.85 };
        var border = new Border
        {
            Width = 170,
            Height = 166,
            Margin = new Thickness(9),
            Padding = new Thickness(10, 9, 10, 9),
            CornerRadius = new CornerRadius(27),
            Background = background,
            BorderBrush = new SolidColorBrush(Color.FromArgb(96, 126, 149, 170)),
            BorderThickness = new Thickness(1),
            Effect = new DropShadowEffect
            {
                BlurRadius = 25, ShadowDepth = 6,
                Color = Colors.Black, Opacity = 0.0
            }
        };
        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(19) });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(19) });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        headingText = Label("DEMAIN", 11, Color.FromRgb(201, 216, 233));
        headingText.FontWeight = FontWeights.SemiBold;
        headingText.HorizontalAlignment = HorizontalAlignment.Center;
        headingText.RenderTransform = new TranslateTransform(0, 5);
        Grid.SetRow(headingText, 0);
        layout.Children.Add(headingText);

        weatherIcon = new Viewbox
        {
            Width = 58, Height = 52,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = WeatherIcons.Create(2)
        };
        Grid.SetRow(weatherIcon, 1);
        layout.Children.Add(weatherIcon);

        var temperatures = new Grid
        {
            Width = 126,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        temperatures.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        temperatures.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var maxColumn = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            RenderTransform = new TranslateTransform(-5, 0),
            LayoutTransform = new ScaleTransform(1.15, 1.15)
        };

        var minColumn = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            RenderTransform = new TranslateTransform(5, 0),
            LayoutTransform = new ScaleTransform(1.15, 1.15)
        };

        var maxLabel = Label("MAX", 9, Color.FromRgb(190, 205, 221));
        maxLabel.HorizontalAlignment = HorizontalAlignment.Center;

        var minLabel = Label("MIN", 9, Color.FromRgb(190, 205, 221));
        minLabel.HorizontalAlignment = HorizontalAlignment.Center;

        maxText = Label("--°", 17, Colors.White);
        maxText.FontWeight = FontWeights.SemiBold;
        maxText.HorizontalAlignment = HorizontalAlignment.Center;

        minText = Label("--°", 17, Color.FromRgb(174, 194, 214));
        minText.FontWeight = FontWeights.SemiBold;
        minText.HorizontalAlignment = HorizontalAlignment.Center;

        maxColumn.Children.Add(maxLabel);
        maxColumn.Children.Add(maxText);

        minColumn.Children.Add(minLabel);
        minColumn.Children.Add(minText);

        Grid.SetColumn(maxColumn, 0);
        Grid.SetColumn(minColumn, 1);

        temperatures.Children.Add(maxColumn);
        temperatures.Children.Add(minColumn);

        Grid.SetRow(temperatures, 2);
        layout.Children.Add(temperatures);

        var sunTimes = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransform = new TranslateTransform(0, -3)
        };
        var rise = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            RenderTransform = new TransformGroup
            {
                Children = new TransformCollection
                {
                    new ScaleTransform(1.20, 1.20),
                    new TranslateTransform(-3, 0)
                }
            },
            RenderTransformOrigin = new Point(0.5, 0.5)
        };
        rise.Children.Add(Label("↑ ", 13, Color.FromRgb(234, 199, 143)));
        sunriseText = Label("--:--", 11, Color.FromRgb(234, 199, 143));
        rise.Children.Add(sunriseText);
        var set = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(8, 0, 0, 0),
            RenderTransform = new TransformGroup
            {
                Children = new TransformCollection
                {
                    new ScaleTransform(1.20, 1.20),
                    new TranslateTransform(3, 0)
                }
            },
            RenderTransformOrigin = new Point(0.5, 0.5)
        };
        set.Children.Add(Label("↓ ", 13, Color.FromRgb(231, 167, 131)));
        sunsetText = Label("--:--", 11, Color.FromRgb(231, 167, 131));
        set.Children.Add(sunsetText);
        sunTimes.Children.Add(rise);
        sunTimes.Children.Add(set);
        descriptionText = Label("Chargement...", 16.5, Color.FromRgb(190, 205, 221));
        descriptionText.TextAlignment = TextAlignment.Center;
        descriptionText.TextWrapping = TextWrapping.NoWrap;
        descriptionText.TextTrimming = TextTrimming.None;
        descriptionText.HorizontalAlignment = HorizontalAlignment.Center;
        descriptionText.VerticalAlignment = VerticalAlignment.Center;

        var adaptiveDescription = new Viewbox
        {
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransform = new TranslateTransform(0, -3),
            Child = descriptionText
        };

        Grid.SetRow(adaptiveDescription, 3);
        layout.Children.Add(adaptiveDescription);
        Grid.SetRow(sunTimes, 4);
        layout.Children.Add(sunTimes);

        transparencyBorder = border;
        border.Child = layout;
        Content = new Viewbox
        {
            Stretch = Stretch.Uniform,
            Child = border
        };

        // Agrandissement visuel du contenu uniquement.
        layout.RenderTransform = new ScaleTransform(1.15, 1.15);
        layout.RenderTransformOrigin = new Point(0.5, 0.5);
    }

    private static TextBlock Label(string text, double size, Color color) => new()
    {
        Text = text,
        FontFamily = new FontFamily("Segoe UI"),
        FontSize = size,
        Foreground = new SolidColorBrush(color)
    };

    // WeatherLanguage_2026
    public void SetLanguage(string language)
    {
        bool english = language == "en";

        headingText.Text = english ? "TOMORROW" : "DEMAIN";
        headingText.FontSize = english ? 9 : 11;

        Title = english
            ? "LiquidGlassMonitor - Tomorrow"
            : "LiquidGlassMonitor - Demain";
    }

    public void SetForecast(int code, string min, string max, string sunrise, string sunset, string description)
    {
        weatherIcon.Child = WeatherIcons.Create(code);
        minText.Text = min;
        maxText.Text = max;
        descriptionText.Text = description;
        sunriseText.Text = sunrise;
        sunsetText.Text = sunset;
    }

    public void PositionNear(WeatherWindow weather)
    {
        if (!weather.IsVisible)
            return;

        var weatherDpi = SatellitePositionV44.Dpi(weather);
        var dpi = SatellitePositionV44.Dpi(this);

        double scalePx =
            weather.Width * weatherDpi.x / 358.0;

        if (!double.IsFinite(scalePx) || scalePx <= 0)
            return;

        Width = 188.0 * scalePx / dpi.x;
        Height = 184.0 * scalePx / dpi.y;

        Point weatherTop = weather.PointToScreen(
            new Point(0, 0)
        );

        SatellitePositionV44.Move(
            this,
            weatherTop.X - 179.0 * scalePx,
            weatherTop.Y
        );
    }}
