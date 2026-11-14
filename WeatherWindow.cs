// SunRowShiftV61 : +4 pixels supplementaires, total +7
using System;
using System.IO;
using System.Collections.Generic;
using System.Windows.Media.Animation;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace LiquidGlassMonitor;

public sealed class WeatherWindow : Window
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

        tomorrowWindow?.SynchronizePanelBrush(brush);
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
        tomorrowWindow?.SetTransparentMode(enabled);    }

    private static readonly HttpClient client = new()
    {
        Timeout = TimeSpan.FromSeconds(12)
    };

    private readonly Window parent;
    private readonly DispatcherTimer timer = new()
    {
        Interval = TimeSpan.FromMinutes(10)
    };

    private readonly TextBlock cityText;
    // WeatherCountry_2026
    private readonly TextBlock countryText;
    private readonly TextBlock tempText;
    private readonly TextBlock conditionText;
    private readonly TextBlock sunriseText;
    private readonly TextBlock sunsetText;
    private readonly Viewbox iconView;
    private readonly TextBlock statusText;

    // WeatherLanguage_2026
    private string weatherLanguage = "fr";

    // TemperatureUnitMenuV2
    private bool useFahrenheit;

    private string FormatTemperature(string value, bool includeUnit)
    {
        string normalized = value
            .Replace("\u00B0C", "")
            .Replace("\u00B0F", "")
            .Replace("\u00B0", "")
            .Trim();

        if (!double.TryParse(
                normalized,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out double celsius))
        {
            return includeUnit
                ? (useFahrenheit ? "--\u00B0F" : "--\u00B0C")
                : "--\u00B0";
        }

        double result = useFahrenheit
            ? celsius * 9.0 / 5.0 + 32.0
            : celsius;

        string number = Math.Round(
            result,
            0,
            MidpointRounding.AwayFromZero
        ).ToString(
            "0",
            System.Globalization.CultureInfo.InvariantCulture);

        return number + (includeUnit
            ? (useFahrenheit ? "\u00B0F" : "\u00B0C")
            : "\u00B0");
    }

    private void SetTemperatureUnit(bool fahrenheit)
    {
        if (useFahrenheit == fahrenheit)
            return;

        useFahrenheit = fahrenheit;
        SaveSettings();
        ShowSelectedCity();
    }

    private string WeatherTranslate(string french, string english) =>
        weatherLanguage == "en" ? english : french;

    private string DescribeLocalized(int code)
    {
        if (weatherLanguage != "en")
            return Describe(code).Text;

        if (code == 0) return "Sunny";
        if (code <= 2) return "Partly cloudy";
        if (code == 3) return "Overcast";
        if (code == 45 || code == 48) return "Fog";
        if (code >= 51 && code <= 67) return "Rain";
        if (code >= 71 && code <= 77) return "Snow";
        if (code >= 80 && code <= 82) return "Showers";
        if (code >= 95) return "Thunderstorm";

        return "Variable conditions";
    }

    public void SetLanguage(string language)
    {
        weatherLanguage = language == "en" ? "en" : "fr";

        tomorrowWindow?.SetLanguage(weatherLanguage);

        ShowSelectedCity();
    }

    private bool enabled;
    private bool compact;
    private bool busy;
    private bool firstRefresh = true;
    // WeatherTwoCities_2026
    private string city = "Lille";
    private string city1 = "Lille";
    private string city2 = "";
    // WeatherThreeCities_2026
    private string city3 = "";


    private int selectedCity = 0;
    private readonly Grid weatherGrid;

    // WeatherDateFormatV49 - désactivé
    private readonly TextBlock weatherDateText = MakeText("", 11);
    private string weatherDateFormat = "fr";

    private readonly DispatcherTimer weatherDateTimer = new()
    {
        Interval = TimeSpan.FromMinutes(1)
    };
    private readonly DispatcherTimer tomorrowCloseTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(350)
    };
    private TomorrowWeatherWindow? tomorrowWindow;

    private readonly Dictionary<string, WeatherSnapshot> cache =
        new(StringComparer.OrdinalIgnoreCase);

    private sealed record WeatherSnapshot(
        string Name, string Country, string Temperature, string Condition,
        int Code, string Status, DateTime Updated, string Sunrise, string Sunset,
        int TomorrowCode, string TomorrowMin, string TomorrowMax,
        string TomorrowSunrise, string TomorrowSunset);

    private string SlotLabel => "";

    private static readonly string settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LiquidGlassMonitor", "weather-settings.json");

    private sealed class WeatherSettings
    {
        public bool UseFahrenheit { get; set; }
        public bool Enabled { get; set; }
        public string City { get; set; } = "Lille";
        public string City2 { get; set; } = "";
        public string City3 { get; set; } = "";
        public int SelectedCity { get; set; }
        public string DateFormat { get; set; } = "fr";
    }

    public bool Enabled => enabled;

    private static TextBlock MakeText(string value, double size)
    {
        return new TextBlock
        {
            Text = value,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = size,
            Foreground = Brushes.White
        };
    }
    public WeatherWindow(Window main)
    {
        parent = main;
        Owner = main;

        Title = "LiquidGlassMonitor - Meteo";
        Width = 340;
        Height = 184;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;

        var background = main.Resources["Panel"] as SolidColorBrush
            ?? new SolidColorBrush(Color.FromRgb(42, 52, 67))
            {
                Opacity = 0.85
            };

        var border = new Border
        {
            Width = 340,
            Height = 166,
            Margin = new Thickness(9),
            Padding = new Thickness(20, 13, 17, 12),
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

        var grid = new Grid();
        weatherGrid = grid;
        weatherGrid.RenderTransform = new TranslateTransform();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(78)
        });
        grid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(100)
        });

        var left = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center
        };

        cityText = MakeText(city, 19);
        cityText.FontWeight = FontWeights.SemiBold;

        weatherDateText.Foreground = new SolidColorBrush(
            Color.FromRgb(190, 205, 221));
        weatherDateText.Margin = new Thickness(0, 1, 0, 2);

        countryText = MakeText("", 11);
        countryText.Foreground = new SolidColorBrush(
            Color.FromRgb(190, 205, 221));

        tempText = MakeText("--°C", 34);
        tempText.FontWeight = FontWeights.SemiBold;

        conditionText = MakeText("Chargement...", 14);
        conditionText.TextWrapping = TextWrapping.Wrap;
        conditionText.Foreground = new SolidColorBrush(
            Color.FromRgb(190, 205, 221));

        statusText = MakeText("", 10);
        statusText.Foreground = new SolidColorBrush(
            Color.FromRgb(144, 164, 184));

        left.Children.Add(cityText);
        // Date déplacée dans le module principal.
        left.Children.Add(countryText);
        left.Children.Add(tempText);
        left.Children.Add(conditionText);
        var sunRow = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        sunriseText = MakeText("--:--", 13);
        sunriseText.HorizontalAlignment = HorizontalAlignment.Center;
        sunriseText.TextAlignment = TextAlignment.Center;
        sunriseText.RenderTransform = new TranslateTransform(-20, 0);
        sunsetText = MakeText("--:--", 13);
        sunsetText.HorizontalAlignment = HorizontalAlignment.Center;
        sunsetText.TextAlignment = TextAlignment.Center;
        sunsetText.RenderTransform = new TranslateTransform(-20, 0);

        sunriseText.Foreground = new SolidColorBrush(
            Color.FromRgb(234, 199, 143));
        sunsetText.Foreground = new SolidColorBrush(
            Color.FromRgb(231, 167, 131));

var sunriseGroup = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            RenderTransform = new TranslateTransform(0, -10),
            // SunGroupsScaleV56 : agrandissement de 30 %.
            LayoutTransform = new ScaleTransform(1.30, 1.30)
        };

        sunriseGroup.Children.Add(CreateSunIcon(true));
        sunriseGroup.Children.Add(sunriseText);
        sunRow.Children.Add(sunriseGroup);

        var sunSpace = new Border
        {
            Height = 8
        };
        

var sunsetGroup = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            RenderTransform = new TranslateTransform(0, 10),
            // SunGroupsScaleV56 : agrandissement de 30 %.
            LayoutTransform = new ScaleTransform(1.30, 1.30)
        };

        sunsetGroup.Children.Add(CreateSunIcon(false));
        sunsetGroup.Children.Add(sunsetText);
        sunRow.Children.Add(sunsetGroup);

        

        iconView = new Viewbox
        {
            Width = 116,
            Height = 116,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(-78, 0, 0, 0),
            Child = WeatherIcons.Create(2)
        };

        // WeatherHoverStatus_2026
        var weatherRight = new Grid();

        weatherRight.RowDefinitions.Add(new RowDefinition
        {
            Height = GridLength.Auto
        });

        weatherRight.RowDefinitions.Add(new RowDefinition
        {
            Height = new GridLength(1, GridUnitType.Star)
        });

        Grid.SetRow(iconView, 1);
        weatherRight.Children.Add(iconView);

        statusText.HorizontalAlignment = HorizontalAlignment.Center;
        statusText.TextAlignment = TextAlignment.Center;
        statusText.Visibility = Visibility.Hidden;
        statusText.Margin = new Thickness(-78, 2, 0, 0);

        Grid.SetRow(statusText, 0);
        weatherRight.Children.Add(statusText);

        Grid.SetColumn(left, 0);
        sunRow.VerticalAlignment = VerticalAlignment.Center;
        sunRow.HorizontalAlignment = HorizontalAlignment.Center;
        sunRow.RenderTransform = new TranslateTransform(135, 0);

        Grid.SetColumn(sunRow, 1);
        weatherRight.RenderTransform = new TranslateTransform(-30, 0);
        Grid.SetColumn(weatherRight, 2);
        grid.Children.Add(sunRow);

        grid.Children.Add(left);
        grid.Children.Add(weatherRight);

        MouseEnter += (_, _) =>
        {
            statusText.Visibility = Visibility.Visible;
            ShowTomorrow();
        };

        MouseLeave += (_, _) =>
        {
            statusText.Visibility = Visibility.Hidden;
            ScheduleTomorrowClose();
        };
        transparencyBorder = border;
        border.Child = grid;
        Content = new Viewbox
        {
            Stretch = Stretch.Uniform,
            Child = border
        };

        // GlassWeatherMenuStyleV1
        var menu = new ContextMenu
        {
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Foreground = System.Windows.Media.Brushes.White,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            FontSize = 13,
            SnapsToDevicePixels = true
        };

        var glassMenuTemplate = new ControlTemplate(typeof(ContextMenu));

        var glassBorder = new FrameworkElementFactory(typeof(Border));
        // Le menu partage le pinceau du panneau principal.
        // Les changements de teinte et d'opacite sont ainsi automatiques.
        var opaqueMenuBrush = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(
                background.Color.R,
                background.Color.G,
                background.Color.B))
        {
            Opacity = 1.0
        };

        glassBorder.SetResourceReference(
            Border.BackgroundProperty,
            "OpaqueCityMenuBackground");
        glassBorder.SetValue(
            Border.BorderBrushProperty,
            new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(96, 126, 149, 170)));
        glassBorder.SetValue(
            Border.BorderThicknessProperty,
            new Thickness(1));
        glassBorder.SetValue(
            Border.CornerRadiusProperty,
            new CornerRadius(14));
        glassBorder.SetValue(
            Border.PaddingProperty,
            new Thickness(7));

        glassBorder.SetValue(
            Border.EffectProperty,
            new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = System.Windows.Media.Colors.Black,
                BlurRadius = 20,
                ShadowDepth = 5,
                Opacity = 0.65
            });

        var itemsPresenter = new FrameworkElementFactory(typeof(ItemsPresenter));
        glassBorder.AppendChild(itemsPresenter);

        glassMenuTemplate.VisualTree = glassBorder;
        menu.Template = glassMenuTemplate;

        // GlassMenuOpaqueOpeningV3
        menu.Resources["OpaqueCityMenuBackground"] = opaqueMenuBrush;

        menu.Opened += (_, _) =>
        {
            if (parent.Resources["Panel"] is not
                System.Windows.Media.SolidColorBrush currentPanel)
                return;

            var color = currentPanel.Color;

            menu.Resources["OpaqueCityMenuBackground"] =
                new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(
                        color.R, color.G, color.B))
                {
                    Opacity = 1.0
                };
        };

        var glassItemStyle = new Style(typeof(MenuItem));

        glassItemStyle.Setters.Add(
            new Setter(
                MenuItem.ForegroundProperty,
                new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(225, 234, 242))));

        glassItemStyle.Setters.Add(
            new Setter(MenuItem.FontSizeProperty, 13.0));

        glassItemStyle.Setters.Add(
            new Setter(MenuItem.PaddingProperty, new Thickness(13, 10, 20, 10)));

        var itemTemplate = new ControlTemplate(typeof(MenuItem));

        var itemBorder = new FrameworkElementFactory(typeof(Border));
        itemBorder.Name = "GlassItemBorder";
        itemBorder.SetValue(
            Border.BackgroundProperty,
            System.Windows.Media.Brushes.Transparent);
        itemBorder.SetValue(
            Border.CornerRadiusProperty,
            new CornerRadius(9));

        var itemContent = new FrameworkElementFactory(typeof(ContentPresenter));
        itemContent.SetValue(
            ContentPresenter.ContentSourceProperty,
            "Header");
        itemContent.SetValue(
            FrameworkElement.MarginProperty,
            new Thickness(13, 10, 20, 10));

        itemBorder.AppendChild(itemContent);
        itemTemplate.VisualTree = itemBorder;

        var hoverTrigger = new Trigger
        {
            Property = MenuItem.IsHighlightedProperty,
            Value = true
        };

        hoverTrigger.Setters.Add(
            new Setter(
                Border.BackgroundProperty,
                new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(105, 65, 103, 132)),
                "GlassItemBorder"));

        itemTemplate.Triggers.Add(hoverTrigger);

        glassItemStyle.Setters.Add(
            new Setter(MenuItem.TemplateProperty, itemTemplate));

        menu.ItemContainerStyle = glassItemStyle;

        PreviewMouseWheel += (_, e) =>
        {
            e.Handled = true;
            SwitchCity(e.Delta > 0 ? -1 : 1);
        };

        var location = new MenuItem
        {
            Header = "Modifier ville 1..."
        };
        location.Click += (_, _) => ChooseCity(0);

        var refresh = new MenuItem
        {
            Header = "Actualiser maintenant"
        };
        refresh.Click += async (_, _) => await RefreshAsync();

        var secondLocation = new MenuItem
        {
            Header = "Configurer ville 2..."
        };
        secondLocation.Click += (_, _) => ChooseCity(1);

        menu.Items.Add(location);
        menu.Items.Add(secondLocation);

        var thirdLocation = new MenuItem
        {
            Header = "Configurer ville 3..."
        };
        thirdLocation.Click += (_, _) => ChooseCity(2);
        menu.Items.Add(thirdLocation);
        menu.Items.Add(refresh);

        // TemperatureUnitMenuV2
        var celsiusItem = new MenuItem
        {
            Header = "Celsius (\u00B0C)"
        };

        var fahrenheitItem = new MenuItem
        {
            Header = "Fahrenheit (\u00B0F)"
        };

        celsiusItem.Click += (_, _) =>
        {
            SetTemperatureUnit(false);
        };

        fahrenheitItem.Click += (_, _) =>
        {
            SetTemperatureUnit(true);
        };

        menu.Items.Add(celsiusItem);
        menu.Items.Add(fahrenheitItem);

        var frenchDateItem = new MenuItem
        {
            Header = "Date : français"
        };

        var americanDateItem = new MenuItem
        {
            Header = "Date : américain"
        };

        frenchDateItem.Click += (_, _) =>
        {
            weatherDateFormat = "fr";
            UpdateWeatherDate();
            SaveSettings();
        };

        americanDateItem.Click += (_, _) =>
        {
            weatherDateFormat = "en-US";
            UpdateWeatherDate();
            SaveSettings();
        };

        menu.Items.Add(frenchDateItem);
        menu.Items.Add(americanDateItem);

        menu.Opened += (_, _) =>
        {
            frenchDateItem.Header =
                (weatherDateFormat == "fr" ? "\u2713  " : "   ") +
                "Date : français";

            americanDateItem.Header =
                (weatherDateFormat == "en-US" ? "\u2713  " : "   ") +
                "Date : américain";
        };

        menu.Opened += (_, _) =>
        {
            celsiusItem.Header =
                (useFahrenheit ? "   " : "\u2713  ") +
                "Celsius (\u00B0C)";

            fahrenheitItem.Header =
                (useFahrenheit ? "\u2713  " : "   ") +
                "Fahrenheit (\u00B0F)";
        };

        ContextMenu = menu;
        border.ContextMenu = menu;

        LoadSettings();

        UpdateWeatherDate();
        weatherDateTimer.Tick += (_, _) => UpdateWeatherDate();
        weatherDateTimer.Start();
        Closed += (_, _) => weatherDateTimer.Stop();

        tomorrowWindow = new TomorrowWeatherWindow(parent);
        tomorrowWindow.SetTransparentMode(transparentAppearance);
        tomorrowWindow.MouseEnter += (_, _) => tomorrowCloseTimer.Stop();
        tomorrowWindow.MouseLeave += (_, _) => ScheduleTomorrowClose();
        tomorrowCloseTimer.Tick += (_, _) =>
        {
            tomorrowCloseTimer.Stop();
            if (!IsMouseOver && tomorrowWindow?.IsMouseOver != true)
                tomorrowWindow?.Hide();
        };

        timer.Tick += async (_, _) => await RefreshAsync();

        Loaded += async (_, _) =>
        {
            FollowParent();
            timer.Start();

            if (firstRefresh)
            {
                firstRefresh = false;
                await RefreshAsync();
            }
        };

        Closed += (_, _) =>
        {
            timer.Stop();
            tomorrowCloseTimer.Stop();
            tomorrowWindow?.Close();
        };
    }

    private void UpdateWeatherDate()
    {
        var culture = System.Globalization.CultureInfo.GetCultureInfo(
            weatherDateFormat == "en-US" ? "en-US" : "fr-FR");

        var format = weatherDateFormat == "en-US"
            ? "dddd, MMMM d, yyyy"
            : "dddd d MMMM yyyy";

        weatherDateText.Text = DateTime.Now.ToString(format, culture);
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(settingsPath))
            {
                var data = JsonSerializer.Deserialize<WeatherSettings>(
                    File.ReadAllText(settingsPath));

                if (data != null)
                {
                    enabled = data.Enabled;
                    weatherDateFormat =
                        data.DateFormat == "en-US" ? "en-US" : "fr";
                    useFahrenheit = data.UseFahrenheit;
                    city1 = string.IsNullOrWhiteSpace(data.City)
                        ? "Lille" : data.City.Trim();
                    city2 = (data.City2 ?? "").Trim();
                    city3 = (data.City3 ?? "").Trim();
                    selectedCity =
                        data.SelectedCity == 2 && city3.Length > 0 ? 2 :
                        data.SelectedCity == 1 && city2.Length > 0 ? 1 : 0;
                    city = selectedCity == 0 ? city1 : selectedCity == 1 ? city2 : city3;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex.Message);
        }

        cityText.Text = city;
        countryText.Text = "";
    }

    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(settingsPath)!);

            File.WriteAllText(settingsPath,
                JsonSerializer.Serialize(new WeatherSettings
                {
                    Enabled = enabled,
                    UseFahrenheit = useFahrenheit,
                    City = city1,
                    City2 = city2,
                    City3 = city3,
                    SelectedCity = selectedCity,
                    DateFormat = weatherDateFormat
                }));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex.Message);
        }
    }

    public void SetEnabled(bool value)
    {
        enabled = value;
        SaveSettings();
        UpdateVisibility();
    }

    public void SetCompact(bool value)
    {
        compact = value;
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        if (enabled && !compact &&
            parent.WindowState == WindowState.Normal)
        {
            FollowParent();

            if (!IsVisible)
            {
                Show();
                _ = RefreshAsync();
            }
        }
        else
        {
            tomorrowCloseTimer.Stop();
            tomorrowWindow?.Hide();
            if (IsVisible) Hide();
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

        var dpi = SatellitePositionV44.Dpi(this);
        double scalePx = visibleWidthPx / 340.0;
        double totalWidthPx = 358.0 * scalePx;
        double totalHeightPx = 184.0 * scalePx;

        Width = totalWidthPx / dpi.x;
        Height = totalHeightPx / dpi.y;

        SatellitePositionV44.Move(
            this,
            left.X - 9.0 * scalePx,
            left.Y - totalHeightPx
        );

        tomorrowWindow?.PositionNear(this);
    }    private void SwitchCity(int direction)
    {
        var available = new List<int> { 0 };

        if (!string.IsNullOrWhiteSpace(city2))
            available.Add(1);

        if (!string.IsNullOrWhiteSpace(city3))
            available.Add(2);

        if (available.Count < 2) return;

        int currentIndex = available.IndexOf(selectedCity);
        if (currentIndex < 0) currentIndex = 0;

        int nextIndex = (currentIndex +
            (direction > 0 ? 1 : -1) +
            available.Count) % available.Count;

        int next = available[nextIndex];
        if (next == selectedCity) return;

        selectedCity = next;
        city = next == 0 ? city1 : next == 1 ? city2 : city3;
        SaveSettings();

        tomorrowWindow?.Hide();
        ShowSelectedCity();
        if (IsMouseOver) ShowTomorrow();

        var transform = (TranslateTransform)weatherGrid.RenderTransform;
        transform.BeginAnimation(
            TranslateTransform.YProperty, null);
        weatherGrid.BeginAnimation(UIElement.OpacityProperty, null);

        var duration = new Duration(TimeSpan.FromMilliseconds(180));

        var slide = new DoubleAnimation(
            direction > 0 ? 9 : -9, 0, duration)
        {
            EasingFunction = new QuadraticEase
            {
                EasingMode = EasingMode.EaseOut
            }
        };

        var fade = new DoubleAnimation(0.25, 1, duration);

        transform.BeginAnimation(
            TranslateTransform.YProperty, slide);
        weatherGrid.BeginAnimation(
            UIElement.OpacityProperty, fade);

        if (!cache.TryGetValue(city, out var snapshot) ||
            DateTime.Now - snapshot.Updated > TimeSpan.FromMinutes(10))
        {
            _ = RefreshAsync();
        }
    }

    private void ShowTomorrow()
    {
        tomorrowCloseTimer.Stop();
        if (!enabled || compact || !IsVisible ||
            parent.WindowState != WindowState.Normal || tomorrowWindow == null)
            return;
        tomorrowWindow.PositionNear(this);
        UpdateTomorrow();
        if (!tomorrowWindow.IsVisible) tomorrowWindow.Show(); tomorrowWindow.PositionNear(this);
    }

    private void ScheduleTomorrowClose()
    {
        tomorrowCloseTimer.Stop();
        tomorrowCloseTimer.Start();
    }

    private void UpdateTomorrow()
    {
        if (tomorrowWindow == null) return;
        if (cache.TryGetValue(city, out var snapshot))
            tomorrowWindow.SetForecast(snapshot.TomorrowCode,
                FormatTemperature(snapshot.TomorrowMin, false),
                FormatTemperature(snapshot.TomorrowMax, false),
                snapshot.TomorrowSunrise, snapshot.TomorrowSunset, DescribeLocalized(snapshot.TomorrowCode));
        else
            tomorrowWindow.SetForecast(2, "--°", "--°", "--:--", "--:--", WeatherTranslate("Chargement...", "Loading..."));
    }

    private static FrameworkElement CreateSunIcon(bool sunrise)
    {
        var canvas = new Canvas
        {
            Width = 120,
            Height = 105,
            Margin = new Thickness(0),
            IsHitTestVisible = false,
            ClipToBounds = true
        };

        var sunTop = sunrise
            ? Color.FromRgb(255, 242, 180)
            : Color.FromRgb(255, 216, 156);

        var sunBottom = sunrise
            ? Color.FromRgb(240, 163, 68)
            : Color.FromRgb(231, 122, 88);

        var rayColor = sunrise
            ? Color.FromRgb(246, 207, 120)
            : Color.FromRgb(233, 161, 123);

        var horizonColor = sunrise
            ? Color.FromRgb(244, 213, 162)
            : Color.FromRgb(234, 185, 155);

        var arrowColor = sunrise
            ? Color.FromRgb(246, 206, 131)
            : Color.FromRgb(236, 160, 128);

        var sunGradient = new LinearGradientBrush(
            sunTop,
            sunBottom,
            new Point(0, 0),
            new Point(0, 1));

        // Demi-soleil visible uniquement au-dessus de l'horizon.
        var sun = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse(
                "M 35,60 A 25,25 0 0 1 85,60 Z"),
            Fill = sunGradient,
            Stroke = null
        };

        // Rayons, avec les deux rayons lateraux remontes.
        var rays = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse(
                "M 60,11 L 60,20 " +
                "M 31,24 L 38,31 " +
                "M 89,24 L 82,31 " +
                "M 18,47 L 27,47 " +
                "M 102,47 L 93,47"),
            Stroke = new SolidColorBrush(rayColor),
            StrokeThickness = 3,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Fill = Brushes.Transparent
        };

        // Horizon clair, sans barre bleue.
        var horizon = new System.Windows.Shapes.Line
        {
            X1 = 12,
            Y1 = 60,
            X2 = 108,
            Y2 = 60,
            Stroke = new SolidColorBrush(horizonColor),
            StrokeThickness = 3,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };

        // Fleche montante pour le lever,
        // descendante pour le coucher.
        var arrow = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse(
                sunrise
                    ? "M 60,96 L 60,74 M 50,84 L 60,74 L 70,84"
                    : "M 60,74 L 60,96 M 50,86 L 60,96 L 70,86"),
            Stroke = new SolidColorBrush(arrowColor),
            StrokeThickness = 4,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Fill = Brushes.Transparent
        };

        canvas.Children.Add(sun);
        canvas.Children.Add(rays);
        canvas.Children.Add(horizon);
        canvas.Children.Add(arrow);

        return new Viewbox
        {
            Width = 30, Height = 26,
            RenderTransform = new TranslateTransform(-20, 0),
            Stretch = Stretch.Uniform,
            Child = canvas
        };
    }

    private static string FormatSunTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "--:--";

        return DateTime.TryParse(
            value,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out var date)
            ? date.ToString("HH:mm")
            : "--:--";
    }
    private void ShowSelectedCity()
    {
        UpdateTomorrow();
        cityText.Text = city;
        countryText.Text = "";

        if (cache.TryGetValue(city, out var snapshot))
        {
            cityText.Text = snapshot.Name;
            countryText.Text = snapshot.Country;
            tempText.Text = FormatTemperature(snapshot.Temperature, true);
            conditionText.Text = DescribeLocalized(snapshot.Code);
            iconView.Child = WeatherIcons.Create(snapshot.Code);
            sunriseText.Text = snapshot.Sunrise;
            sunsetText.Text = snapshot.Sunset;
            statusText.Text = SlotLabel +
                WeatherTranslate("Mise à jour ", "Updated ") +
                snapshot.Updated.ToString("HH:mm");
        }
        else
        {
            tempText.Text = FormatTemperature("--", true);
            conditionText.Text = WeatherTranslate("Chargement...", "Loading...");
            iconView.Child = WeatherIcons.Create(2);
            sunriseText.Text = "--:--";
            sunsetText.Text = "--:--";
            statusText.Text = SlotLabel + WeatherTranslate("Actualisation...", "Updating...");
        }
    }

    private void ChooseCity(int slot)
    {
        var dialog = new Window
        {
            Title = "Localisation meteo",
            Width = 330,
            Height = 160,
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Background = new SolidColorBrush(
                Color.FromRgb(42, 52, 67))
        };

        var panel = new StackPanel
        {
            Margin = new Thickness(17)
        };

        panel.Children.Add(MakeText("Ville :", 13));

        var input = new TextBox
        {
            Text = slot == 0 ? city1 : slot == 1 ? city2 : city3,
            Margin = new Thickness(0, 8, 0, 10),
            FontSize = 14
        };

        var button = new Button
        {
            Content = "Valider",
            HorizontalAlignment = HorizontalAlignment.Right,
            Padding = new Thickness(16, 5, 16, 5)
        };

        button.Click += (_, _) => dialog.DialogResult = true;

        input.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
                dialog.DialogResult = true;
        };

        panel.Children.Add(input);
        panel.Children.Add(button);
        dialog.Content = panel;

        if (dialog.ShowDialog() == true &&
            !string.IsNullOrWhiteSpace(input.Text))
        {
            string newCity = input.Text.Trim();

            if (slot == 0)
                city1 = newCity;
            else if (slot == 1)
                city2 = newCity;
            else
                city3 = newCity;

            selectedCity = slot;
            city = newCity;
            cache.Remove(newCity);
            SaveSettings();
            ShowSelectedCity();
            _ = RefreshAsync();
            if (IsMouseOver) ShowTomorrow();
        }
    }

    private static (string Text, string Icon) Describe(int code)
    {
        if (code == 0) return ("Ensoleille", "☀");
        if (code <= 2) return ("Partiellement nuageux", "⛅");
        if (code == 3) return ("Couvert", "☁");
        if (code == 45 || code == 48) return ("Brouillard", "≋");
        if (code >= 51 && code <= 67) return ("Pluie", "☂");
        if (code >= 71 && code <= 77) return ("Neige", "❄");
        if (code >= 80 && code <= 82) return ("Averses", "☂");
        if (code >= 95) return ("Orage", "⚡");
        return ("Conditions variables", "☁");
    }

    private async Task RefreshAsync()
    {
        if (busy || !enabled) return;
        string requestedCity = city;

        busy = true;

        try
        {
            statusText.Text = SlotLabel + WeatherTranslate("Actualisation...", "Updating...");



            string geocodeUrl =
                "https://geocoding-api.open-meteo.com/v1/search" +
                "?name=" + Uri.EscapeDataString(requestedCity) +
                "&count=1&language=fr&format=json";

            using var geoResponse = await client.GetAsync(geocodeUrl);
            geoResponse.EnsureSuccessStatusCode();

            using var geo = JsonDocument.Parse(
                await geoResponse.Content.ReadAsStringAsync());

            if (requestedCity != city) return;

            if (!geo.RootElement.TryGetProperty("results", out var results) ||
                results.GetArrayLength() == 0)
            {
                statusText.Text = SlotLabel + "Ville introuvable";
                return;
            }

            var place = results[0];

            double lat = place.GetProperty("latitude").GetDouble();
            double lon = place.GetProperty("longitude").GetDouble();

            string weatherUrl =
                "https://api.open-meteo.com/v1/forecast" +
                "?latitude=" + lat.ToString(
                    System.Globalization.CultureInfo.InvariantCulture) +
                "&longitude=" + lon.ToString(
                    System.Globalization.CultureInfo.InvariantCulture) +
                "&current=temperature_2m,weather_code" +
                "&daily=sunrise,sunset,temperature_2m_max,temperature_2m_min,weather_code" +
                "&timezone=auto";

            using var response = await client.GetAsync(weatherUrl);
            response.EnsureSuccessStatusCode();

            using var data = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync());

            if (requestedCity != city) return;

            var current = data.RootElement.GetProperty("current");

            double temperature =
                current.GetProperty("temperature_2m").GetDouble();

            int weatherCode =
                current.GetProperty("weather_code").GetInt32();

            string sunrise = "--:--";
            string sunset = "--:--";
            string tomorrowSunrise = "--:--";
            string tomorrowSunset = "--:--";
            string tomorrowMin = "--°";
            string tomorrowMax = "--°";
            int tomorrowCode = 2;

            if (data.RootElement.TryGetProperty("daily", out var daily))
            {
                if (daily.TryGetProperty("sunrise", out var rises) &&
                    rises.ValueKind == JsonValueKind.Array &&
                    rises.GetArrayLength() > 0)
                {
                    sunrise = FormatSunTime(rises[0].GetString());
                }

                if (daily.TryGetProperty("sunset", out var sets) &&
                    sets.ValueKind == JsonValueKind.Array &&
                    sets.GetArrayLength() > 0)
                {
                    sunset = FormatSunTime(sets[0].GetString());
                }
                if (daily.TryGetProperty("time", out var dates) &&
                    dates.ValueKind == JsonValueKind.Array &&
                    dates.GetArrayLength() > 1 &&
                    DateOnly.TryParse(dates[0].GetString(), out var day0) &&
                    DateOnly.TryParse(dates[1].GetString(), out var day1) &&
                    day1 == day0.AddDays(1))
                {
                    if (daily.TryGetProperty("sunrise", out var tr) &&
                        tr.GetArrayLength() > 1)
                        tomorrowSunrise = FormatSunTime(tr[1].GetString());
                    if (daily.TryGetProperty("sunset", out var ts) &&
                        ts.GetArrayLength() > 1)
                        tomorrowSunset = FormatSunTime(ts[1].GetString());
                    if (daily.TryGetProperty("temperature_2m_min", out var mn) &&
                        mn.GetArrayLength() > 1 && mn[1].ValueKind == JsonValueKind.Number)
                        tomorrowMin = Math.Round(mn[1].GetDouble()).ToString("0") + "°";
                    if (daily.TryGetProperty("temperature_2m_max", out var mx) &&
                        mx.GetArrayLength() > 1 && mx[1].ValueKind == JsonValueKind.Number)
                        tomorrowMax = Math.Round(mx[1].GetDouble()).ToString("0") + "°";
                    if (daily.TryGetProperty("weather_code", out var codes) &&
                        codes.GetArrayLength() > 1 && codes[1].ValueKind == JsonValueKind.Number)
                        tomorrowCode = codes[1].GetInt32();
                }
            }
            var description = Describe(weatherCode);

            string resolvedCountry =
                place.TryGetProperty("country", out var countryProperty)
                    ? countryProperty.GetString() ?? ""
                    : "";

            string resolvedName = place.GetProperty("name").GetString()
                ?? requestedCity;

            var snapshot = new WeatherSnapshot(
                resolvedName,
                resolvedCountry,
                Math.Round(temperature).ToString("0") + "°C",
                description.Text,
                weatherCode,
                "Mise a jour " + DateTime.Now.ToString("HH:mm"),
                DateTime.Now,
                sunrise,
                sunset,
                tomorrowCode,
                tomorrowMin,
                tomorrowMax,
                tomorrowSunrise,
                tomorrowSunset);

            cache[requestedCity] = snapshot;

            if (requestedCity == city)
                ShowSelectedCity();
        }
        catch (Exception ex)
        {
            statusText.Text = SlotLabel + WeatherTranslate("Meteo indisponible", "Weather unavailable");
            System.Diagnostics.Debug.WriteLine(
                "Meteo : " + ex.Message);
        }
        finally
        {
            busy = false;

            if (requestedCity != city && enabled &&
                (!cache.TryGetValue(city, out var current) ||
                 DateTime.Now - current.Updated >
                    TimeSpan.FromMinutes(10)))
            {
                _ = RefreshAsync();
            }
        }
    }
}