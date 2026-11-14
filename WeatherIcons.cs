using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace LiquidGlassMonitor;

internal static class WeatherIcons
{
    private static Brush Gradient(
        Color top, Color middle, Color bottom)
    {
        return new LinearGradientBrush(
            new GradientStopCollection
            {
                new GradientStop(top, 0),
                new GradientStop(middle, 0.52),
                new GradientStop(bottom, 1)
            },
            new Point(0.25, 0),
            new Point(0.75, 1));
    }

    private static DropShadowEffect Shadow(
        Color color, double opacity = 0.25)
    {
        return new DropShadowEffect
        {
            Color = color,
            Opacity = opacity,
            BlurRadius = 6,
            ShadowDepth = 2,
            Direction = 270
        };
    }

    private static Path PathShape(string data, Brush fill)
    {
        return new Path
        {
            Data = Geometry.Parse(data),
            Fill = fill
        };
    }

    public static Canvas Create(int code)
    {
        var canvas = new Canvas
        {
            Width = 100,
            Height = 100
        };

        var gold = Gradient(
            Color.FromRgb(255, 247, 174),
            Color.FromRgb(255, 197, 77),
            Color.FromRgb(239, 145, 48));

        var whiteCloud = Gradient(
            Colors.White,
            Color.FromRgb(217, 233, 247),
            Color.FromRgb(140, 174, 204));

        var greyCloud = Gradient(
            Color.FromRgb(218, 231, 244),
            Color.FromRgb(148, 174, 201),
            Color.FromRgb(83, 111, 144));

        var blue = Gradient(
            Color.FromRgb(185, 247, 255),
            Color.FromRgb(76, 191, 247),
            Color.FromRgb(41, 123, 214));

        void Sun(double x, double y, double size)
        {
            double cx = x + size / 2;
            double cy = y + size / 2;
            double r = size / 2;

            for (int i = 0; i < 8; i++)
            {
                double a = i * Math.PI / 4;

                canvas.Children.Add(new Line
                {
                    X1 = cx + Math.Cos(a) * (r + 5),
                    Y1 = cy + Math.Sin(a) * (r + 5),
                    X2 = cx + Math.Cos(a) * (r + 11),
                    Y2 = cy + Math.Sin(a) * (r + 11),
                    Stroke = new SolidColorBrush(
                        Color.FromRgb(255, 211, 106)),
                    StrokeThickness = 3,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                });
            }

            var disc = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = gold,
                Effect = Shadow(
                    Color.FromRgb(246, 176, 65), 0.35)
            };

            Canvas.SetLeft(disc, x);
            Canvas.SetTop(disc, y);
            canvas.Children.Add(disc);

            var reflection = new Ellipse
            {
                Width = size * 0.58,
                Height = size * 0.23,
                Fill = new SolidColorBrush(
                    Color.FromArgb(105, 255, 255, 255))
            };

            Canvas.SetLeft(reflection, x + size * 0.18);
            Canvas.SetTop(reflection, y + size * 0.14);
            canvas.Children.Add(reflection);
        }

        void Cloud(bool dark = false)
        {
            var shape = PathShape(
                "M 24,73 C 11,73 10,57 21,52 " +
                "C 25,50 29,49 33,50 " +
                "C 36,35 52,29 65,37 " +
                "C 71,41 75,47 76,53 " +
                "C 90,52 96,62 91,71 " +
                "C 88,76 83,78 76,78 " +
                "L 26,78 C 25,78 24,76 24,73 Z",
                dark ? greyCloud : whiteCloud);

            shape.Effect = Shadow(
                Color.FromRgb(12, 29, 49), 0.32);

            canvas.Children.Add(shape);

            canvas.Children.Add(PathShape(
                "M 35,49 C 40,38 53,35 62,41 " +
                "C 53,39 44,43 40,51 Z",
                new SolidColorBrush(
                    Color.FromArgb(95, 255, 255, 255))));
        }

        void Rain()
        {
            for (int i = 0; i < 3; i++)
            {
                // Goutte arrondie, plus petite de 20 %.
                var drop = PathShape(
                    "M 0,0 " +
                    "C -1.5,0 -2.8,2.5 -4.5,6.5 " +
                    "C -6.2,10.5 -6,13.5 -3.8,15.5 " +
                    "C -1.8,17.5 1.8,17.5 3.8,15.5 " +
                    "C 6,13.5 6.2,10.5 4.5,6.5 " +
                    "C 2.8,2.5 1.5,0 0,0 Z",
                    blue);

                drop.RenderTransform = new ScaleTransform(0.68, 0.68);

                Canvas.SetLeft(drop, 32 + i * 19);
                Canvas.SetTop(drop, 77 + (i % 2) * 3);

                canvas.Children.Add(drop);
            }
        }

        void Snow()
        {
            var brush = new SolidColorBrush(
                Color.FromRgb(175, 233, 255));

            for (int i = 0; i < 3; i++)
            {
                double x = 31 + i * 19;
                double y = 86 + (i % 2) * 3;

                for (int j = 0; j < 3; j++)
                {
                    double a = j * Math.PI / 3;

                    canvas.Children.Add(new Line
                    {
                        X1 = x - Math.Cos(a) * 5,
                        Y1 = y - Math.Sin(a) * 5,
                        X2 = x + Math.Cos(a) * 5,
                        Y2 = y + Math.Sin(a) * 5,
                        Stroke = brush,
                        StrokeThickness = 2,
                        StrokeStartLineCap = PenLineCap.Round,
                        StrokeEndLineCap = PenLineCap.Round
                    });
                }
            }
        }

        if (code == 0)
        {
            Sun(30, 30, 40);
        }
        else if (code <= 2)
        {
            Sun(47, 16, 32);
            Cloud();
        }
        else if (code == 3)
        {
            Cloud();
        }
        else if (code == 45 || code == 48)
        {
            Cloud();

            for (int i = 0; i < 3; i++)
            {
                canvas.Children.Add(new Line
                {
                    X1 = 22,
                    Y1 = 82 + i * 6,
                    X2 = 78,
                    Y2 = 82 + i * 6,
                    Stroke = new SolidColorBrush(
                        Color.FromArgb(130, 188, 216, 238)),
                    StrokeThickness = 2.5,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                });
            }
        }
        else if ((code >= 51 && code <= 67) ||
                 (code >= 80 && code <= 82))
        {
            Cloud(true);
            Rain();
        }
        else if (code >= 71 && code <= 77)
        {
            Cloud();
            Snow();
        }
        else if (code >= 95)
        {
            Cloud(true);

            var lightning = PathShape(
                "M 53,73 L 43,87 L 53,86 " +
                "L 48,99 L 68,79 L 56,81 " +
                "L 62,73 Z",
                Gradient(
                    Color.FromRgb(255, 250, 173),
                    Color.FromRgb(255, 209, 65),
                    Color.FromRgb(241, 143, 39)));

            lightning.Effect = Shadow(
                Color.FromRgb(255, 195, 57), 0.4);

            canvas.Children.Add(lightning);
        }
        else
        {
            Sun(47, 16, 32);
            Cloud();
        }

        return canvas;
    }
}