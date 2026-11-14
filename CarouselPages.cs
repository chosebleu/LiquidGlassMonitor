using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace LiquidGlassMonitor;

public partial class MainWindow
{
    // La page Systeme (0) est toujours active. La page Reseau (1) est facultative.
    private bool networkCarouselEnabled = true;
    private readonly string carouselSettingsFile = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LiquidGlassMonitor", "carousel-pages.txt");

    private int[] ActiveCarouselPages() => networkCarouselEnabled ? new[] { 0, 1 } : new[] { 0 };

    private void LoadCarouselPages()
    {
        try
        {
            if (File.Exists(carouselSettingsFile))
                networkCarouselEnabled = File.ReadAllText(carouselSettingsFile).Trim() != "0";
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
        carouselPage = 0;
        ApplyCarouselPage();
    }

    private void SaveCarouselPages()
    {
        try
        {
            string? directory = System.IO.Path.GetDirectoryName(carouselSettingsFile);
            if (directory != null) Directory.CreateDirectory(directory);
            File.WriteAllText(carouselSettingsFile, networkCarouselEnabled ? "1" : "0");
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
    }

    private void SetupCarouselPagesOption()
    {
        if (SettingsButton.ContextMenu is not { } menu) return;
        StackPanel? panel = null;
        foreach (object item in menu.Items)
        {
            if (item is MenuItem mi && mi.Header is StackPanel stack)
            {
                panel = stack;
                break;
            }
        }
        if (panel == null) return;
        foreach (UIElement child in panel.Children)
        {
            if (child is CheckBox cb && cb.Name == "CarouselNetworkCheckBox")
            {
                cb.IsChecked = networkCarouselEnabled;
                return;
            }
        }
        panel.Children.Add(new TextBlock
        {
            Text = "PAGES DU CARROUSEL",
            Foreground = Brushes.White,
            FontSize = 11,
            Margin = new Thickness(0, 17, 0, 7)
        });
        panel.Children.Add(new CheckBox
        {
            Content = "Système (toujours actif)",
            IsChecked = true,
            IsEnabled = false,
            Foreground = Brushes.White,
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 5)
        });
        var network = new CheckBox
        {
            Name = "CarouselNetworkCheckBox",
            Content = "Réseau",
            IsChecked = networkCarouselEnabled,
            Foreground = Brushes.White,
            FontSize = 12,
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 0, 0, 5)
        };
        network.Checked += (_, _) => ChangeCarouselNetwork(true);
        network.Unchecked += (_, _) => ChangeCarouselNetwork(false);
        panel.Children.Add(network);
    }

    private void ChangeCarouselNetwork(bool enabled)
    {
        networkCarouselEnabled = enabled;
        if (!enabled && carouselPage == 1) carouselPage = 0;
        SaveCarouselPages();
        ApplyCarouselPage();
    }

    private void UpdateCarouselDots()
    {
        int[] pages = ActiveCarouselPages();
        CarouselDots.Visibility = !compactMode && pages.Length > 1
            ? Visibility.Visible : Visibility.Collapsed;
        while (CarouselDots.Children.Count > pages.Length)
            CarouselDots.Children.RemoveAt(CarouselDots.Children.Count - 1);
        while (CarouselDots.Children.Count < pages.Length)
            CarouselDots.Children.Add(new Ellipse
            {
                Fill = new SolidColorBrush(Color.FromRgb(180, 200, 215)),
                Margin = new Thickness(4, 0, 4, 0)
            });
        for (int i = 0; i < pages.Length; i++)
        {
            if (CarouselDots.Children[i] is Ellipse dot)
            {
                bool selected = pages[i] == carouselPage;
                dot.Width = selected ? 7 : 5;
                dot.Height = selected ? 7 : 5;
                dot.Opacity = selected ? 1.0 : 0.35;
            }
        }
    }
}
